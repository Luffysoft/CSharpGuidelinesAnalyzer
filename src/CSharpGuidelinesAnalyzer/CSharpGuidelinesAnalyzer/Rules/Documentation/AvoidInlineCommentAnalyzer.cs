using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharpGuidelinesAnalyzer.Rules.Documentation
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AvoidInlineCommentAnalyzer : GuidelineAnalyzer
    {
        public const string DiagnosticId = "AV2310";

        private const string Title = "Code block should not contain inline comment";
        private const string MessageFormat = "Code block should not contain inline comment.";
        private const string Description = "Avoid inline comments.";

        [NotNull]
        private static readonly AnalyzerCategory Category = AnalyzerCategory.Documentation;

        [NotNull]
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat,
            Category.DisplayName, DiagnosticSeverity.Warning, false, Description, Category.GetHelpLinkUri(DiagnosticId));

        [ItemNotNull]
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        [ItemNotNull]
        private static readonly ImmutableArray<string> ArrangeActAssertLines =
            ImmutableArray.Create("// Arrange", "// Act", "// Assert", "// Act and assert");

        public override void Initialize([NotNull] AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCodeBlockAction(AnalyzeCodeBlock);
        }

        private void AnalyzeCodeBlock(CodeBlockAnalysisContext context)
        {
            SyntaxTrivia[] outerCommentTrivia = context.CodeBlock.GetLeadingTrivia().Concat(context.CodeBlock.GetTrailingTrivia())
                .Where(IsComment).ToArray();

            AnalyzeCommentTrivia(outerCommentTrivia, context);
        }

        private void AnalyzeCommentTrivia([NotNull] SyntaxTrivia[] outerCommentTrivia, CodeBlockAnalysisContext context)
        {
            foreach (SyntaxTrivia commentTrivia in context.CodeBlock.DescendantTrivia().Where(IsComment))
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (!outerCommentTrivia.Contains(commentTrivia) && !IsCommentInEmptyElseClause(commentTrivia))
                {
                    string commentText = commentTrivia.ToString();

                    if (!IsResharperSuppression(commentText) && !IsResharperLanguageInjection(commentText) &&
                        !IsArrangeActAssertUnitTestPattern(commentText))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, commentTrivia.GetLocation()));
                    }
                }
            }
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            SyntaxKind kind = trivia.Kind();
            return kind == SyntaxKind.SingleLineCommentTrivia || kind == SyntaxKind.MultiLineCommentTrivia;
        }

        private static bool IsCommentInEmptyElseClause(SyntaxTrivia commentTrivia)
        {
            return commentTrivia.Token.Parent is BlockSyntax parentBlock && !parentBlock.Statements.Any() &&
                parentBlock.Parent is ElseClauseSyntax;
        }

        private bool IsResharperSuppression([NotNull] string commentText)
        {
            return commentText.Contains("// ReSharper disable ") || commentText.Contains("// ReSharper restore ");
        }

        private bool IsResharperLanguageInjection([NotNull] string commentText)
        {
            return commentText.Contains("language=");
        }

        private bool IsArrangeActAssertUnitTestPattern([NotNull] string commentText)
        {
            return ArrangeActAssertLines.Any(line => line.Equals(commentText));
        }
    }
}
