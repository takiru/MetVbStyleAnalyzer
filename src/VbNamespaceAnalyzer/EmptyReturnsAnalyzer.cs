using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// ドキュメントコメントに &lt;returns&gt; の記載はあるが、中身が空 (または空白のみ) の場合を検出するアナライザー。
    /// &lt;returns&gt; が1つも無いケースは VBNS0009 (MissingReturnsAnalyzer) の担当なので、
    /// ここでは「中身の空チェック」のみを扱う。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class EmptyReturnsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0011";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "returns の内容が空です",
            messageFormat: "'{0}' の <returns> の内容が空です",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "戻り値を持つ Function / Property のドキュメントコメントの <returns> 要素には、" +
                         "空でない説明を記述する必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0011.severity を設定してください。");

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
            if (returnsElements.IsEmpty)
            {
                // <returns> が1つも無いケースは別のアナライザー (VBNS0009) の担当
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);

            foreach (var returnsElement in returnsElements)
            {
                if (DocCommentUtilities.IsElementContentEmpty(returnsElement))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, returnsElement.GetLocation(), name));
                }
            }
        }
    }
}