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

            var fixedSizeBuilder = ImmutableArray.CreateBuilder<FixedSizeAttributeData>();
            var byteOrderBuilder = ImmutableArray.CreateBuilder<ByteOrderAttributeData>();

            var attributes = typeSymbol.GetAttributes();

            foreach (var attribute in attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                //IncrementalValuesProvider<T>.ForAttributeWithMetadataName<T> does this check when finding matching attributes, so it must be important
                if (attribute.ApplicationSyntaxReference?.SyntaxTree != typeSyntax.SyntaxTree)
                    continue;

                var attributeDisplayName = attribute.AttributeClass?.ToDisplayString(FullyQualifiedDisplayFormat);
                if (attributeDisplayName == null || !attributeDisplayName.AsSpan().StartsWith(HomeNamespace))
                    continue;

                var attributeNameSpan = attributeDisplayName.AsSpan(HomeNamespace.Length + 1);

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
                    continue; //not a readable property if no attribute

                if (context.SemanticModel.GetDeclaredSymbol(memberSyntax, cancellationToken) is not IPropertySymbol propertySymbol)
                    continue;

                propertyBuilder.Add(PropertyInfo.FromSymbol(propertySymbol, memberSyntax.SyntaxTree, cancellationToken));
            }

            return new TypeInfo(
                typeSymbol.ContainingNamespace.ToDisplayString(FullyQualifiedDisplayFormat),
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

        public TypeDeclarationSyntax GetDeclarationSyntax()
        {
            return Kind switch
            {
                TypeKind.Struct => SyntaxFactory.StructDeclaration(Name),
                TypeKind.Class => SyntaxFactory.ClassDeclaration(Name),
                _ => throw new NotSupportedException()
            };
        }

        public MethodDeclarationSyntax GetMethodSyntax()
        {
            var methodDec = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), InterfaceReadMethod)
                .WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(SyntaxFactory.IdentifierName(TargetInterface)))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("reader")).WithType(SyntaxFactory.IdentifierName("global::System.IO.Endian.EndianReader")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("version")).WithType(SyntaxFactory.IdentifierName("double?")),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("origin")).WithType(SyntaxFactory.IdentifierName("long"))
                )
                .WithBody(SyntaxFactory.Block(EnumerateReadStatements().ToArray()));

            return methodDec;
        }

        public IEnumerable<StatementSyntax> EnumerateReadStatements()
        {
            var baseAddressToken = SyntaxFactory.Identifier("baseAddress");
            var baseAddressIdentifier = SyntaxFactory.IdentifierName("origin");

            var readerIdentifier = SyntaxFactory.IdentifierName("reader");
            var seekIdentifier = SyntaxFactory.IdentifierName("Seek");
            var seekOriginBeginExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("global::System.IO.SeekOrigin"),
                SyntaxFactory.IdentifierName("Begin"));

            var hasUnboundedMin = false;
            var hasUnboundedMax = false;
            var versionSet = new HashSet<double?>();

            var allVersionAttributes = FixedSizeAttributes.Cast<VersionedAttributeData>()
                .Concat(ByteOrderAttributes)
                .Concat(Properties.SelectMany(p => p.OffsetAttributes.Cast<VersionedAttributeData>().Concat(p.ByteOrderAttributes).Concat(p.StoreTypeAttributes)));

            foreach (var attr in allVersionAttributes)
            {
                if (attr.MinVersion.HasValue)
                    versionSet.Add(attr.MinVersion);
                else
                    hasUnboundedMin = true;

                if (attr.MaxVersion.HasValue)
                    versionSet.Add(attr.MaxVersion);
                else
                    hasUnboundedMax = true;
            }

            if (versionSet.Count == 0)
            {
                var body = BuildStatementsForVersion(null);
                foreach (var statement in body)
                    yield return statement;
                yield break;
            }

            var versionList = versionSet.ToList();
            versionList.Sort();

            if (hasUnboundedMin)
                versionList.Insert(0, null);
            if (hasUnboundedMax)
                versionList.Add(null);

            var rangeList = new (double? Min, double? Max, string Name)[versionList.Count -1];

            for (var i = 0; i < versionList.Count - 1; i++)
            {
                var (min, max) = (versionList[i], versionList[i + 1]);
                var minName = min.HasValue
                    ? "_GE" + min.Value.ToString().Replace('.', 'x')
                    : null;
                var maxName = max.HasValue
                    ? "_LT" + max.Value.ToString().Replace('.', 'x')
                    : null;
                rangeList[i] = (min, max, $"Read{minName}{maxName}");
            }

            var versionIdentifier = SyntaxFactory.IdentifierName("version");

            //TODO: validate that only one has version attribute and output diagnostic errors if not
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

            var elseClause = default(ElseClauseSyntax);
            foreach (var (min, max, name) in rangeList.Reverse())
            {
                var minCheck = min.HasValue
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, versionIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(min.Value)))
                    : null;

                var maxCheck = max.HasValue
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.LessThanExpression, versionIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(max.Value)))
                    : null;

                var combinedCheck = minCheck != null && maxCheck != null
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, minCheck, maxCheck)
                    : minCheck ?? maxCheck!;

                //else if ({VersionCheck})
                //    {ReadMethod}();
                elseClause = SyntaxFactory.ElseClause(
                    SyntaxFactory.IfStatement(
                        combinedCheck,
                        SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name))),
                        elseClause
                    )
                );
            }

            var messageStringSyntax = SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken)).AddContents(
                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, "Must provide a version when reading type \\\"", string.Empty, SyntaxFactory.TriviaList())),
                SyntaxFactory.Interpolation(
                    SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("nameof")).AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(Name))
                        )
                    ),
                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, "\\\"", string.Empty, SyntaxFactory.TriviaList()))
                );

            //if (version == null)
            //    throw new NotSupportedException("...");
            //[else if ...]
            yield return SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, versionIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("global::System.NotSupportedException")
                    ).AddArgumentListArguments(SyntaxFactory.Argument(
                        //SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal($"Must provide a version when reading type \"{Name}\""))))
                        messageStringSyntax))
                    ),
                elseClause
            );

            foreach (var (min, max, name) in rangeList)
            {
                var testValue = min ?? (max!.Value - 1);
                var body = BuildStatementsForVersion(testValue);

                //void {MethodName}()
                //{
                //    ...
                //}
                yield return SyntaxFactory.LocalFunctionStatement(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    name
                ).WithBody(SyntaxFactory.Block(body.ToArray()));
            }

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

                //always read in order of offset so the final position is at the end of the highest property
                var sorted = from p in Properties
                             let offsetAttribute = p.OffsetAttributes.FirstOrDefault(o => o.ValidForVersion(version))
                             where offsetAttribute != null
                             orderby offsetAttribute.Offset
                             select (p, offsetAttribute);

                long? currentOffset = null;
                foreach (var (property, offsetAttribute) in sorted)
                {
                    var readStatement = property.GetSetterStatementForVersion(version, (ByteOrder?)byteOrderAttribute?.ByteOrder);
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
    }
}
