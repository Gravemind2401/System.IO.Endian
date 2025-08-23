using Microsoft.CodeAnalysis;

namespace System.IO.Endian.SourceGenerator
{
    internal static class Globals
    {
        public const string HomeNamespace = "System.IO.Endian";
        public const string MarkerAttribute = $"{HomeNamespace}.StreamableObjectAttribute";
        public const string TargetInterface = $"global::{HomeNamespace}.IStreamableObject";
        public const string InterfaceReadMethod = "PopulateFromStream";

        public static readonly SymbolDisplayFormat FullyQualifiedDisplayFormat
            = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None);
    }
}
