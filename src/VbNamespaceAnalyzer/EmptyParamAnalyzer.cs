using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// ドキュメントコメントに &lt;param&gt; の記載はあるが、中身が空 (または空白のみ) の場合を検出するアナライザー。
    /// &lt;param&gt; が1つも無いケースは VBNS0007 (MissingParamAnalyzer) の担当、
    /// name属性が仮引数と一致しないケースは VBNS0008 (ParamNameMismatchAnalyzer) の担当なので、
    /// ここでは「中身の空チェック」のみを扱う。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class EmptyParamAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0010";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "param の内容が空です",
            messageFormat: "<param name=\"{0}\"> の内容が空です",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ドキュメントコメントの <param> 要素には、空でない説明を記述する必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0010.severity を設定してください。");

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
                // ドキュメントコメント自体が無いケースは別のアナライザー (VBNS0005) の担当
                return;
            }

            var paramElements = DocCommentUtilities.GetElements(doc, "param");

            foreach (var paramElement in paramElements)
            {
                if (!DocCommentUtilities.IsElementContentEmpty(paramElement))
                {
                    continue;
                }

                var declaredName = DocCommentUtilities.GetNameAttributeValue(paramElement) ?? "?";
                context.ReportDiagnostic(Diagnostic.Create(Rule, paramElement.GetLocation(), declaredName));
            }
        }
    }
}