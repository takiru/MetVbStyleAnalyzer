using System;
using System.Collections.Generic;
using System.IO;
using VSIXProject1;

namespace VSIXProject1
{
    /// <summary>
    /// .editorconfig の [*.vb] セクションに、dotnet_diagnostic.VBNSxxxx.severity の行を
    /// 追記・更新するための簡易ライター。既存の内容はできる限り保持する。
    /// </summary>
    internal static class EditorConfigWriter
    {
        private const string SectionHeader = "[*.vb]";

        public static void ApplySeverities(string path, IReadOnlyDictionary<string, RuleSeverityOption> severities)
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

            foreach (var pair in severities)
            {
                var key = $"dotnet_diagnostic.{pair.Key}.severity";
                var newLine = $"{key} = {ToConfigValue(pair.Value)}";

                var existingIndex = -1;
                for (var i = sectionStart + 1; i < sectionEnd; i++)
                {
                    if (lines[i].TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        existingIndex = i;
                        break;
                    }
                }

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