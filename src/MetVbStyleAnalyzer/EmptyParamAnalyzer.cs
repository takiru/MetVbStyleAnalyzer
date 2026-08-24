using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// ドキュメントコメントに &lt;param&gt; の記載はあるが、中身が空 (または空白のみ) の場合を検出するアナライザー。
    /// &lt;param&gt; が1つも無いケースは MSA1104 (MissingParamAnalyzer) の担当、
    /// name属性が仮引数と一致しないケースは MSA1105 (ParamNameMismatchAnalyzer) の担当なので、
    /// ここでは「中身の空チェック」のみを扱う。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class EmptyParamAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1106";

        /// <summary>
        /// .editorconfig でこのキーにカンマ区切りの引数名を指定すると、
        /// それらの引数の <param> は内容が空でも対象外になる。
        /// 例: msa1106_excluded_param_names = reserved,unused
        /// </summary>
        public const string ExcludedNamesOptionKey = "msa1106_excluded_param_names";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "param の内容が空です",
            messageFormat: "<param name=\"{0}\"> の内容が空です",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ドキュメントコメントの <param> 要素には、空でない説明を記述する必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1106.severity を設定してください。" +
                         "特定の引数名を対象外にするには msa1106_excluded_param_names にカンマ区切りで指定してください。");

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

            var doc = DocCommentUtilities.GetDocumentationComment(node);
            if (doc is null)
            {
                // ドキュメントコメント自体が無いケースは別のアナライザー (MSA1102) の担当
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(node.SyntaxTree);
            var excludedNames = DocCommentUtilities.GetExcludedNames(options, ExcludedNamesOptionKey);

            var paramElements = DocCommentUtilities.GetElements(doc, "param");

            foreach (var paramElement in paramElements)
            {
                if (!DocCommentUtilities.IsElementContentEmpty(paramElement))
                {
                    continue;
                }

                var declaredName = DocCommentUtilities.GetNameAttributeValue(paramElement);

                if (declaredName != null && excludedNames.Contains(declaredName))
                {
                    // 除外リストに含まれる引数名は、内容が空でも対象外にする
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, paramElement.GetLocation(), declaredName ?? "?"));
            }
        }
    }
}