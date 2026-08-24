using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// VB.NET では引数なしのメソッド呼び出しで括弧を省略できてしまうため、
    /// 「Dim value = Me.Fuga」のように、実際には Function/Sub であるメンバーを
    /// プロパティのように括弧なしで参照しているコードを検出するアナライザー。
    /// AddressOf によるデリゲート参照や NameOf は意図的な参照として対象外にする。
    /// Implements / Handles 句のメンバー参照は、式ではなく宣言の一部 (QualifiedNameSyntax) として
    /// 解析され、呼び出しの概念自体が存在しないため対象外にする。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class MethodInvocationParenthesesAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1100";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "メソッドがプロパティのようにアクセスされています",
            messageFormat: "メソッド '{0}' がプロパティのように括弧なしで参照されています。呼び出すには '{0}()' のように括弧を付けてください",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "VB.NET は引数なしのメソッド呼び出しで括弧を省略できてしまうため、" +
                         "誤ってメソッドをプロパティのように扱っていても気づきにくくなります。" +
                         "AddressOf によるデリゲート参照や NameOf、Implements / Handles 句は対象外です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1100.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeMemberReference,
                SyntaxKind.IdentifierName,
                SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeMemberReference(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;

            // "Fuga()" のように、この参照がそのまま呼び出し式の対象になっている場合は
            // 括弧が明示されているので対象外
            if (node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node)
            {
                return;
            }

            // "AddressOf Fuga" は意図的にメソッドをデリゲートとして参照しているので対象外
            if (node.Parent is UnaryExpressionSyntax unary && unary.Kind() == SyntaxKind.AddressOfExpression)
            {
                return;
            }

            // "NameOf(Fuga)" はメソッド名を文字列化しているだけで呼び出しではないため対象外
            if (node.Parent is NameOfExpressionSyntax)
            {
                return;
            }

            // "Implements IFoo.Bar" / "Handles Control.Event" のメンバー参照は、
            // 式 (呼び出し) ではなく宣言の一部 (QualifiedNameSyntax) として解析されるため対象外
            if (IsWithinImplementsOrHandlesClause(node))
            {
                return;
            }

            // "Me.Fuga" の ".Fuga" 部分 (IdentifierNameSyntax) は、
            // 親の MemberAccessExpressionSyntax ("Me.Fuga" 全体) 側で判定するため、
            // 二重報告を避けてここではスキップする
            if (node is IdentifierNameSyntax && node.Parent is MemberAccessExpressionSyntax parentMemberAccess
                && parentMemberAccess.Name == node)
            {
                return;
            }

            var symbolInfo = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            if (methodSymbol == null || methodSymbol.MethodKind != MethodKind.Ordinary)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                Rule,
                node.GetLocation(),
                methodSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsWithinImplementsOrHandlesClause(SyntaxNode node)
        {
            return node.Ancestors().Any(a => a is ImplementsClauseSyntax || a is HandlesClauseSyntax);
        }
    }
}