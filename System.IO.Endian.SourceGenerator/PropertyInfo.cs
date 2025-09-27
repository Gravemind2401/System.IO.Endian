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
                    //TODO: use string.Intern() when reading string properties
                    hasInternedAttribute = true;
                    continue;
                }

                if (attributeNameSpan.SequenceEqual("VersionNumberAttribute"))
                {
                    hasVersionNumberAttribute = true;
                    continue;
                }
            }

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

            ValidateVersionProperty();
            ValidateStringProperty();
            ValidateAttributes();

            cancellationToken.ThrowIfCancellationRequested();

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

            void ValidateVersionProperty()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!hasVersionNumberAttribute)
                    return;

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

            void ValidateStringProperty()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (symbol.Type.ToFullyQualifiedGlobalDisplayString() != "string")
                    return;

                if (storeTypeBuilder.Count > 0)
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        StoreTypeOnStringMember,
                        symbol
                    ));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (offsetBuilder.Count > 0)
                {
                    var attrCount = Convert.ToInt32(hasLengthPrefixedAttribute) + Convert.ToInt32(nullTerminatedAttributeData != null) + Convert.ToInt32(fixedLengthAttributeData != null);
                    if (attrCount == 0)
                    {
                        diagnosticsBuilder.Add(DiagnosticInfo.Create(
                            AmbiguousStringStorageMode,
                            symbol
                        ));
                    }
                    else if (attrCount > 1)
                    {
                        var prefixedName = hasLengthPrefixedAttribute
                            ? "[LengthPrefixed]"
                            : null;

                        var nullTerminatedName = nullTerminatedAttributeData != null
                            ? "[NullTerminated]"
                            : null;

                        var fixedLengthName = fixedLengthAttributeData != null
                            ? "[FixedLength]"
                            : null;

                        diagnosticsBuilder.Add(DiagnosticInfo.Create(
                            DuplicateStringStorageMode,
                            symbol,
                            prefixedName ?? nullTerminatedName!,
                            fixedLengthName ?? nullTerminatedName!
                        ));
                    }
                }
            }

            void ValidateAttributes()
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (storeTypeBuilder.Any(x => x.StoreType.ToFullyQualifiedGlobalDisplayString() == "string"))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        StoreTypeIsString,
                        symbol
                    ));
                }

                if (CheckForOverlap(offsetBuilder))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        AttributeVersionOverlap,
                        symbol,
                        "Offset",
                        symbol.ContainingType.Name,
                        symbol.Name
                    ));
                }

                if (CheckForOverlap(byteOrderBuilder))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        AttributeVersionOverlap,
                        symbol,
                        "ByteOrder",
                        symbol.ContainingType.Name,
                        symbol.Name
                    ));
                }

                if (CheckForOverlap(storeTypeBuilder))
                {
                    diagnosticsBuilder.Add(DiagnosticInfo.Create(
                        AttributeVersionOverlap,
                        symbol,
                        "StoreType",
                        symbol.ContainingType.Name,
                        symbol.Name
                    ));
                }
            }

            bool CheckForOverlap<T>(IList<T> attributes) where T : VersionedAttributeData
            {
                if (attributes.Count < 2)
                    return false;

                for (var i = 0; i < attributes.Count - 1; i++)
                {
                    if (!attributes[i].IsVersioned)
                        continue;

                    for (var j = i + 1; j < attributes.Count; j++)
                    {
                        if (!attributes[j].IsVersioned)
                            continue;

                        var (a, b) = (attributes[i], attributes[j]);
                        if (a.MinVersion < b.MaxVersion
                            || b.MinVersion < a.MaxVersion
                            || (a.MinVersion == null && b.MinVersion == null)
                            || (a.MaxVersion == null && b.MaxVersion == null))
                            return true;
                    }
                }

                return false;
            }
        }

        private static PropertyKind GetPropertyKind(ITypeSymbol typeSymbol, out ITypeSymbol underlyingType, out int? propertySize)
        {
            underlyingType = typeSymbol;
            propertySize = null;

            if (typeSymbol.TypeKind == TypeKind.Enum)
                underlyingType = typeSymbol = ((INamedTypeSymbol)typeSymbol).EnumUnderlyingType!;

            var ns = typeSymbol.ContainingNamespace?.ToFullyQualifiedGlobalDisplayString();
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

            return typeSymbol.Interfaces.Any(t => t.ToFullyQualifiedGlobalDisplayString().StartsWith(BufferableInterface))
                ? PropertyKind.Bufferable
                : PropertyKind.Dynamic;
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
            ExpressionSyntax? readExpression = default;

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
                    var versionParam = SyntaxFactory.IdentifierName("version");
                    readArgs = [SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, versionParam, SyntaxFactory.IdentifierName("Value")))];

                    var readMethodCall = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        readerIdentifier,
                        SyntaxFactory.IdentifierName(readMethodName)
                    ));

                    //version.HasValue ? ReadObject<T>(version) : ReadObject<T>();
                    readExpression = SyntaxFactory.ConditionalExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            versionParam,
                            SyntaxFactory.IdentifierName("HasValue")
                        ),
                        readMethodCall.AddArgumentListArguments(readArgs),
                        readMethodCall
                    );
                }
            }

            //reader.{ReadMethod}({args})
            readExpression ??= SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                readerIdentifier,
                SyntaxFactory.IdentifierName(readMethodName)
            )).AddArgumentListArguments(readArgs);

            var propertyType = Symbol.Type;
            if (propertyType.IsNullableStruct())
                propertyType = ((INamedTypeSymbol)propertyType).TypeArguments[0];

            //({PropertyType}){readExpression}
            if (!SymbolEqualityComparer.Default.Equals(storeType, propertyType))
            {
                //put the "condition ? true : false" in parentheses so the cast happens on the result instead of the condition
                if (readExpression is ConditionalExpressionSyntax)
                    readExpression = SyntaxFactory.ParenthesizedExpression(readExpression);
                readExpression = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(propertyType.ToFullyQualifiedGlobalDisplayString()), readExpression);
            }

            return readExpression;
        }

        public StatementSyntax GetWriteStatementForVersion(double? version, ByteOrder? byteOrder)
        {
            var writerIdentifier = SyntaxFactory.IdentifierName("writer");

            var byteOrderAttribute = ByteOrderAttributes.FirstOrDefault(o => o.ValidForVersion(version));
            if (byteOrderAttribute != null)
                byteOrder = byteOrderAttribute.ByteOrder;

            var propertyType = Symbol.Type;
            var isNullable = IsVersionProperty;
            if (propertyType.IsNullableStruct())
            {
                propertyType = ((INamedTypeSymbol)propertyType).TypeArguments[0];
                isNullable = true;
            }

            ExpressionSyntax valueExpresssion;
            if (IsVersionProperty)
            {
                //when writing the version property, write the value of the version parameter instead of the actual property value

                //version
                valueExpresssion = SyntaxFactory.IdentifierName("version");

                //({propertyType})version
                if (Symbol.Type.ToFullyQualifiedGlobalDisplayString() is not ("double" or "double?"))
                    valueExpresssion = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(propertyType.ToFullyQualifiedGlobalDisplayString()), valueExpresssion);
            }
            else
            {
                //this.{Property}
                valueExpresssion = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("this"), SyntaxFactory.IdentifierName(Symbol.Name));
            }

            var (storeType, propertyKind) = (UnderlyingType, PropertyKind);
            if (storeType == null)
            {
                var storeTypeAttribute = StoreTypeAttributes.FirstOrDefault(o => o.ValidForVersion(version));
                storeType = storeTypeAttribute?.StoreType ?? Symbol.Type;
                propertyKind = GetPropertyKind(storeType, out storeType, out _);
            }

            if (isNullable)
            {
                //{valueExpression}.GetValueOrDefault()
                valueExpresssion = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    valueExpresssion,
                    SyntaxFactory.IdentifierName("GetValueOrDefault")
                ));
            }

            //({storeType}){valueExpression}
            if (!SymbolEqualityComparer.Default.Equals(storeType, propertyType))
                valueExpresssion = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(storeType.ToFullyQualifiedGlobalDisplayString()), valueExpresssion);

            var valueArgument = SyntaxFactory.Argument(valueExpresssion);

            var byteOrderArgument = byteOrder.HasValue
                ? SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("System.IO.Endian.ByteOrder"),
                        SyntaxFactory.IdentifierName(byteOrder.Value.ToString())
                    ))
                : null;

            string writeMethodName;
            ArgumentSyntax[] writeArgs;

            if (propertyKind == PropertyKind.String && !StringAttributes.IsLengthPrefixed)
            {
                if (StringAttributes.FixedLengthAttributeData != null)
                {
                    writeMethodName = "WriteStringFixedLength";
                    writeArgs = new ArgumentSyntax[3];

                    writeArgs[0] = valueArgument;

                    writeArgs[1] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal(StringAttributes.FixedLengthAttributeData.Length)
                    ));

                    if (StringAttributes.FixedLengthAttributeData.Padding != ' ')
                    {
                        writeArgs[2] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            SyntaxKind.CharacterLiteralExpression,
                            SyntaxFactory.Literal(StringAttributes.FixedLengthAttributeData.Padding)
                        ));
                    }
                }
                else
                {
                    writeMethodName = "WriteStringNullTerminated";
                    var length = StringAttributes.NullTerminatedAttributeData!.Length;
                    writeArgs = new ArgumentSyntax[length.HasValue ? 2 : 1];
                    writeArgs[0] = valueArgument;
                    if (length.HasValue)
                    {
                        writeArgs[1] = SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            SyntaxFactory.Literal(length.Value)
                        ));
                    }
                }
            }
            else if (propertyKind is PropertyKind.Primitive or PropertyKind.String)
            {
                var typeName = storeType.ToFrameworkTypesDisplayString().TrimEnd('?');

                writeMethodName = "Write";
                writeArgs = new ArgumentSyntax[byteOrder.HasValue ? 2 : 1];
                writeArgs[0] = valueArgument;
                if (byteOrder.HasValue && typeName is not ("System.SByte" or "System.Byte"))
                    writeArgs[1] = byteOrderArgument!;
            }
            else
            {
                var typeName = storeType.ToFullyQualifiedGlobalDisplayString().TrimEnd('?');
                if (propertyKind == PropertyKind.Bufferable)
                {
                    writeMethodName = $"WriteBufferable<{typeName}>";
                    writeArgs = new ArgumentSyntax[byteOrder.HasValue ? 2 : 1];
                    writeArgs[0] = valueArgument;
                    if (byteOrder.HasValue)
                        writeArgs[1] = byteOrderArgument!;
                }
                else
                {
                    writeMethodName = $"WriteObject<{typeName}>";
                    var versionParam = SyntaxFactory.IdentifierName("version");

                    var writeMethodCall = SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        writerIdentifier,
                        SyntaxFactory.IdentifierName(writeMethodName)
                    ));

                    //if (version.HasValue) { WriteObject<T>(obj, version); } else { WriteObject<T>(obj); }
                    return SyntaxFactory.IfStatement(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            versionParam,
                            SyntaxFactory.IdentifierName("HasValue")
                        ),
                        SyntaxFactory.ExpressionStatement(
                            writeMethodCall.AddArgumentListArguments(valueArgument, SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, versionParam, SyntaxFactory.IdentifierName("Value"))))
                        ),
                        SyntaxFactory.ElseClause(SyntaxFactory.ExpressionStatement(
                            writeMethodCall.AddArgumentListArguments(valueArgument)
                        ))
                    );
                }
            }

            //writer.{WriteMethod}({args})
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    writerIdentifier,
                    SyntaxFactory.IdentifierName(writeMethodName)
                )).AddArgumentListArguments(writeArgs)
            );
        }
    }
}
