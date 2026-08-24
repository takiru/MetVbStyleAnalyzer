using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// ドキュメントコメントの &lt;param name="..."&gt; が、実際の仮引数の名前と一致しているかを検証するアナライザー。
    /// 以下の両方向のズレを検出する。
    ///   ・記載されている &lt;param&gt; の name が、どの仮引数の名前とも一致しない
    ///   ・仮引数の中に、対応する &lt;param&gt; が1つも記載されていないものがある
    /// &lt;param&gt; が1つも無いケースは MSA1104 (MissingParamAnalyzer) の担当なので、ここでは扱わない。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class ParamNameMismatchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1105";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "param の name 属性が仮引数と一致していません",
            messageFormat: "{0}",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ドキュメントコメントの <param name=\"...\"> は、実際の仮引数の名前と一致している必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1105.severity を設定してください。");

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
                return;
            }

            var doc = DocCommentUtilities.GetDocumentationComment(node);
            if (doc is null)
            {
                // ドキュメントコメント自体が無いケースは別のアナライザー (MSA1102) の担当
                return;
            }

            var paramElements = DocCommentUtilities.GetElements(doc, "param");
            if (paramElements.IsEmpty)
            {
                // <param> が1つも無いケースは別のアナライザー (MSA1104) の担当
                return;
            }

            var actualNames = parameters
                .Select(p => p.Identifier.Identifier.ValueText)
                .ToArray();

            // 1. 記載されている <param> の name が、どの仮引数とも一致しない場合
            foreach (var paramElement in paramElements)
            {
                var declaredName = DocCommentUtilities.GetNameAttributeValue(paramElement);

                var matches = declaredName != null
                    && actualNames.Any(n => string.Equals(n, declaredName, StringComparison.OrdinalIgnoreCase));

                if (!matches)
                {
                    var location = DocCommentUtilities.GetNameAttributeLocation(paramElement);
                    var message = declaredName is null
                        ? "<param> に name 属性が指定されていません"
                        : $"<param name=\"{declaredName}\"> は、実際の仮引数のいずれとも一致していません";

                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, message));
                }
            }

            // 2. 仮引数の中に、対応する <param> が1つも記載されていないものがある場合
            var declaredNames = paramElements
                .Select(DocCommentUtilities.GetNameAttributeValue)
                .Where(n => n != null)
                .ToArray();

            foreach (var parameter in parameters)
            {
                var actualName = parameter.Identifier.Identifier.ValueText;
                var hasMatchingParamTag = declaredNames.Any(n => string.Equals(n, actualName, StringComparison.OrdinalIgnoreCase));

                if (!hasMatchingParamTag)
                {
                    var location = parameter.Identifier.Identifier.GetLocation();
                    var message = $"仮引数 '{actualName}' に対応する <param> の記載がありません";

                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, message));
                }
            }
        }
    }
}