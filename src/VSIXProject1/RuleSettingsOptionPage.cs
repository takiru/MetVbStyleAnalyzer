using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using VSIXProject1;

namespace VSIXProject1
{
    /// <summary>
    /// Tools &gt; Options &gt; VbNamespaceAnalyzer &gt; ルール設定 に表示される設定ページ。
    /// DialogPage の public プロパティは、既定の PropertyGrid ベースの UI として
    /// Visual Studio が自動的に描画する。
    /// </summary>
    public class RuleSettingsOptionPage : DialogPage
    {
        // EmptyParamAnalyzer (VBNS0010) 側の DocCommentUtilities.ExcludedNamesOptionKey と同じ値にする必要がある
        private const string EmptyParamExcludedNamesKey = "vbns0010_excluded_param_names";

        // NestingDepthAnalyzer (VBNS0004) 側の MaxDepthOptionKey と同じ値にする必要がある
        private const string MaxNestingDepthKey = "vbns0004_max_nesting_depth";

        [Category("出力先")]
        [DisplayName("反映先")]
        [Description(".editorconfig に書き込むか、VS全体 (グローバル設定) として適用するかを選択します。")]
        [DefaultValue(ConfigTarget.EditorConfig)]
        public ConfigTarget Target { get; set; } = ConfigTarget.EditorConfig;

        [Category("出力先")]
        [DisplayName(".editorconfig のパス (省略可)")]
        [Description("空の場合、現在開いているソリューションの直下にある .editorconfig を自動的に使用します (反映先が .editorconfig のときのみ有効)。")]
        public string EditorConfigPath { get; set; } = string.Empty;

        [Category("VBNS0001")]
        [DisplayName("名前空間がフォルダー構造と一致していません")]
        public RuleSeverityOption VBNS0001 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0002")]
        [DisplayName("名前空間が指定されていません")]
        public RuleSeverityOption VBNS0002 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0003")]
        [DisplayName("メソッド呼び出しに括弧がありません")]
        public RuleSeverityOption VBNS0003 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0004")]
        [DisplayName("If / Select Case のネストが深すぎます")]
        public RuleSeverityOption VBNS0004 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0004")]
        [DisplayName("許容するネスト階層数")]
        [Description("If / Select Case のネストがこの数値以下であれば対象外になります。既定値は3です。")]
        [DefaultValue(3)]
        public int MaxNestingDepth { get; set; } = 3;

        [Category("VBNS0005")]
        [DisplayName("ドキュメントコメントが指定されていません")]
        public RuleSeverityOption VBNS0005 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0006")]
        [DisplayName("summary の内容が空です")]
        public RuleSeverityOption VBNS0006 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0007")]
        [DisplayName("param の記載がありません")]
        public RuleSeverityOption VBNS0007 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0008")]
        [DisplayName("param の name 属性が仮引数と一致していません")]
        public RuleSeverityOption VBNS0008 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0010")]
        [DisplayName("param の内容が空です")]
        public RuleSeverityOption VBNS0010 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0010")]
        [DisplayName("除外する引数名 (カンマ区切り)")]
        [Description("ここに指定した引数名は、<param> の内容が空でも VBNS0010 の対象外になります。複数指定する場合はカンマで区切ってください。例: reserved,unused")]
        public string EmptyParamExcludedNames { get; set; } = string.Empty;

        [Category("VBNS0011")]
        [DisplayName("returns が無いか、内容が空です")]
        public RuleSeverityOption VBNS0011 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0012")]
        [DisplayName("Public/Protected/Protected Friend の定数に Const を使用しています")]
        public RuleSeverityOption VBNS0012 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0013")]
        [DisplayName("Module 名が指定されていません")]
        public RuleSeverityOption VBNS0013 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0014")]
        [DisplayName("型名 (Class/Structure/Interface/Module/Enum/Delegate) がファイル名と一致していません")]
        public RuleSeverityOption VBNS0014 { get; set; } = RuleSeverityOption.Error;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);

            if (e.ApplyBehavior != ApplyKind.Apply)
            {
                return;
            }

            var severities = new Dictionary<string, RuleSeverityOption>
            {
                ["VBNS0001"] = VBNS0001,
                ["VBNS0002"] = VBNS0002,
                ["VBNS0003"] = VBNS0003,
                ["VBNS0004"] = VBNS0004,
                ["VBNS0005"] = VBNS0005,
                ["VBNS0006"] = VBNS0006,
                ["VBNS0007"] = VBNS0007,
                ["VBNS0008"] = VBNS0008,
                ["VBNS0010"] = VBNS0010,
                ["VBNS0011"] = VBNS0011,
                ["VBNS0012"] = VBNS0012,
                ["VBNS0013"] = VBNS0013,
                ["VBNS0014"] = VBNS0014,
            };

            var extraValues = new Dictionary<string, string>
            {
                [EmptyParamExcludedNamesKey] = NormalizeNameList(EmptyParamExcludedNames),
                [MaxNestingDepthKey] = MaxNestingDepth > 0 ? MaxNestingDepth.ToString() : string.Empty
            };

            if (Target == ConfigTarget.EditorConfig)
            {
                var path = ResolveEditorConfigPath();
                if (path != null)
                {
                    TryApply(() => EditorConfigWriter.ApplySeverities(path, severities), "severityの書き込み");
                    TryApply(() => EditorConfigWriter.ApplyValues(path, extraValues), "追加設定 (ネスト階層数・除外引数名) の書き込み");
                }
            }
            else
            {
                TryApply(() => GlobalConfigWriter.ApplySeverities(GlobalConfigWriter.DefaultGlobalConfigPath, severities), "severityの書き込み");
                TryApply(() => GlobalConfigWriter.ApplyValues(GlobalConfigWriter.DefaultGlobalConfigPath, extraValues), "追加設定 (ネスト階層数・除外引数名) の書き込み");
            }
        }

        private static void TryApply(System.Action action, string description)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"{description}に失敗しました。ファイルが他のプロセスで使用中の可能性があります。\n\n{ex}",
                    "VbNamespaceAnalyzer ルール設定",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private static string NormalizeNameList(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var names = input
                .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);

            return string.Join(",", names);
        }

        private string ResolveEditorConfigPath()
        {
            if (!string.IsNullOrWhiteSpace(EditorConfigPath))
            {
                return EditorConfigPath;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            if (GetService(typeof(DTE)) is DTE dte && !string.IsNullOrEmpty(dte.Solution?.FullName))
            {
                var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
                if (!string.IsNullOrEmpty(solutionDir))
                {
                    return Path.Combine(solutionDir, ".editorconfig");
                }
            }

            // ソリューションが開かれていない等で解決できない場合は何もしない
            return null;
        }
    }
}