using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// 戻り値を持つ Function / Property において、ドキュメントコメントの &lt;returns&gt; が
    /// 無いか、中身が空の場合を検出するアナライザー。
    /// Sub (戻り値なし) や Class は対象外。ドキュメントコメント自体が存在しない場合は
    /// MSA1102 (MissingDocumentationCommentAnalyzer) の対象なので、ここでは扱わない。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class EmptyReturnsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1107";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "returns の内容が空です",
            messageFormat: "'{0}' の <returns> が無いか、内容が空です",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "戻り値を持つ Function / Property のドキュメントコメントには、空でない <returns> 要素が必要です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1107.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeNode,
                SyntaxKind.FunctionStatement,
                SyntaxKind.PropertyStatement);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;

            if (!DocCommentUtilities.HasReturnValue(node))
            {
                return;
            }

            var doc = DocCommentUtilities.GetDocumentationComment(node);
            if (doc is null)
            {
                // ドキュメントコメント自体が無いケースは別のアナライザー (MSA1102) の担当
                return;
            }

            var returns = DocCommentUtilities.GetElement(doc, "returns");
            if (returns != null && !DocCommentUtilities.IsElementContentEmpty(returns))
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }
}