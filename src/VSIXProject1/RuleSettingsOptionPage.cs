using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

        [Category("VBNS0009")]
        [DisplayName("returns の記載がありません")]
        public RuleSeverityOption VBNS0009 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0010")]
        [DisplayName("param の内容が空です")]
        public RuleSeverityOption VBNS0010 { get; set; } = RuleSeverityOption.Error;

        [Category("VBNS0011")]
        [DisplayName("returns の内容が空です")]
        public RuleSeverityOption VBNS0011 { get; set; } = RuleSeverityOption.Error;

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
                ["VBNS0009"] = VBNS0009,
                ["VBNS0010"] = VBNS0010,
                ["VBNS0011"] = VBNS0011,
            };

            if (Target == ConfigTarget.EditorConfig)
            {
                var path = ResolveEditorConfigPath();
                if (path != null)
                {
                    EditorConfigWriter.ApplySeverities(path, severities);
                }
            }
            else
            {
                GlobalConfigWriter.ApplySeverities(GlobalConfigWriter.DefaultGlobalConfigPath, severities);
            }
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