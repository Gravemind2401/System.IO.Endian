#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable RS2008 // Enable analyzer release tracking
#pragma warning restore IDE0079 // Remove unnecessary suppression
using Microsoft.CodeAnalysis;

namespace System.IO.Endian.SourceGenerator
{
    internal static class DiagnosticDescriptors
    {
        private const string MarkerAttribute = "[StreamableObject]";
        private const string GeneratedInterface = "IStreamableObject";
        private const string VersionNumberAttribute = "[VersionNumber]";
        private const string OffsetAttribute = "[Offset]";
        private const string ByteOrderAttribute = "[ByteOrder]";
        private const string StoreTypeAttribute = "[StoreType]";

        public static readonly DiagnosticDescriptor DuplicateInterfaceForStreamableObjectAttribute = new(
            id: "SIOE0001",
            title: $"Duplicate {GeneratedInterface} definition",
            messageFormat: $"Cannot apply {MarkerAttribute} to type {{0}}, as it already declares the {GeneratedInterface} interface",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Cannot apply {MarkerAttribute} to a type that already declares the {GeneratedInterface} interface.");

        public static readonly DiagnosticDescriptor MultipleMembersWithVersionNumberAttribute = new(
            id: "SIOE0002",
            title: $"Multiple {VersionNumberAttribute} definitions",
            messageFormat: $"Cannot apply {VersionNumberAttribute} to more than one member in type {{0}}",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"{VersionNumberAttribute} can only be declared on one member per type.");

        public static readonly DiagnosticDescriptor NonNumericMemberWithVersionNumberAttribute = new(
            id: "SIOE0003",
            title: $"{VersionNumberAttribute} applied to non-numeric property",
            messageFormat: $"Cannot apply {VersionNumberAttribute} to property {{0}} because the type {{1}} cannot be used as a version number",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"{VersionNumberAttribute} can only be used with built-in numeric types.");

        public static readonly DiagnosticDescriptor NoOffsetForVersionNumberMember = new(
            id: "SIOE0004",
            title: $"No offset provided for {VersionNumberAttribute} property",
            messageFormat: $"The property {{0}} must declare declare {OffsetAttribute} because it has {VersionNumberAttribute} applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Properties with {VersionNumberAttribute} applied must also have {OffsetAttribute} applied.");

        public static readonly DiagnosticDescriptor MultipleOffsetsForVersionNumberMember = new(
            id: "SIOE0005",
            title: $"Multiple {OffsetAttribute} declarations provided for {VersionNumberAttribute} property",
            messageFormat: $"The property {{0}} cannot have more than one {OffsetAttribute} applied because it has {VersionNumberAttribute} applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Properties with {VersionNumberAttribute} applied cannot have {OffsetAttribute} applied more than once.");

        public static readonly DiagnosticDescriptor MultipleByteOrdersForVersionNumberMember = new(
            id: "SIOE0006",
            title: $"Multiple {ByteOrderAttribute} declarations provided for {VersionNumberAttribute} property",
            messageFormat: $"The property {{0}} cannot have more than one {ByteOrderAttribute} applied because it has {VersionNumberAttribute} applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Properties with {VersionNumberAttribute} applied cannot have {ByteOrderAttribute} applied more than once.");

        public static readonly DiagnosticDescriptor MultipleStoreTypesForVersionNumberMember = new(
            id: "SIOE0007",
            title: $"Multiple {StoreTypeAttribute} declarations provided for {VersionNumberAttribute} property",
            messageFormat: $"The property {{0}} cannot have more than one {StoreTypeAttribute} applied because it has {VersionNumberAttribute} applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Properties with {VersionNumberAttribute} applied cannot have {StoreTypeAttribute} applied more than once.");

        public static readonly DiagnosticDescriptor VersionedAttributesForVersionNumberMember = new(
            id: "SIOE0008",
            title: $"Versioned attribute declarations provided for {VersionNumberAttribute} property",
            messageFormat: $"The property {{0}} cannot specify min or max version for {OffsetAttribute}, {ByteOrderAttribute} or {StoreTypeAttribute} because it has {VersionNumberAttribute} applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Properties with {VersionNumberAttribute} applied cannot specify MinVersion or MaxVersion when applying {OffsetAttribute}, {ByteOrderAttribute} and {StoreTypeAttribute}.");

        public static readonly DiagnosticDescriptor StoreTypeOnStringMember = new(
            id: "SIOE0009",
            title: $"{StoreTypeAttribute} attribute used on a string member",
            messageFormat: $"String properties cannot have the {StoreTypeAttribute} attribute applied",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"The {StoreTypeAttribute} attribute cannot be used on string members.");

        public static readonly DiagnosticDescriptor StoreTypeIsString = new(
            id: "SIOE0010",
            title: $"System.String used as parameter for {StoreTypeAttribute} attribute",
            messageFormat: $"The type parameter for the {StoreTypeAttribute} attribute cannot be typeof(string)",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"The type System.String cannot be used as the parameter for {StoreTypeAttribute}.");

        public static readonly DiagnosticDescriptor AmbiguousStringStorageMode = new(
            id: "SIOE0011",
            title: $"Ambiguous string member storage mode",
            messageFormat: $"String members must apply either [FixedLength], [NullTerminated] or [LengthPrefixed] attribute",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"String members must apply either [FixedLength], [NullTerminated] or [LengthPrefixed] attribute.");

        public static readonly DiagnosticDescriptor DuplicateStringStorageMode = new(
            id: "SIOE0012",
            title: $"Duplicate string member storage mode attributes",
            messageFormat: $"String members cannot have {{0}} and {{1}} attributes applied at the same time",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"String members can only have one of [FixedLength], [NullTerminated] or [LengthPrefixed].");
    }
}
