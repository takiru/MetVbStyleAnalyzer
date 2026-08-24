using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace VbStyleAnalyzer
{
    /// <summary>
    /// 仮引数を持つ Method / Property において、ドキュメントコメントに
    /// &lt;param&gt; の記載が1つも無い場合を検出するアナライザー。
    /// 仮引数が無いメンバーは対象外。ドキュメントコメント自体が無い場合は MSA1102 の担当。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class MissingParamAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1104";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "param の記載がありません",
            messageFormat: "'{0}' は仮引数を持ちますが、ドキュメントコメントに <param> の記載がありません",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "仮引数を持つ Method / Property のドキュメントコメントには、" +
                         "各仮引数に対応する <param> 要素が必要です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1104.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeNode,
                SyntaxKind.SubStatement,
                SyntaxKind.FunctionStatement,
                SyntaxKind.PropertyStatement);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;

            var parameters = DocCommentUtilities.GetParameters(node);
            if (parameters.IsEmpty)
            {
                // 仮引数が無ければ <param> は不要
                return;
            }

            var doc = DocCommentUtilities.GetDocumentationComment(node);
            if (doc is null)
            {
                // ドキュメントコメント自体が無いケースは別のアナライザー (MSA1102) の担当
                return;
            }

            var paramElements = DocCommentUtilities.GetElements(doc, "param");
            if (!paramElements.IsEmpty)
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }
}