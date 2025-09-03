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

        public static readonly DiagnosticDescriptor DuplicateInterfaceForStreamableObjectAttribute = new(
            id: "SIOE0001",
            title: $"Duplicate {GeneratedInterface} definition",
            messageFormat: $"Cannot apply {MarkerAttribute} to type {{0}}, as it already declares the {GeneratedInterface} interface",
            category: typeof(SourceGenerator).FullName,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"Cannot apply {MarkerAttribute} to a type that already declares the {GeneratedInterface} interface.");
    }
}
