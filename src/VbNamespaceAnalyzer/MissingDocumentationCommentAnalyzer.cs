using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// Class / Method(Sub,Function) / Property の宣言に、
    /// そもそもドキュメントコメント (''' ...) が用意されていない場合を検出するアナライザー。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class MissingDocumentationCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0005";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "ドキュメントコメントが指定されていません",
            messageFormat: "'{0}' にドキュメントコメントが指定されていません",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Class / Sub / Function / Property の宣言には、''' で始まるドキュメントコメントが必要です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0005.severity を設定してください。");

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

            if (DocCommentUtilities.GetDocumentationComment(node) is not null)
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }
}