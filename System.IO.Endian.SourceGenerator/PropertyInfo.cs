using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using System.Collections.Immutable;
using static System.IO.Endian.SourceGenerator.Globals;

namespace System.IO.Endian.SourceGenerator
{
    internal sealed record PropertyInfo(
        IPropertySymbol Symbol,
        ITypeSymbol? UnderlyingType,
        PropertyKind PropertyKind,
        ImmutableEquatableArray<OffsetAttributeData> OffsetAttributes,
        ImmutableEquatableArray<ByteOrderAttributeData> ByteOrderAttributes,
        ImmutableEquatableArray<StoreTypeAttributeData> StoreTypeAttributes,
        StringAttributeInfo StringAttributes)
    {
        public static PropertyInfo FromSymbol(IPropertySymbol symbol, SyntaxTree syntaxTree, CancellationToken cancellationToken)
        {
            var attributes = symbol.GetAttributes();

            var offsetBuilder = ImmutableArray.CreateBuilder<OffsetAttributeData>();
            var byteOrderBuilder = ImmutableArray.CreateBuilder<ByteOrderAttributeData>();
            var storeTypeBuilder = ImmutableArray.CreateBuilder<StoreTypeAttributeData>();

            var hasInternedAttribute = false;
            var hasLengthPrefixedAttribute = false;

            var nullTerminatedAttributeData = default(NullTerminatedAttributeData);
            var fixedLengthAttributeData = default(FixedLengthAttributeData);

            foreach (var attribute in attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                //IncrementalValuesProvider<T>.ForAttributeWithMetadataName<T> does this check when finding matching attributes, so it must be important
                if (attribute.ApplicationSyntaxReference?.SyntaxTree != syntaxTree)
                    continue;

                var attributeDisplayName = attribute.AttributeClass?.ToDisplayString(FullyQualifiedDisplayFormat);
                if (attributeDisplayName == null || !attributeDisplayName.AsSpan().StartsWith(HomeNamespace))
                    continue;

                var attributeNameSpan = attributeDisplayName.AsSpan(HomeNamespace.Length + 1);

                if (attributeNameSpan.SequenceEqual("OffsetAttribute"))
                {
                    var offset = (long)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    offsetBuilder.Add(new OffsetAttributeData(offset, minVersion, maxVersion));
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("ByteOrderAttribute"))
                {
                    var order = (int)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    byteOrderBuilder.Add(new ByteOrderAttributeData(order, minVersion, maxVersion));
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("StoreTypeAttribute"))
                {
                    var storeType = (ITypeSymbol)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    storeTypeBuilder.Add(new StoreTypeAttributeData(storeType, minVersion, maxVersion));
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("FixedLengthAttribute"))
                {
                    var length = (int)attribute.ConstructorArguments[0].Value!;
                    var trim = false;
                    var padding = ' ';

                    foreach (var (name, value) in attribute.NamedArguments)
                    {
                        if (name == nameof(FixedLengthAttributeData.Trim))
                            trim = (bool)value.Value!;
                        else if (name == nameof(FixedLengthAttributeData.Padding))
                            padding = (char)value.Value!;
                    }

                    fixedLengthAttributeData = new(length, trim, padding);
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("NullTerminatedAttribute"))
                {
                    var maxLength = default(int?);

                    foreach (var (name, value) in attribute.NamedArguments)
                    {
                        if (name == nameof(NullTerminatedAttributeData.Length))
                        {
                            maxLength = (int)value.Value!;
                            break;
                        }
                    }

                    nullTerminatedAttributeData = new(maxLength);
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("LengthPrefixedAttribute"))
                {
                    hasLengthPrefixedAttribute = true;
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("InternedAttribute"))
                {
                    hasInternedAttribute = true;
                    continue;
                }
            }

            ITypeSymbol? underlyingType;
            PropertyKind propertyKind;

            //TODO: if only one storetype and its unversioned then it can be non deferred

            if (storeTypeBuilder.Count > 0)
                (underlyingType, propertyKind) = (null, PropertyKind.Deferred);
            else
                propertyKind = GetPropertyKind(symbol.Type, out underlyingType);

            //TODO: validate string properties here and output diagnostic error if invalid

            return new PropertyInfo(
                symbol,
                underlyingType,
                propertyKind,
                offsetBuilder.ToImmutableEquatableArray(),
                byteOrderBuilder.ToImmutableEquatableArray(),
                storeTypeBuilder.ToImmutableEquatableArray(),
                new StringAttributeInfo(hasInternedAttribute, hasLengthPrefixedAttribute, nullTerminatedAttributeData, fixedLengthAttributeData));
        }

        private static PropertyKind GetPropertyKind(ITypeSymbol typeSymbol, out ITypeSymbol underlyingType)
        {
            underlyingType = typeSymbol;

            if (typeSymbol.TypeKind == TypeKind.Enum)
                underlyingType = typeSymbol = ((INamedTypeSymbol)typeSymbol).EnumUnderlyingType!;

            var ns = typeSymbol.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (ns == "global::System")
            {
                if (typeSymbol.Name == "String")
                    return PropertyKind.String;

                if (typeSymbol.Name == "Nullable")
                {
                    underlyingType = typeSymbol = ((INamedTypeSymbol)typeSymbol).TypeArguments[0];

                    //nullable enum types wont enter the first enum check above
                    if (typeSymbol.TypeKind == TypeKind.Enum)
                        underlyingType = typeSymbol = ((INamedTypeSymbol)typeSymbol).EnumUnderlyingType!;
                }

                if (typeSymbol.Name is "SByte" or "Int16" or "Int32" or "Int64"
                    or "Byte" or "UInt16" or "UInt32" or "UInt64"
                    or "Half" or "Single" or "Double" or "Decimal"
                    or "Guid")
                    return PropertyKind.Primitive;
            }

            if (typeSymbol.Interfaces.Any(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith(BufferableInterface)))
                return PropertyKind.Bufferable;

            return PropertyKind.Dynamic;
        }

        public void AddStatementsForVersion(ImmutableArray<StatementSyntax>.Builder statementBuilder, double? version, ByteOrder? byteOrder)
        {
            var offsetAttribute = OffsetAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (offsetAttribute == null)
                return;

            var thisIdentifier = SyntaxFactory.IdentifierName("this");
            var baseAddressIdentifier = SyntaxFactory.IdentifierName("origin");
            var readerIdentifier = SyntaxFactory.IdentifierName("reader");
            var seekIdentifier = SyntaxFactory.IdentifierName("Seek");
            var seekOriginBeginExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("global::System.IO.SeekOrigin"),
                SyntaxFactory.IdentifierName("Begin"));

            //reader.Seek(baseAddress + {Offset}L, SeekOrigin.Begin);
            statementBuilder.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    readerIdentifier,
                    seekIdentifier
                )).AddArgumentListArguments(SyntaxFactory.Argument(
                    SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, baseAddressIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(offsetAttribute.Offset)))
                ), SyntaxFactory.Argument(seekOriginBeginExpression)
            )).WithLeadingTrivia(SyntaxFactory.TriviaList(
                SyntaxFactory.Comment($"//0x{offsetAttribute.Offset:X2}")
            )));

            var byteOrderAttribute = ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (byteOrderAttribute != null)
                byteOrder = (ByteOrder)byteOrderAttribute.ByteOrder;

            var byteOrderArgument = byteOrder.HasValue
                ? SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("System.IO.Endian.ByteOrder"),
                        SyntaxFactory.IdentifierName(byteOrder.Value.ToString())
                    ))
                : null;

            var (storeType, propertyKind) = (UnderlyingType, PropertyKind);
            if (storeType == null)
            {
                var storeTypeAttribute = StoreTypeAttributes.FirstOrDefault(o => o.ValidForVersion(version));
                storeType = storeTypeAttribute?.StoreType ?? Symbol.Type;

                propertyKind = GetPropertyKind(storeType, out storeType);
            }

            //since FullyQualifiedDisplayFormat doesnt expand nullables to Nullable<T>, it will just end with a ? instead.
            //since nullables allow implicit casts from non-nullable values, we dont need to do anything about the type difference.
            var typeName = storeType.ToDisplayString(FullyQualifiedDisplayFormat).TrimEnd('?');

            string readMethodName;
            ArgumentSyntax[] readArgs;

            if (propertyKind == PropertyKind.String && !StringAttributes.IsLengthPrefixed)
            {
                if (StringAttributes.FixedLengthAttributeData != null)
                {
                    readMethodName = "ReadString";
                    readArgs = new ArgumentSyntax[2];

                    readArgs[0] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal(StringAttributes.FixedLengthAttributeData.Length)
                    ));

                    readArgs[1] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        StringAttributes.FixedLengthAttributeData.Trim ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression
                    ));
                }
                else
                {
                    readMethodName = "ReadNullTerminatedString";
                    var length = StringAttributes.NullTerminatedAttributeData!.Length;
                    readArgs = length.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                    if (length.HasValue)
                    {
                        readArgs[0] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(length.Value)
                        ));
                    }
                }
            }
            else if (propertyKind is PropertyKind.Primitive or PropertyKind.String)
            {
                readMethodName = "Read" + typeName.Substring("System.".Length);

                readArgs = byteOrder.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                if (byteOrder.HasValue && typeName is not ("System.SByte" or "System.Byte"))
                    readArgs[0] = byteOrderArgument!;
            }
            else if (propertyKind == PropertyKind.Bufferable)
            {
                readMethodName = $"ReadBufferable<{typeName}>";
                readArgs = byteOrder.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                if (byteOrder.HasValue)
                    readArgs[0] = byteOrderArgument!;
            }
            else
            {
                readMethodName = $"ReadObject<{typeName}>";
                readArgs = version.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                if (version.HasValue)
                {
                    readArgs[0] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal(version.Value)
                    ));
                }
            }

            //reader.{ReadMethod}({args})
            ExpressionSyntax readExpression = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    readerIdentifier,
                    SyntaxFactory.IdentifierName(readMethodName)
                )).AddArgumentListArguments(readArgs);

            //TODO: no need to cast to nullable

            //({PropertyType}){readExpression}
            if (!SymbolEqualityComparer.Default.Equals(storeType, Symbol.Type))
                readExpression = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(Symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), readExpression);

            //result.{Property} = {readExpression}
            statementBuilder.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, thisIdentifier, SyntaxFactory.IdentifierName(Symbol.Name)),
                    readExpression
                )
            ));
        }
    }
}
