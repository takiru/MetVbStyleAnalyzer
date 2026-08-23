using System;
using System.Collections.Generic;
using System.IO;
using VSIXProject1;

namespace VSIXProject1
{
    /// <summary>
    /// VB.NET プロジェクトが .editorconfig で severity を指定していない場合に適用される、
    /// VS全体 (マシン共通) の .globalconfig を書き出すためのライター。
    /// このファイルを有効にするには、各ソリューションの Directory.Build.props 側で
    /// 一度だけ &lt;GlobalAnalyzerConfigFiles Include="(このパス)" /&gt; の参照を追加してもらう必要がある。
    /// (通常の .editorconfig が同じルールに severity を指定していれば、そちらが必ず優先される)
    /// </summary>
    internal static class GlobalConfigWriter
    {
        public static readonly string DefaultGlobalConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VbNamespaceAnalyzer",
            "VbNamespaceAnalyzer.globalconfig");

        public static void ApplySeverities(string path, IReadOnlyDictionary<string, RuleSeverityOption> severities)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new List<string>
            {
                "is_global = true",
                "global_level = -100",
                string.Empty,
                "# VbNamespaceAnalyzer Visual Studio 拡張機能により自動生成されています",
                "# 各プロジェクトの .editorconfig で同じルールに severity が指定されている場合は、そちらが優先されます"
            };

            foreach (var pair in severities)
            {
                lines.Add($"dotnet_diagnostic.{pair.Key}.severity = {ToConfigValue(pair.Value)}");
            }

            File.WriteAllLines(path, lines);
        }

        private static string ToConfigValue(RuleSeverityOption severity)
        {
            switch (severity)
            {
                case RuleSeverityOption.None:
                    return "none";
                case RuleSeverityOption.Error:
                    return "error";
                case RuleSeverityOption.Warning:
                default:
                    return "warning";
            }
        }
    }
}