using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace System.IO.Endian.SourceGenerator
{
    internal class VersionRangeHelper
    {
        private readonly (double? Min, double? Max, string MethodName)[] versionRanges;

        public bool IsVersioned => versionRanges.Length > 0;
        public IfStatementSyntax? VersionCheckStatement { get; }

        public VersionRangeHelper(TypeInfo typeInfo, IdentifierNameSyntax versionParamIdentifier)
        {
            var hasUnboundedMin = false;
            var hasUnboundedMax = false;
            var versionSet = new HashSet<double?>();

            var allVersionAttributes = typeInfo.FixedSizeAttributes.Cast<VersionedAttributeData>()
                .Concat(typeInfo.ByteOrderAttributes)
                .Concat(typeInfo.Properties.SelectMany(p => p.OffsetAttributes.Cast<VersionedAttributeData>().Concat(p.ByteOrderAttributes).Concat(p.StoreTypeAttributes)));

            foreach (var attr in allVersionAttributes)
            {
                if (attr.MinVersion.HasValue)
                    versionSet.Add(attr.MinVersion);
                else
                    hasUnboundedMin = true;

                if (attr.MaxVersion.HasValue)
                    versionSet.Add(attr.MaxVersion);
                else
                    hasUnboundedMax = true;
            }

            if (versionSet.Count == 0)
            {
                versionRanges = [];
                return;
            }

            var versionList = versionSet.ToList();
            versionList.Sort();

            if (hasUnboundedMin)
                versionList.Insert(0, null);
            if (hasUnboundedMax)
                versionList.Add(null);

            versionRanges = new (double?, double?, string)[versionList.Count - 1];

            for (var i = 0; i < versionList.Count - 1; i++)
            {
                var (min, max) = (versionList[i], versionList[i + 1]);
                var minName = min.HasValue
                    ? "_GE" + min.Value.ToString().Replace('.', 'x')
                    : null;
                var maxName = max.HasValue
                    ? "_LT" + max.Value.ToString().Replace('.', 'x')
                    : null;
                versionRanges[i] = (min, max, $"Execute{minName}{maxName}");
            }

            var elseClause = default(ElseClauseSyntax);
            foreach (var (min, max, methodName) in versionRanges.Reverse())
            {
                var minCheck = min.HasValue
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, versionParamIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(min.Value)))
                    : null;

                var maxCheck = max.HasValue
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.LessThanExpression, versionParamIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(max.Value)))
                    : null;

                var combinedCheck = minCheck != null && maxCheck != null
                    ? SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, minCheck, maxCheck)
                    : minCheck ?? maxCheck!;

                //else if ({VersionCheck})
                //    {MethodName}();
                elseClause = SyntaxFactory.ElseClause(
                    SyntaxFactory.IfStatement(
                        combinedCheck,
                        SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName))),
                        elseClause
                    )
                );
            }

            var messageStringSyntax = SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken)).AddContents(
                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, "Must provide a version when reading or writing type \\\"", string.Empty, SyntaxFactory.TriviaList())),
                SyntaxFactory.Interpolation(
                    SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("nameof")).AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(typeInfo.Name))
                        )
                    ),
                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.InterpolatedStringTextToken, "\\\"", string.Empty, SyntaxFactory.TriviaList()))
                );

            //if (version == null)
            //    throw new NotSupportedException("...");
            //[else if ...]
            VersionCheckStatement = SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, versionParamIdentifier, SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("global::System.NotSupportedException")
                    ).AddArgumentListArguments(SyntaxFactory.Argument(
                        //SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal($"Must provide a version when reading or writing type \"{typeInfo.Name}\""))))
                        messageStringSyntax))
                    ),
                elseClause
            );
        }

        public IEnumerable<LocalFunctionStatementSyntax> EnumerateVersionMethodDeclarations(Func<double?, ImmutableArray<StatementSyntax>> statementsFunc)
        {
            foreach (var (min, max, name) in versionRanges)
            {
                var testValue = min ?? (max!.Value - 1);
                var body = statementsFunc(testValue);

                //void {MethodName}()
                //{
                //    ...
                //}
                yield return SyntaxFactory.LocalFunctionStatement(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    name
                ).WithBody(SyntaxFactory.Block(body.ToArray()));
            }
        }
    }
}
