using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace MetVbStyleAnalyzer
{
    /// <summary>
    /// VB.NET の Module のメンバー (メソッド・フィールド) は、Module名を付けずに
    /// 直接参照できてしまうため、Module名を明示的に指定していない参照を検出するアナライザー。
    /// どのModuleのメンバーかが参照元から分かりにくくなることを防ぐ目的。
    /// ただし、そのメンバーを宣言している Module 自身の内部からの参照は許可し、
    /// 外部 (別の Module・Class 等) からの未修飾アクセスのみを対象にする。
    ///
    /// また、Microsoft.VisualBasic.Strings (Len, UCase 等) のような、
    /// 参照アセンブリ (ソースコードではなくビルド済みのライブラリ) 側の Module は対象外にする。
    /// このアナライザーが検出したいのは「自分のソリューション内で定義した Module」のメンバーであり、
    /// 慣習的に無条件で使われる VB ランタイムの組み込み関数群まで対象にすると実用的でないため。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class ModuleMemberQualificationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1109";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Module 名が指定されていません",
            messageFormat: "'{0}' への参照に Module 名が指定されていません。'{1}.{0}' のように記述してください",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "VB.NET の Module のメンバー (メソッド・フィールド) は、Module名を付けずに参照できてしまいます。" +
                         "参照元だけを見てどの Module のメンバーかが分かりにくくなるため、" +
                         "常に Module 名を明示的に指定することを推奨します。" +
                         "対象は自分のソリューション内 (ソースコード) で定義された Module のみで、" +
                         "参照アセンブリ側の Module (VBランタイムの組み込み関数群等) は対象外です。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1109.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        }

        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
        {
            var node = (IdentifierNameSyntax)context.Node;

            // "ModuleName.Member" のように、既に Module 名で修飾されている参照は対象外
            if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node)
            {
                return;
            }

            var symbolInfo = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken);
            var symbol = symbolInfo.Symbol;

            if (symbol is IMethodSymbol method && method.MethodKind != MethodKind.Ordinary)
            {
                // プロパティのGet/Set、演算子、コンストラクター等は対象外
                return;
            }

            if (!(symbol is IMethodSymbol) && !(symbol is IFieldSymbol))
            {
                return;
            }

            var containingType = symbol.ContainingType;
            if (containingType is null || containingType.TypeKind != TypeKind.Module)
            {
                return;
            }

            // 参照アセンブリ (ソースコードではなくビルド済みのDLL) 側で定義された Module は対象外。
            // 自分のソリューション内で書いた Module (ソースコードとして存在するもの) だけを対象にする。
            if (!containingType.Locations.Any(loc => loc.IsInSource))
            {
                return;
            }

            // 参照元が、そのメンバーを宣言している Module 自身の内部である場合は許可する
            var usageContainingType = context.ContainingSymbol?.ContainingType;
            if (usageContainingType != null && SymbolEqualityComparer.Default.Equals(usageContainingType, containingType))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), symbol.Name, containingType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}