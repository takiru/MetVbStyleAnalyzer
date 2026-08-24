using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace MetVbStyleAnalyzer.Vsix
{
    /// <summary>
    /// Tools &gt; Options &gt; VbNamespaceAnalyzer &gt; ルール設定 に表示される設定ページ。
    /// DialogPage の public プロパティは、既定の PropertyGrid ベースの UI として
    /// Visual Studio が自動的に描画する。
    /// </summary>
    public class RuleSettingsOptionPage : DialogPage
    {
        // NamespaceMatchFolderAnalyzer (MSA1000/MSA1001) 側の ExcludedNamespacesOptionKey と同じ値にする必要がある
        private const string ExcludedNamespacesKey = "msa1000_excluded_namespaces";

        // EmptyParamAnalyzer (MSA1106) 側の DocCommentUtilities.ExcludedNamesOptionKey と同じ値にする必要がある
        private const string EmptyParamExcludedNamesKey = "msa1106_excluded_param_names";

        // NestingDepthAnalyzer (MSA1101) 側の MaxDepthOptionKey と同じ値にする必要がある
        private const string MaxNestingDepthKey = "msa1101_max_nesting_depth";

        // EmptyReturnsAnalyzer (MSA1107) 側の AllowEmptyForPropertyOptionKey と同じ値にする必要がある
        private const string AllowEmptyReturnsForPropertyKey = "msa1107_allow_empty_for_property";

        [Category("出力先")]
        [DisplayName("反映先")]
        [Description(".editorconfig に書き込むか、VS全体 (グローバル設定) として適用するかを選択します。")]
        [DefaultValue(ConfigTarget.EditorConfig)]
        public ConfigTarget Target { get; set; } = ConfigTarget.EditorConfig;

        [Category("出力先")]
        [DisplayName(".editorconfig のパス (省略可)")]
        [Description("空の場合、現在開いているソリューションの直下にある .editorconfig を自動的に使用します (反映先が .editorconfig のときのみ有効)。")]
        public string EditorConfigPath { get; set; } = string.Empty;

        [Category("MSA1000")]
        [DisplayName("名前空間がフォルダー構造と一致していません")]
        public RuleSeverityOption MSA1000 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1000")]
        [DisplayName("除外する名前空間 (カンマ区切り)")]
        [Description("ここに指定した名前空間 (プレフィックス一致。サブ名前空間も含む) は、MSA1000 / MSA1001 の両方の対象外になります。RootNamespace を含めても含めなくてもマッチします。複数指定する場合はカンマで区切ってください。例: Migrations,Generated.Code")]
        public string ExcludedNamespaces { get; set; } = string.Empty;

        [Category("MSA1001")]
        [DisplayName("名前空間が指定されていません")]
        public RuleSeverityOption MSA1001 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1100")]
        [DisplayName("メソッドがプロパティのようにアクセスされています")]
        public RuleSeverityOption MSA1100 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1101")]
        [DisplayName("If / Select Case のネストが深すぎます")]
        public RuleSeverityOption MSA1101 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1101")]
        [DisplayName("許容するネスト階層数")]
        [Description("If / Select Case のネストがこの数値以下であれば対象外になります。既定値は3です。")]
        [DefaultValue(3)]
        public int MaxNestingDepth { get; set; } = 3;

        [Category("MSA1102")]
        [DisplayName("ドキュメントコメントが指定されていません")]
        public RuleSeverityOption MSA1102 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1103")]
        [DisplayName("summary の内容が空です")]
        public RuleSeverityOption MSA1103 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1104")]
        [DisplayName("param の記載がありません")]
        public RuleSeverityOption MSA1104 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1105")]
        [DisplayName("param の name 属性が仮引数と一致していません")]
        public RuleSeverityOption MSA1105 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1106")]
        [DisplayName("param の内容が空です")]
        public RuleSeverityOption MSA1106 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1106")]
        [DisplayName("除外する引数名 (カンマ区切り)")]
        [Description("ここに指定した引数名は、<param> の内容が空でも MSA1106 の対象外になります。複数指定する場合はカンマで区切ってください。例: sender,e")]
        public string EmptyParamExcludedNames { get; set; } = "sender,e";

        [Category("MSA1107")]
        [DisplayName("returns が無いか、内容が空です")]
        public RuleSeverityOption MSA1107 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1107")]
        [DisplayName("Property は returns の空を許容する")]
        [Description("true にすると、Property は <returns> が無くても/空でも MSA1107 の対象外になります。既定は false (Property も検証対象)。")]
        [DefaultValue(false)]
        public bool AllowEmptyReturnsForProperty { get; set; } = false;

        [Category("MSA1108")]
        [DisplayName("Public/Protected/Protected Friend の定数に Const を使用しています")]
        public RuleSeverityOption MSA1108 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1109")]
        [DisplayName("Module 名が指定されていません")]
        public RuleSeverityOption MSA1109 { get; set; } = RuleSeverityOption.Error;

        [Category("MSA1110")]
        [DisplayName("型名 (Class/Structure/Interface/Module/Enum/Delegate) がファイル名と一致していません")]
        public RuleSeverityOption MSA1110 { get; set; } = RuleSeverityOption.Error;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);

            if (e.ApplyBehavior != ApplyKind.Apply)
            {
                return;
            }

            var severities = new Dictionary<string, RuleSeverityOption>
            {
                ["MSA1000"] = MSA1000,
                ["MSA1001"] = MSA1001,
                ["MSA1100"] = MSA1100,
                ["MSA1101"] = MSA1101,
                ["MSA1102"] = MSA1102,
                ["MSA1103"] = MSA1103,
                ["MSA1104"] = MSA1104,
                ["MSA1105"] = MSA1105,
                ["MSA1106"] = MSA1106,
                ["MSA1107"] = MSA1107,
                ["MSA1108"] = MSA1108,
                ["MSA1109"] = MSA1109,
                ["MSA1110"] = MSA1110,
            };

            var extraValues = new Dictionary<string, string>
            {
                [ExcludedNamespacesKey] = NormalizeNameList(ExcludedNamespaces),
                [EmptyParamExcludedNamesKey] = NormalizeNameList(EmptyParamExcludedNames),
                [MaxNestingDepthKey] = MaxNestingDepth > 0 ? MaxNestingDepth.ToString() : string.Empty,
                [AllowEmptyReturnsForPropertyKey] = AllowEmptyReturnsForProperty ? "true" : "false"
            };

            if (Target == ConfigTarget.EditorConfig)
            {
                var path = ResolveEditorConfigPath();
                if (path != null)
                {
                    TryApply(() => EditorConfigWriter.ApplySeverities(path, severities), "severityの書き込み");
                    TryApply(() => EditorConfigWriter.ApplyValues(path, extraValues), "追加設定 (除外名前空間・ネスト階層数・除外引数名・Property空許容) の書き込み");
                }
            }
            else
            {
                TryApply(() => GlobalConfigWriter.ApplySeverities(GlobalConfigWriter.DefaultGlobalConfigPath, severities), "severityの書き込み");
                TryApply(() => GlobalConfigWriter.ApplyValues(GlobalConfigWriter.DefaultGlobalConfigPath, extraValues), "追加設定 (除外名前空間・ネスト階層数・除外引数名・Property空許容) の書き込み");
            }
        }

        private static void TryApply(Action action, string description)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    ServiceProvider.GlobalProvider,
                    $"{description}に失敗しました。ファイルが他のプロセスで使用中の可能性があります。\n\n{ex}",
                    "MetVbStyleAnalyzer ルール設定",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static string NormalizeNameList(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var names = input
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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