using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// ドキュメントコメントは存在するが、&lt;summary&gt; が無い、または中身が空の場合を検出するアナライザー。
    /// ドキュメントコメント自体が存在しない場合は MSA1102 (MissingDocumentationCommentAnalyzer) の対象なので、
    /// ここでは扱わない。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class EmptySummaryAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1103";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "summary の内容が空です",
            messageFormat: "'{0}' の <summary> が無いか、内容が空です",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ドキュメントコメントの <summary> 要素には、空でない説明を記述する必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1103.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeNode,
                SyntaxKind.ClassStatement,
                SyntaxKind.SubStatement,
                SyntaxKind.FunctionStatement,
                SyntaxKind.PropertyStatement);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;

            var doc = DocCommentUtilities.GetDocumentationComment(node);
            if (doc is null)
            {
                // ドキュメントコメント自体が無いケースは別のアナライザー (MSA1102) の担当
                return;
            }

            var summary = DocCommentUtilities.GetElement(doc, "summary");
            if (summary != null && !DocCommentUtilities.IsElementContentEmpty(summary))
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }
}