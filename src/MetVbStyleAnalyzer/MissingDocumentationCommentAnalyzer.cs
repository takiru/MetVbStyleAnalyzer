using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// Class / Method(Sub,Function) / Property の宣言に、
    /// そもそもドキュメントコメント (''' ...) が用意されていない場合を検出するアナライザー。
    /// Partial な宣言 (Partial Class や、VBのPartialメソッド) は対象外にする。
    /// 分割定義のうち、ドキュメントコメントが本体側にだけ書かれているのは正当なケースであり、
    /// 全ての分割ファイルに重複してコメントを要求するのは実用的でないため。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class MissingDocumentationCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1102";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "ドキュメントコメントが指定されていません",
            messageFormat: "'{0}' にドキュメントコメントが指定されていません",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Class / Sub / Function / Property の宣言には、''' で始まるドキュメントコメントが必要です。" +
                         "Partial な宣言は対象外です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1102.severity を設定してください。");

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

            if (HasPartialModifier(node))
            {
                return;
            }

            if (DocCommentUtilities.GetDocumentationComment(node) != null)
            {
                return;
            }

            var (name, location) = DocCommentUtilities.GetMemberNameAndLocation(node);
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }

        private static bool HasPartialModifier(SyntaxNode node)
        {
            var modifiers = GetModifiers(node);

            foreach (var modifier in modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static SyntaxTokenList GetModifiers(SyntaxNode node)
        {
            switch (node)
            {
                case ClassStatementSyntax c:
                    return c.Modifiers;
                case MethodStatementSyntax m:
                    // Sub のみ Partial メソッドになり得る (Function は VB の仕様上 Partial 不可)
                    return m.Modifiers;
                case PropertyStatementSyntax p:
                    // Property 自体は Partial にできないが、念のため他と同様に扱う
                    return p.Modifiers;
                default:
                    return default;
            }
        }
    }
}