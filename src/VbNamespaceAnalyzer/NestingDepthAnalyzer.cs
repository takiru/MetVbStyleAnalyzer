using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// If ブロックや Select Case ブロックのネストが、既定の許容階層数 (3) を超えている場合を検出するアナライザー。
    /// ElseIf / Else 自体はネストを増やさず、その中にさらに If / Select Case が書かれた場合のみ加算する。
    /// メソッド・プロパティ・ラムダなどの境界を越えたカウントは行わない。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class NestingDepthAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0004";

        // 3階層までは許容し、4階層目 (これを超えた時点) で警告する
        private const int MaxAllowedDepth = 3;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "If / Select Case のネストが深すぎます",
            messageFormat: "If / Select Case のネストが {0} 階層になっています。{1} 階層以内に収めてください",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "If ブロックや Select Case ブロックが深くネストしていると可読性が落ちるため、" +
                         "既定では4階層以上のネストを検出します。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0004.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeNode,
                SyntaxKind.MultiLineIfBlock,
                SyntaxKind.SingleLineIfStatement,
                SyntaxKind.SelectBlock);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var depth = 1;
            var current = node.Parent;

            while (current is not null)
            {
                var stop = false;

                switch (current.Kind())
                {
                    case SyntaxKind.MultiLineIfBlock:
                    case SyntaxKind.SingleLineIfStatement:
                    case SyntaxKind.SelectBlock:
                        depth++;
                        break;

                    // メソッド・プロパティ・ラムダなどの境界を越えたら、それ以上は数えない
                    case SyntaxKind.SubBlock:
                    case SyntaxKind.FunctionBlock:
                    case SyntaxKind.ConstructorBlock:
                    case SyntaxKind.OperatorBlock:
                    case SyntaxKind.GetAccessorBlock:
                    case SyntaxKind.SetAccessorBlock:
                    case SyntaxKind.AddHandlerAccessorBlock:
                    case SyntaxKind.RemoveHandlerAccessorBlock:
                    case SyntaxKind.RaiseEventAccessorBlock:
                    case SyntaxKind.MultiLineFunctionLambdaExpression:
                    case SyntaxKind.MultiLineSubLambdaExpression:
                    case SyntaxKind.SingleLineFunctionLambdaExpression:
                    case SyntaxKind.SingleLineSubLambdaExpression:
                        stop = true;
                        break;
                }

                if (stop)
                {
                    break;
                }

                current = current.Parent;
            }

            if (depth <= MaxAllowedDepth)
            {
                return;
            }

            var location = node switch
            {
                MultiLineIfBlockSyntax multiLineIf => multiLineIf.IfStatement.GetLocation(),
                SingleLineIfStatementSyntax singleLineIf => singleLineIf.IfKeyword.GetLocation(),
                SelectBlockSyntax selectBlock => selectBlock.SelectStatement.GetLocation(),
                _ => node.GetLocation()
            };

            var diagnostic = Diagnostic.Create(Rule, location, depth, MaxAllowedDepth);
            context.ReportDiagnostic(diagnostic);
        }
    }
}