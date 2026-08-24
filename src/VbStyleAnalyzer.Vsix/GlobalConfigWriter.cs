using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VbStyleAnalyzer.Vsix
{
    /// <summary>
    /// VB.NET プロジェクトが .editorconfig で severity 等を指定していない場合に適用される、
    /// VS全体 (マシン共通) の .globalconfig を書き出すためのライター。
    /// このファイルを有効にするには、各ソリューションの Directory.Build.props 側で
    /// 一度だけ &lt;GlobalAnalyzerConfigFiles Include="(このパス)" /&gt; の参照を追加してもらう必要がある。
    /// (通常の .editorconfig が同じキーを指定していれば、そちらが必ず優先される)
    /// </summary>
    internal static class GlobalConfigWriter
    {
        public static readonly string DefaultGlobalConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VbNamespaceAnalyzer",
            "VbNamespaceAnalyzer.globalconfig");

        private static readonly string[] HeaderLines =
        {
            "is_global = true",
            "global_level = -100",
            string.Empty,
            "# VbNamespaceAnalyzer Visual Studio 拡張機能により自動生成されています",
            "# 各プロジェクトの .editorconfig で同じキーが指定されている場合は、そちらが優先されます",
            string.Empty
        };

        public static void ApplySeverities(string path, IReadOnlyDictionary<string, RuleSeverityOption> severities)
        {
            var values = new Dictionary<string, string>();

            foreach (var pair in severities)
            {
                values[$"dotnet_diagnostic.{pair.Key}.severity"] = ToConfigValue(pair.Value);
            }

            ApplyValues(path, values);
        }

        /// <summary>
        /// 任意の key = value の組を .globalconfig にマージして書き込む。
        /// 既存のキーは値を上書きし、値が空文字列のキーは削除する。呼び出すたびに全体を上書きしない。
        /// </summary>
        public static void ApplyValues(string path, IReadOnlyDictionary<string, string> values)
        {
            FileRetry.Execute(() => ApplyValuesCore(path, values));
        }

        private static void ApplyValuesCore(string path, IReadOnlyDictionary<string, string> values)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 既存の key=value 行を読み込む (ヘッダー行やコメント、is_global/global_level は維持のため除外して個別管理)
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var separatorIndex = trimmed.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = trimmed.Substring(0, separatorIndex).Trim();
                    var value = trimmed.Substring(separatorIndex + 1).Trim();

                    if (string.Equals(key, "is_global", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "global_level", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    existing[key] = value;
                }
            }

            foreach (var pair in values)
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    existing.Remove(pair.Key);
                }
                else
                {
                    existing[pair.Key] = pair.Value;
                }
            }

            var lines = new List<string>(HeaderLines);
            lines.AddRange(existing.Select(pair => $"{pair.Key} = {pair.Value}"));

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