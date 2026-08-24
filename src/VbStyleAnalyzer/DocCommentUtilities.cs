using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace VbStyleAnalyzer
{
    /// <summary>
    /// VB.NET のドキュメントコメント (''' で始まるXMLコメント) を解析するための共通ヘルパー。
    /// これ自体は DiagnosticAnalyzer ではなく、各ドキュメントコメント系アナライザーから利用される。
    /// </summary>
    internal static class DocCommentUtilities
    {
        /// <summary>
        /// 指定した宣言ノードの先頭トリビアから、ドキュメントコメントを取得する。
        /// 存在しない場合は null を返す。
        /// </summary>
        public static DocumentationCommentTriviaSyntax GetDocumentationComment(SyntaxNode node)
        {
            foreach (var trivia in node.GetLeadingTrivia())
            {
                if (trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
                {
                    return doc;
                }
            }

            return null;
        }

        /// <summary>
        /// ドキュメントコメント直下にある、指定タグ名の最初の要素を取得する (例: "summary", "returns")。
        /// </summary>
        public static XmlElementSyntax GetElement(DocumentationCommentTriviaSyntax doc, string tagName)
        {
            return doc.Content
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(e => IsNamed(e, tagName));
        }

        /// <summary>
        /// ドキュメントコメント直下にある、指定タグ名の要素をすべて取得する (例: "param" は複数あり得る)。
        /// </summary>
        public static ImmutableArray<XmlElementSyntax> GetElements(DocumentationCommentTriviaSyntax doc, string tagName)
        {
            return doc.Content
                .OfType<XmlElementSyntax>()
                .Where(e => IsNamed(e, tagName))
                .ToImmutableArray();
        }

        private static bool IsNamed(XmlElementSyntax element, string tagName)
        {
            return element.StartTag.Name is XmlNameSyntax name
                && string.Equals(name.LocalName.ValueText, tagName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// XML要素の中身のテキストが空 (または空白のみ) かどうかを判定する。
        /// </summary>
        public static bool IsElementContentEmpty(XmlElementSyntax element)
        {
            var text = string.Concat(element.Content
                .OfType<XmlTextSyntax>()
                .SelectMany(t => t.TextTokens)
                .Select(t => t.ValueText));

            return string.IsNullOrWhiteSpace(text);
        }

        /// <summary>
        /// "&lt;param name="x"&gt;" のような要素から、name属性が参照している識別子名を取得する。
        /// VB.NET のドキュメントコメントでは、name属性は通常の XmlAttributeSyntax ではなく
        /// 専用の XmlNameAttributeSyntax として解析される点に注意。
        /// </summary>
        public static string GetNameAttributeValue(XmlElementSyntax element)
        {
            foreach (var attribute in element.StartTag.Attributes)
            {
                if (attribute is XmlNameAttributeSyntax nameAttribute)
                {
                    return nameAttribute.Reference.Identifier.ValueText;
                }
            }

            return null;
        }

        /// <summary>
        /// "&lt;param name="x"&gt;" の name 属性部分の位置を取得する (診断の報告位置に使う)。
        /// name属性が見つからない場合は要素全体の位置を返す。
        /// </summary>
        public static Location GetNameAttributeLocation(XmlElementSyntax element)
        {
            foreach (var attribute in element.StartTag.Attributes)
            {
                if (attribute is XmlNameAttributeSyntax nameAttribute)
                {
                    return nameAttribute.Reference.GetLocation();
                }
            }

            return element.GetLocation();
        }

        /// <summary>
        /// .editorconfig のカスタムキー (カンマ区切りの引数名リスト) を読み取り、
        /// 除外対象の引数名の集合を返す。キーが無い場合は空集合。
        /// 例: msa1106_excluded_param_names = reserved,unused
        /// </summary>
        public static ImmutableHashSet<string> GetExcludedNames(AnalyzerConfigOptions options, string key)
        {
            if (!options.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return ImmutableHashSet<string>.Empty;
            }

            return raw
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Class / Method(Sub,Function) / Property の宣言ノードから、
        /// 実際の仮引数の一覧を取得する。対象外の宣言や引数なしの場合は空配列。
        /// </summary>
        public static ImmutableArray<ParameterSyntax> GetParameters(SyntaxNode node)
        {
            var methodStatement = node as MethodStatementSyntax;
            if (methodStatement != null)
            {
                return methodStatement.ParameterList?.Parameters.ToImmutableArray()
                       ?? ImmutableArray<ParameterSyntax>.Empty;
            }

            var propertyStatement = node as PropertyStatementSyntax;
            if (propertyStatement != null)
            {
                return propertyStatement.ParameterList?.Parameters.ToImmutableArray()
                       ?? ImmutableArray<ParameterSyntax>.Empty;
            }

            return ImmutableArray<ParameterSyntax>.Empty;
        }

        /// <summary>
        /// その宣言が戻り値を持つか (Function または Property) を判定する。
        /// Sub や Class は戻り値を持たないため false。
        /// </summary>
        public static bool HasReturnValue(SyntaxNode node)
        {
            var methodStatement = node as MethodStatementSyntax;
            if (methodStatement != null)
            {
                return methodStatement.Kind() == Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.FunctionStatement;
            }

            if (node is PropertyStatementSyntax)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Class / Method / Property の名前と、診断報告に使う位置 (識別子の位置) を取得する。
        /// </summary>
        public static (string Name, Location Location) GetMemberNameAndLocation(SyntaxNode node)
        {
            var classStatement = node as ClassStatementSyntax;
            if (classStatement != null)
            {
                return (classStatement.Identifier.Text, classStatement.Identifier.GetLocation());
            }

            var methodStatement = node as MethodStatementSyntax;
            if (methodStatement != null)
            {
                return (methodStatement.Identifier.Text, methodStatement.Identifier.GetLocation());
            }

            var propertyStatement = node as PropertyStatementSyntax;
            if (propertyStatement != null)
            {
                return (propertyStatement.Identifier.Text, propertyStatement.Identifier.GetLocation());
            }

            return (node.ToString(), node.GetLocation());
        }
    }
}