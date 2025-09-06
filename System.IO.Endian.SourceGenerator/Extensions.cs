using Microsoft.CodeAnalysis;

namespace System.IO.Endian.SourceGenerator
{
    internal static class Extensions
    {
#if !NETSTANDARD2_1_OR_GREATER
        //netstandard2.1 has these built in but apparently source generators need to use netstandard2.0
        public static bool StartsWith(this ReadOnlySpan<char> span, string value) => span.StartsWith(value.AsSpan());
        public static bool SequenceEqual(this ReadOnlySpan<char> span, string value) => span.SequenceEqual(value.AsSpan());
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) => (key, value) = (pair.Key, pair.Value);
#endif

        private static readonly SymbolDisplayFormat FullyQualifiedLocalDisplayFormat
            = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

        private static readonly SymbolDisplayFormat FullyQualifiedFrameworkTypesDisplayFormat
            = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None);

        /// <summary>
        /// Uses <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/> to return the display string.
        /// <list type="bullet">
        /// <item>Includes the global namespace prefix, except for special system types</item>
        /// <item>Predefined system types will use the special type keywords like "int" instead of "System.Int32"</item>
        /// <item>Nullable types will use the ? shorthand syntax</item>
        /// </list>
        /// </summary>
        public static string ToFullyQualifiedGlobalDisplayString(this ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        /// <summary>
        /// Same as using <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>, except:
        /// <list type="bullet">
        /// <item>The global namespace prefix will not be included</item>
        /// </list>
        /// </summary>
        public static string ToFullyQualifiedLocalDisplayString(this ISymbol symbol) => symbol.ToDisplayString(FullyQualifiedLocalDisplayFormat);

        /// <summary>
        /// Same as using <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>, except:
        /// <list type="bullet">
        /// <item>The global namespace prefix will not be included</item>
        /// <item>Predefined system types will use the full frameowkr type names like "System.Int32" instead of "int"</item>
        /// </list>
        /// </summary>
        public static string ToFrameworkTypesDisplayString(this ISymbol symbol) => symbol.ToDisplayString(FullyQualifiedFrameworkTypesDisplayFormat);
    }
}