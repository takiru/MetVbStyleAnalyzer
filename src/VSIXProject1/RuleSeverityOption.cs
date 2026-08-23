namespace VSIXProject1
{
    /// <summary>
    /// ルールごとに選択できる重要度。.editorconfig / .globalconfig の severity 値に対応する。
    /// </summary>
    public enum RuleSeverityOption
    {
        None,
        Warning,
        Error
    }

    /// <summary>
    /// 設定ダイアログで選んだ severity を、どこに反映するか。
    /// </summary>
    public enum ConfigTarget
    {
        /// <summary>現在のソリューションの .editorconfig に反映する</summary>
        EditorConfig,

        /// <summary>VS全体 (マシン共通) のグローバル設定として反映する</summary>
        GlobalConfig
    }
}
