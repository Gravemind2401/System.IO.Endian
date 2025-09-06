using Microsoft.CodeAnalysis;

namespace System.IO.Endian.SourceGenerator
{
    internal static class Globals
    {
        public const string HomeNamespace = "System.IO.Endian";
        public const string HomeNamespaceGlobal = "global::System.IO.Endian";
        public const string MarkerAttribute = $"{HomeNamespace}.StreamableObjectAttribute";
        public const string TargetInterface = $"{HomeNamespaceGlobal}.IStreamableObject";
        public const string BufferableInterface = $"{HomeNamespaceGlobal}.IBufferable";
        public const string InterfaceReadMethod = "PopulateFromStream";
    }
}
