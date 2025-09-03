using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using System.Text;
using static System.IO.Endian.SourceGenerator.Globals;

namespace System.IO.Endian.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class SourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var diagnosticsProvider = context.SyntaxProvider.ForAttributeWithMetadataName(MarkerAttribute, IsSyntaxTargetForGeneration, GetSemanticTargetForGeneration);
            context.RegisterSourceOutput(diagnosticsProvider.Select(static (item, _) => item.Diagnostics), Report);

            var infoProvider = diagnosticsProvider
                .Where(static item => item.Info != null)
                .Select(static (item, _) => item.Info!);

            context.RegisterSourceOutput(infoProvider, Execute);
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            return true;
        }

        private static (TypeInfo? Info, ImmutableEquatableArray<DiagnosticInfo> Diagnostics) GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var typeInfo = TypeInfo.FromContext(context, cancellationToken, out var diagnostics);
            return (typeInfo, diagnostics);
        }

        private static void Report(SourceProductionContext context, ImmutableEquatableArray<DiagnosticInfo> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        private static void Execute(SourceProductionContext context, TypeInfo info)
        {
            var compilationUnit = info.GetCompilationUnit();
            var sourceText = compilationUnit.GetText(Encoding.UTF8);

            context.AddSource($"{info.Name}.g.cs", sourceText);
        }
    }
}
