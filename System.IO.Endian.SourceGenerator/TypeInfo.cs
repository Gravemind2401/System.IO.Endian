using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using System.Collections.Immutable;
using static System.IO.Endian.SourceGenerator.DiagnosticDescriptors;
using static System.IO.Endian.SourceGenerator.Globals;

namespace System.IO.Endian.SourceGenerator
{
    internal sealed record TypeInfo(
        string Namespace,
        string Name,
        TypeKind Kind,
        ImmutableEquatableArray<FixedSizeAttributeData> FixedSizeAttributes,
        ImmutableEquatableArray<ByteOrderAttributeData> ByteOrderAttributes,
        ImmutableEquatableArray<PropertyInfo> Properties)
    {
        public static TypeInfo? FromContext(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken, out ImmutableEquatableArray<DiagnosticInfo> diagnostics)
        {
            diagnostics = [];

            var typeSymbol = (ITypeSymbol)context.TargetSymbol;

            if (typeSymbol.AllInterfaces.Any(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == TargetInterface))
            {
                diagnostics = [DiagnosticInfo.Create(DuplicateInterfaceForStreamableObjectAttribute, typeSymbol, typeSymbol.Name)];
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var typeSyntax = (TypeDeclarationSyntax)context.TargetNode;
            var attributes = typeSymbol.GetAttributes();

            cancellationToken.ThrowIfCancellationRequested();

            var diagnosticsBuilder = ImmutableArray.CreateBuilder<DiagnosticInfo>();
            var fixedSizeBuilder = ImmutableArray.CreateBuilder<FixedSizeAttributeData>();
            var byteOrderBuilder = ImmutableArray.CreateBuilder<ByteOrderAttributeData>();

            var versionProp = default(PropertyInfo);

            foreach (var attribute in attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                //IncrementalValuesProvider<T>.ForAttributeWithMetadataName<T> does this check when finding matching attributes, so it must be important
                if (attribute.ApplicationSyntaxReference?.SyntaxTree != typeSyntax.SyntaxTree)
                    continue;

                var attributeDisplayName = attribute.AttributeClass?.ToFullyQualifiedGlobalDisplayString();
                if (attributeDisplayName == null || !attributeDisplayName.AsSpan().StartsWith(HomeNamespaceGlobal))
                    continue;

                var attributeNameSpan = attributeDisplayName.AsSpan(HomeNamespaceGlobal.Length + 1);

                if (attributeNameSpan.SequenceEqual("FixedSizeAttribute"))
                {
                    var size = (long)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    fixedSizeBuilder.Add(new FixedSizeAttributeData(size, minVersion, maxVersion));
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("ByteOrderAttribute"))
                {
                    var order = (ByteOrder)(int)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    byteOrderBuilder.Add(new ByteOrderAttributeData(order, minVersion, maxVersion));
                    continue;
                }
            }

            var propertyBuilder = ImmutableArray.CreateBuilder<PropertyInfo>();

            foreach (var memberSyntax in typeSyntax.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!memberSyntax.IsKind(SyntaxKind.PropertyDeclaration))
                    continue;

                if (memberSyntax.AttributeLists.Count == 0)
                    continue; //definitely not a readable property if no attributes

                if (context.SemanticModel.GetDeclaredSymbol(memberSyntax, cancellationToken) is not IPropertySymbol propertySymbol)
                    continue;

                var prop = PropertyInfo.FromSymbol(propertySymbol, memberSyntax.SyntaxTree, cancellationToken, diagnosticsBuilder);
                if (prop.IsVersionProperty)
                {
                    if (versionProp == null)
                        versionProp = prop;
                    else
                    {
                        diagnosticsBuilder.Add(DiagnosticInfo.Create(
                            MultipleMembersWithVersionNumberAttribute,
                            prop.Symbol,
                            typeSymbol.Name
                        ));
                    }
                }

                propertyBuilder.Add(prop);
            }

            if (diagnosticsBuilder.Count > 0)
            {
                diagnostics = diagnosticsBuilder.ToImmutableEquatableArray();
                return null;
            }

            return new TypeInfo(
                typeSymbol.ContainingNamespace.ToFullyQualifiedLocalDisplayString(),
                typeSymbol.Name,
                typeSymbol.TypeKind,
                fixedSizeBuilder.ToImmutableEquatableArray(),
                byteOrderBuilder.ToImmutableEquatableArray(),
                propertyBuilder.ToImmutableEquatableArray());
        }

        public CompilationUnitSyntax GetCompilationUnit()
        {
            var typeDec = GetDeclarationSyntax()
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .AddMembers(GetMethodSyntax());

            typeDec = typeDec.WithBaseList(SyntaxFactory.BaseList([SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(TargetInterface))]));

            //ensure the generated files dont produce warnings that consumers have no control over
            var triviaList = SyntaxFactory.TriviaList(
                SyntaxFactory.Comment("// <auto-generated/>"),
                SyntaxFactory.Trivia(SyntaxFactory.PragmaWarningDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.DisableKeyword), true)),
                SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true))
            );

            return SyntaxFactory.CompilationUnit().AddMembers(
                SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(Namespace))
                    .WithLeadingTrivia(triviaList)
                    .AddMembers(typeDec)
            ).NormalizeWhitespace();
        }

        private TypeDeclarationSyntax GetDeclarationSyntax()
        {
            return Kind switch
            {
                TypeKind.Struct => SyntaxFactory.StructDeclaration(Name),
                TypeKind.Class => SyntaxFactory.ClassDeclaration(Name),
                _ => throw new NotSupportedException()
            };
        }

        private MethodDeclarationSyntax[] GetMethodSyntax()
        {
            var helper = new VersionRangeHelper(this, SyntaxFactory.IdentifierName("version"));

            var readMethodDec = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), InterfaceReadMethod)
                .WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(SyntaxFactory.IdentifierName(TargetInterface)))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("reader")).WithType(SyntaxFactory.IdentifierName("global::System.IO.Endian.EndianReader")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("version")).WithType(SyntaxFactory.IdentifierName("double?")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("origin")).WithType(SyntaxFactory.IdentifierName("long"))
                )
                .WithBody(SyntaxFactory.Block(EnumerateReadStatements(helper).ToArray()));

            var writeMethodDec = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), InterfaceWriteMethod)
                .WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(SyntaxFactory.IdentifierName(TargetInterface)))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("writer")).WithType(SyntaxFactory.IdentifierName("global::System.IO.Endian.EndianWriter")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("version")).WithType(SyntaxFactory.IdentifierName("double?")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("origin")).WithType(SyntaxFactory.IdentifierName("long"))
                )
                .WithBody(SyntaxFactory.Block(EnumerateWriteStatements(helper).ToArray()));

            return [readMethodDec, writeMethodDec];
        }

        private IEnumerable<(PropertyInfo, OffsetAttributeData)> EnumeratePropertyOffsets(double? version)
        {
            //always read/write in order of offset so the final position is at the end of the highest property
            return from p in Properties
                   let offsetAttribute = p.OffsetAttributes.FirstOrDefault(o => o.ValidForVersion(version))
                   where offsetAttribute != null
                   orderby offsetAttribute.Offset
                   select (p, offsetAttribute);
        }

        private IEnumerable<StatementSyntax> EnumerateReadStatements(VersionRangeHelper versionHelper)
        {
            var baseAddressIdentifier = SyntaxFactory.IdentifierName("origin");
            var readerIdentifier = SyntaxFactory.IdentifierName("reader");
            var seekIdentifier = SyntaxFactory.IdentifierName("Seek");
            var seekOriginBeginExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("global::System.IO.SeekOrigin"),
                SyntaxFactory.IdentifierName("Begin"));

            if (!versionHelper.IsVersioned)
            {
                var body = BuildStatementsForVersion(null);
                foreach (var statement in body)
                    yield return statement;
                yield break;
            }

            var versionIdentifier = SyntaxFactory.IdentifierName("version");
            var versionProperty = Properties.FirstOrDefault(p => p.IsVersionProperty);

            if (versionProperty != null)
            {
                var offset = versionProperty.OffsetAttributes[0].Offset;
                var byteOrder = versionProperty.ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(null))?.ByteOrder
                    ?? ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(null))?.ByteOrder;

                var commentTrivia = SyntaxFactory.TriviaList(
                    SyntaxFactory.Comment($"//{offset} [0x{offset:X2}] (VersionNumber)")
                );

                yield return CreateSeekStatement(offset).WithLeadingTrivia(commentTrivia);

                var readExpression = versionProperty.GetReadExpressionForVersion(null, byteOrder);
                if (versionProperty.Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is not ("double" or "double?"))
                    readExpression = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName("double"), readExpression);

                yield return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    versionIdentifier,
                    readExpression
                ));
            }

            yield return versionHelper.VersionCheckStatement!;

            foreach (var localMethod in versionHelper.EnumerateVersionMethodDeclarations(BuildStatementsForVersion))
                yield return localMethod;

            StatementSyntax CreateSeekStatement(long relativeOffset)
            {
                ExpressionSyntax argumentExpression = relativeOffset == 0
                    ? baseAddressIdentifier
                    : SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, baseAddressIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(relativeOffset)));

                //reader.Seek(baseAddress + {Offset}L, SeekOrigin.Begin);
                return SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        readerIdentifier,
                        seekIdentifier
                    )).AddArgumentListArguments(
                        SyntaxFactory.Argument(argumentExpression),
                        SyntaxFactory.Argument(seekOriginBeginExpression)
                    )
                );
            }

            ImmutableArray<StatementSyntax> BuildStatementsForVersion(double? version)
            {
                var builder = ImmutableArray.CreateBuilder<StatementSyntax>();
                var byteOrderAttribute = ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(version));

                long? currentOffset = null;
                foreach (var (property, offsetAttribute) in EnumeratePropertyOffsets(version))
                {
                    var readStatement = property.GetSetterStatementForVersion(version, byteOrderAttribute?.ByteOrder);
                    var commentTrivia = SyntaxFactory.TriviaList(
                        SyntaxFactory.Comment($"//{offsetAttribute.Offset} [0x{offsetAttribute.Offset:X2}]")
                    );

                    if (offsetAttribute.Offset == currentOffset)
                        readStatement = readStatement.WithLeadingTrivia(commentTrivia);
                    else
                        builder.Add(CreateSeekStatement(offsetAttribute.Offset).WithLeadingTrivia(commentTrivia));

                    builder.Add(readStatement);

                    currentOffset = property.PropertySize.HasValue
                        ? offsetAttribute.Offset + property.PropertySize.Value
                        : null;
                }

                var fixedSizeAttribute = FixedSizeAttributes.FirstOrDefault(o => o.ValidForVersion(version));
                if (fixedSizeAttribute != null)
                {
                    //reader.Seek({FixedSize}, SeekOrigin.Begin);
                    builder.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            readerIdentifier,
                            seekIdentifier
                        )).AddArgumentListArguments(SyntaxFactory.Argument(
                            SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, baseAddressIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(fixedSizeAttribute.Size)))
                        ), SyntaxFactory.Argument(seekOriginBeginExpression)
                    )).WithLeadingTrivia(SyntaxFactory.TriviaList(
                        SyntaxFactory.Comment($"//{fixedSizeAttribute.Size} [0x{fixedSizeAttribute.Size:X2}] (FixedSize)")
                    )));
                }

                return builder.ToImmutableArray();
            }
        }

        private IEnumerable<StatementSyntax> EnumerateWriteStatements(VersionRangeHelper versionHelper)
        {
            var baseAddressIdentifier = SyntaxFactory.IdentifierName("origin");
            var readerIdentifier = SyntaxFactory.IdentifierName("reader");
            var seekIdentifier = SyntaxFactory.IdentifierName("Seek");
            var seekOriginBeginExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("global::System.IO.SeekOrigin"),
                SyntaxFactory.IdentifierName("Begin"));

            if (!versionHelper.IsVersioned)
            {
                var body = BuildStatementsForVersion(null);
                foreach (var statement in body)
                    yield return statement;
                yield break;
            }

            var versionIdentifier = SyntaxFactory.IdentifierName("version");
            var versionProperty = Properties.FirstOrDefault(p => p.IsVersionProperty);

            if (versionProperty != null)
            {
                //TODO: "version ??= this.VersionProperty;"
            }

            yield return versionHelper.VersionCheckStatement!;

            foreach (var localMethod in versionHelper.EnumerateVersionMethodDeclarations(BuildStatementsForVersion))
                yield return localMethod;

            ImmutableArray<StatementSyntax> BuildStatementsForVersion(double? version)
            {
                var builder = ImmutableArray.CreateBuilder<StatementSyntax>();

                //TODO

                return builder.ToImmutableArray();
            }
        }
    }
}
