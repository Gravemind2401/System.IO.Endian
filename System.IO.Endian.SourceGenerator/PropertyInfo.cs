using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using System.Collections.Immutable;
using static System.IO.Endian.SourceGenerator.DiagnosticDescriptors;
using static System.IO.Endian.SourceGenerator.Globals;

namespace System.IO.Endian.SourceGenerator
{
    internal sealed record PropertyInfo(
        IPropertySymbol Symbol,
        ITypeSymbol? UnderlyingType,
        PropertyKind PropertyKind,
        int? PropertySize,
        bool IsVersionProperty,
        ImmutableEquatableArray<OffsetAttributeData> OffsetAttributes,
        ImmutableEquatableArray<ByteOrderAttributeData> ByteOrderAttributes,
        ImmutableEquatableArray<StoreTypeAttributeData> StoreTypeAttributes,
        StringAttributeInfo StringAttributes)
    {
        public static PropertyInfo FromSymbol(IPropertySymbol symbol, SyntaxTree syntaxTree, CancellationToken cancellationToken, ImmutableArray<DiagnosticInfo>.Builder diagnosticsBuilder)
        {
            var attributes = symbol.GetAttributes();

            cancellationToken.ThrowIfCancellationRequested();

            var offsetBuilder = ImmutableArray.CreateBuilder<OffsetAttributeData>();
            var byteOrderBuilder = ImmutableArray.CreateBuilder<ByteOrderAttributeData>();
            var storeTypeBuilder = ImmutableArray.CreateBuilder<StoreTypeAttributeData>();

            var hasVersionNumberAttribute = false;
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

                var attributeDisplayName = attribute.AttributeClass?.ToFullyQualifiedGlobalDisplayString();
                if (attributeDisplayName == null || !attributeDisplayName.AsSpan().StartsWith(HomeNamespaceGlobal))
                    continue;

                var attributeNameSpan = attributeDisplayName.AsSpan(HomeNamespaceGlobal.Length + 1);

                if (attributeNameSpan.SequenceEqual("OffsetAttribute"))
                {
                    var offset = (long)attribute.ConstructorArguments[0].Value!;
                    attribute.GetVersionArgs(out var minVersion, out var maxVersion);
                    offsetBuilder.Add(new OffsetAttributeData(offset, minVersion, maxVersion));
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("ByteOrderAttribute"))
                {
                    var order = (ByteOrder)(int)attribute.ConstructorArguments[0].Value!;
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

                if (attributeNameSpan.SequenceEqual("VersionNumberAttribute"))
                {
                    hasVersionNumberAttribute = true;
                    continue;
                }
            }

            //TODO: check for attribute version overlap issues here and output diagnostic errors
            //TODO: validate string properties here and output diagnostic error if invalid
            //enforce strings cannot have StoreTypeAttribute, StoreTypeAttribute cannot be string

            cancellationToken.ThrowIfCancellationRequested();

            ITypeSymbol? underlyingType;
            PropertyKind propertyKind;
            int? propertySize;

            if (storeTypeBuilder.Count == 0)
                propertyKind = GetPropertyKind(symbol.Type, out underlyingType, out propertySize);
            else if (storeTypeBuilder.Count == 1 && !storeTypeBuilder[0].IsVersioned)
                propertyKind = GetPropertyKind(storeTypeBuilder[0].StoreType, out underlyingType, out propertySize);
            else
                (underlyingType, propertyKind, propertySize) = (null, PropertyKind.Deferred, null);

            cancellationToken.ThrowIfCancellationRequested();

            if (hasVersionNumberAttribute)
            {
                if (offsetBuilder.Count == 0)
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        NoOffsetForVersionNumberMember,
                        symbol,
                        symbol.Name
                    ));
                }
                else if (offsetBuilder.Count > 1)
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        MultipleOffsetsForVersionNumberMember,
                        symbol,
                        symbol.Name
                    ));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (byteOrderBuilder.Count > 1)
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        MultipleByteOrdersForVersionNumberMember,
                        symbol,
                        symbol.Name
                    ));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (storeTypeBuilder.Count > 1)
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        MultipleStoreTypesForVersionNumberMember,
                        symbol,
                        symbol.Name
                    ));
                }

                var typeTest = storeTypeBuilder.Count == 1
                    ? storeTypeBuilder[0].StoreType
                    : symbol.Type;

                var typeName = typeTest.ToFullyQualifiedLocalDisplayString();
                if (typeName.TrimEnd('?') is not ("byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "Half" or "float" or "double" or "decimal"))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        NonNumericMemberWithVersionNumberAttribute,
                        symbol,
                        symbol.Name,
                        typeName
                    ));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (offsetBuilder.Any(x => x.IsVersioned) || byteOrderBuilder.Any(x => x.IsVersioned) || storeTypeBuilder.Any(x => x.IsVersioned))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        VersionedAttributesForVersionNumberMember,
                        symbol,
                        symbol.Name
                    ));
                }
            }

            return new PropertyInfo(
                symbol,
                underlyingType,
                propertyKind,
                propertySize,
                hasVersionNumberAttribute,
                offsetBuilder.ToImmutableEquatableArray(),
                byteOrderBuilder.ToImmutableEquatableArray(),
                storeTypeBuilder.ToImmutableEquatableArray(),
                new StringAttributeInfo(hasInternedAttribute, hasLengthPrefixedAttribute, nullTerminatedAttributeData, fixedLengthAttributeData));
        }

        private static PropertyKind GetPropertyKind(ITypeSymbol typeSymbol, out ITypeSymbol underlyingType, out int? propertySize)
        {
            underlyingType = typeSymbol;
            propertySize = null;

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
                {
                    propertySize = typeSymbol.Name switch
                    {
                        "SByte" or "Byte" => 1,
                        "Int16" or "UInt16" or "Half" => 2,
                        "Int32" or "UInt32" or "Single" => 4,
                        "Int64" or "UInt64" or "Double" => 8,
                        "Decimal" or "Guid" => 16,
                        _ => null
                    };

                    return PropertyKind.Primitive;
                }
            }

            if (typeSymbol.Interfaces.Any(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith(BufferableInterface)))
                return PropertyKind.Bufferable;

            return PropertyKind.Dynamic;
        }

        public StatementSyntax GetSetterStatementForVersion(double? version, ByteOrder? byteOrder)
        {
            var thisIdentifier = SyntaxFactory.IdentifierName("this");

            //result.{Property} = {readExpression}
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, thisIdentifier, SyntaxFactory.IdentifierName(Symbol.Name)),
                    GetReadExpressionForVersion(version, byteOrder)
                )
            );
        }

        public ExpressionSyntax GetReadExpressionForVersion(double? version, ByteOrder? byteOrder)
        {
            var readerIdentifier = SyntaxFactory.IdentifierName("reader");

            var byteOrderAttribute = ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (byteOrderAttribute != null)
                byteOrder = byteOrderAttribute.ByteOrder;

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
                propertyKind = GetPropertyKind(storeType, out storeType, out _);
            }

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
                //since nullables allow implicit casts from non-nullable values, we dont need to do anything about the type difference.
                var typeName = storeType.ToFrameworkTypesDisplayString().TrimEnd('?');
                readMethodName = "Read" + typeName.Substring("System.".Length);

                readArgs = byteOrder.HasValue ? new ArgumentSyntax[1] : Array.Empty<ArgumentSyntax>();
                if (byteOrder.HasValue && typeName is not ("System.SByte" or "System.Byte"))
                    readArgs[0] = byteOrderArgument!;
            }
            else
            {
                //since nullables allow implicit casts from non-nullable values, we dont need to do anything about the type difference.
                var typeName = storeType.ToFullyQualifiedGlobalDisplayString().TrimEnd('?');

                if (propertyKind == PropertyKind.Bufferable)
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
            }

            //reader.{ReadMethod}({args})
            ExpressionSyntax readExpression = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    readerIdentifier,
                    SyntaxFactory.IdentifierName(readMethodName)
                )).AddArgumentListArguments(readArgs);

            var propertyType = Symbol.Type;
            if (propertyType.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System" && propertyType.Name == "Nullable")
                propertyType = ((INamedTypeSymbol)propertyType).TypeArguments[0];

            //({PropertyType}){readExpression}
            if (!SymbolEqualityComparer.Default.Equals(storeType, propertyType))
                readExpression = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(propertyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), readExpression);

            return readExpression;
        }
    }
}
