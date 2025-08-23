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

                if (attributeNameSpan.SequenceEqual("InternedAttribute"))
                {
                    hasInternedAttribute = true;
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("LengthPrefixedAttribute"))
                {
                    hasLengthPrefixedAttribute = true;
                    continue;
                }
            }

            return new PropertyInfo(
                symbol,
                offsetBuilder.ToImmutableEquatableArray(),
                byteOrderBuilder.ToImmutableEquatableArray(),
                storeTypeBuilder.ToImmutableEquatableArray(),
                new StringAttributeInfo(hasInternedAttribute, hasLengthPrefixedAttribute, nullTerminatedAttributeData, fixedLengthAttributeData));
        }

        public void AddStatementsForVersion(ImmutableArray<StatementSyntax>.Builder statementBuilder, double? version, ByteOrder? byteOrder)
        {
            var offsetAttribute = OffsetAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (offsetAttribute == null)
                return;

            var thisIdentifier = SyntaxFactory.IdentifierName("this");
            var baseAddressIdentifier = SyntaxFactory.IdentifierName("baseAddress");
            var readerIdentifier = SyntaxFactory.IdentifierName("reader");
            var seekIdentifier = SyntaxFactory.IdentifierName("Seek");
            var seekOriginBeginExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("global::System.IO.SeekOrigin"),
                SyntaxFactory.IdentifierName("Begin"));

            var byteOrderAttribute = ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (byteOrderAttribute != null)
                byteOrder = (ByteOrder)byteOrderAttribute.ByteOrder;

            var storeTypeAttribute = StoreTypeAttributes.FirstOrDefault(o => o.ValidForVersion(version));

            //reader.Seek(baseAddress + {Offset}L, SeekOrigin.Begin);
            statementBuilder.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    readerIdentifier,
                    seekIdentifier
                )).AddArgumentListArguments(SyntaxFactory.Argument(
                    SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, baseAddressIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(offsetAttribute.Offset)))
                ), SyntaxFactory.Argument(seekOriginBeginExpression)
            )));

            var storeType = Symbol.Type;
            var propertyType = storeType;

            if (storeTypeAttribute != null)
                storeType = storeTypeAttribute.StoreType;

            if (storeType.TypeKind == TypeKind.Enum && storeType is INamedTypeSymbol namedTypeSymbol)
                storeType = namedTypeSymbol.EnumUnderlyingType!;

            //since FullyQualifiedDisplayFormat doesnt expand nullables to Nullable<T>, it will just end with a ? instead.
            //since nullables allow implicit casts from non-nullable values, we dont need to do anything about the type difference.
            var typeName = storeType.ToDisplayString(FullyQualifiedDisplayFormat).TrimEnd('?');

            string readMethodName;
            ArgumentSyntax[] readArgs;

            //TODO: output as diagnostics
            if (typeName == "System.String" && !StringAttributes.IsValid)
                throw new InvalidOperationException("String property has invalid string attributes!");

            if (typeName == "System.String" && !StringAttributes.IsLengthPrefixed)
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
            else if (typeName is "System.SByte" or "System.Int16" or "System.Int32" or "System.Int64"
                or "System.Byte" or "System.UInt16" or "System.UInt32" or "System.UInt64"
                or "System.Half" or "System.Single" or "System.Double" or "System.Decimal"
                or "System.Guid" or "System.String")
            {
                readMethodName = "Read" + typeName.Substring("System.".Length);

                readArgs = byteOrder.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                if (byteOrder.HasValue && typeName is not ("System.SByte" or "System.Byte"))
                {
                    readArgs[0] = SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("System.IO.Endian.ByteOrder"),
                        SyntaxFactory.IdentifierName(byteOrder.Value.ToString())
                    ));
                }
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

            //({PropertyType}){readExpression}
            if (!SymbolEqualityComparer.Default.Equals(storeType, propertyType))
                readExpression = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(propertyType.ToDisplayString(FullyQualifiedDisplayFormat)), readExpression);

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
