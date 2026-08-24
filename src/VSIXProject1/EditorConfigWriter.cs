using System;
using System.Collections.Generic;
using System.IO;
using VSIXProject1;

namespace VSIXProject1
{
    /// <summary>
    /// .editorconfig の [*.vb] セクションに、key = value 形式の行を追記・更新するための簡易ライター。
    /// 既存の内容はできる限り保持する。
    /// </summary>
    internal static class EditorConfigWriter
    {
        private const string SectionHeader = "[*.vb]";

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
        /// 任意の key = value の組を [*.vb] セクションに書き込む。
        /// 値が空文字列の場合は、その行を削除する (未設定に戻す) 挙動にする。
        /// </summary>
        public static void ApplyValues(string path, IReadOnlyDictionary<string, string> values)
        {
            FileRetry.Execute(() => ApplyValuesCore(path, values));
        }

        private static void ApplyValuesCore(string path, IReadOnlyDictionary<string, string> values)
        {
            var lines = File.Exists(path)
                ? new List<string>(File.ReadAllLines(path))
                : new List<string> { "# VbNamespaceAnalyzer Visual Studio 拡張機能により自動更新されています" };

            var sectionStart = lines.FindIndex(l => l.Trim().Equals(SectionHeader, StringComparison.OrdinalIgnoreCase));

            if (sectionStart < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
                {
                    lines.Add(string.Empty);
                }

                lines.Add(SectionHeader);
                sectionStart = lines.Count - 1;
            }

            var sectionEnd = lines.FindIndex(sectionStart + 1, l => l.TrimStart().StartsWith("[", StringComparison.Ordinal));
            if (sectionEnd < 0)
            {
                sectionEnd = lines.Count;
            }

            foreach (var pair in values)
            {
                var key = pair.Key;

                var existingIndex = -1;
                for (var i = sectionStart + 1; i < sectionEnd; i++)
                {
                    if (lines[i].TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(pair.Value))
                {
                    // 値が空なら、既存の行があれば削除して未設定に戻す
                    if (existingIndex >= 0)
                    {
                        lines.RemoveAt(existingIndex);
                        sectionEnd--;
                    }

                    continue;
                }

                var newLine = $"{key} = {pair.Value}";

                if (existingIndex >= 0)
                {
                    lines[existingIndex] = newLine;
                }
                else
                {
                    lines.Insert(sectionEnd, newLine);
                    sectionEnd++;
                }
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