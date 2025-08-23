using Microsoft.CodeAnalysis;
using System.Text;
using static System.IO.Endian.SourceGenerator.Globals;

namespace System.IO.Endian.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class SourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider.ForAttributeWithMetadataName(MarkerAttribute, IsSyntaxTargetForGeneration, GetSemanticTargetForGeneration);
            context.RegisterSourceOutput(provider, Execute);
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            return true;
        }

        private static TypeInfo GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            return TypeInfo.FromContext(context, cancellationToken);
        }

        private static void Execute(SourceProductionContext context, TypeInfo item)
        {
            var compilationUnit = item.GetCompilationUnit();
            var sourceText = compilationUnit.GetText(Encoding.UTF8);

            context.AddSource($"{item.Name}.g.cs", sourceText);
        }
    }
}
