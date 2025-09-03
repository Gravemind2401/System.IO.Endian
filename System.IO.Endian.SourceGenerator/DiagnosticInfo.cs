using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;

namespace System.IO.Endian.SourceGenerator
{
    internal sealed record DiagnosticInfo(
        DiagnosticDescriptor Descriptor,
        SyntaxTree? SyntaxTree,
        TextSpan TextSpan,
        ImmutableEquatableArray<string> Arguments)
    {
        public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
        {
            var location = symbol.Locations.First();
            return new(descriptor, location.SourceTree, location.SourceSpan, args.Select(static arg => arg.ToString()).ToImmutableEquatableArray());
        }

        public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] args)
        {
            var location = node.GetLocation();
            return new(descriptor, location.SourceTree, location.SourceSpan, args.Select(static arg => arg.ToString()).ToImmutableEquatableArray());
        }

        public Diagnostic ToDiagnostic()
        {
            return SyntaxTree == null
                ? Diagnostic.Create(Descriptor, null, Arguments.ToArray())
                : Diagnostic.Create(Descriptor, Location.Create(SyntaxTree, TextSpan), Arguments.ToArray());
        }
    }
}
