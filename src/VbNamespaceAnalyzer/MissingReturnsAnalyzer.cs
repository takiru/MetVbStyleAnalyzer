using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// 戻り値を持つ Function / Property において、ドキュメントコメントに
    /// &lt;returns&gt; の記載が無い場合を検出するアナライザー。
    /// Sub (戻り値なし) や Class は対象外。ドキュメントコメント自体が無い場合は VBNS0005 の担当。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class MissingReturnsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0009";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "returns の記載がありません",
            messageFormat: "'{0}' は戻り値を持ちますが、ドキュメントコメントに <returns> の記載がありません",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "戻り値を持つ Function / Property のドキュメントコメントには、<returns> 要素が必要です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0009.severity を設定してください。");

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
                // ドキュメントコメント自体が無いケースは別のアナライザー (VBNS0005) の担当
                return;
            }

            var returnsElements = DocCommentUtilities.GetElements(doc, "returns");
            if (!returnsElements.IsEmpty)
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }
}