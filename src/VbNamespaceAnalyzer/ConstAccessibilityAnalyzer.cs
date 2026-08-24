using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// スコープが Public / Protected / Protected Friend の Const フィールドを禁止するアナライザー。
    /// Const は値がコンパイル時に呼び出し元アセンブリへ埋め込まれるため、
    /// 公開・準公開スコープで値を変更すると、再コンパイルしていない呼び出し元だけ
    /// 古い値のまま動作し続けるという実行時の不整合が起きやすい。
    /// この用途では Shared ReadOnly を使うことを推奨する。
    /// Enum のメンバーは対象外 (Const キーワードを明示的に書くものではないため)。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class ConstAccessibilityAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0012";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "このスコープでは Const を使用できません",
            messageFormat: "定数 '{0}' のスコープが {1} です。Public / Protected / Protected Friend の定数には " +
                           "Const を使用せず、Shared ReadOnly を使用してください",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Const フィールドの値は呼び出し元アセンブリのコンパイル時に埋め込まれるため、" +
                         "Public / Protected / Protected Friend のような公開・準公開スコープで値を変更すると、" +
                         "再コンパイルしていない呼び出し元だけ古い値のまま動作し続ける不整合が起きます。" +
                         "この用途では Shared ReadOnly を使用してください。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0012.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            if (context.Symbol is not IFieldSymbol field || !field.IsConst)
            {
                return;
            }

            // Enum のメンバーは Const 扱いになるが、Const キーワードを明示的に書くものではないため対象外
            if (field.ContainingType?.TypeKind == TypeKind.Enum)
            {
                return;
            }

            if (field.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return;
            }

            var location = field.Locations.Length > 0 ? field.Locations[0] : Location.None;
            var accessibilityText = GetAccessibilityDisplay(field.DeclaredAccessibility);

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, field.Name, accessibilityText));
        }

        private static string GetAccessibilityDisplay(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public:
                    return "Public";
                case Accessibility.Protected:
                    return "Protected";
                case Accessibility.ProtectedOrInternal:
                    return "Protected Friend";
                default:
                    return accessibility.ToString();
            }
        }
    }
}