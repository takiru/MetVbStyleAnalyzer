using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace VbNamespaceAnalyzer
{
    /// <summary>
    /// VB.NET のファイルに記述された Namespace が、
    /// 「RootNamespace + フォルダー階層」から期待される名前空間と一致するかを検証するアナライザー。
    /// IDE0130 (dotnet_style_namespace_match_folder) が VB.NET で正しく機能しない環境向けの代替実装。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class NamespaceMatchFolderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "VBNS0001";

        // VB プロジェクト特有の、コードとして扱うべきでない既定フォルダー
        private static readonly string[] ExcludedFolders = { "My Project", "obj", "bin" };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "名前空間がフォルダー構造と一致していません",
            messageFormat: "名前空間 '{0}' はフォルダー構造と一致していません。{1}",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "VB.NET のファイルの名前空間が、プロジェクトの RootNamespace とフォルダー階層から導かれる名前空間と一致するかを検証します。" +
                         "VB.NET は Namespace ステートメントに RootNamespace を自動的に前置するため、" +
                         "通常はフォルダー名だけを書く (Namespace Hoge) か、Global キーワードで明示的にバイパスする (Namespace Global.Root.Hoge) 必要があります。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0001.severity を設定してください。");

        private static readonly DiagnosticDescriptor MissingNamespaceRule = new DiagnosticDescriptor(
            "VBNS0002",
            title: "名前空間が指定されていません",
            messageFormat: "型 '{0}' に Namespace が指定されていません。{1}",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "VB.NET のファイルの型が Namespace ステートメントで囲まれておらず、" +
                         "プロジェクト直下のファイルも含め、常に明示的な Namespace 宣言を要求します。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.VBNS0002.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule, MissingNamespaceRule);

        public override void Initialize(AnalysisContext context)
        {
            // 自動生成コード (Designer.vb, AssemblyInfo.vb 等) は対象外にする
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(AnalyzeTree);
        }

        private static void AnalyzeTree(SyntaxTreeAnalysisContext context)
        {
            var tree = context.Tree;
            var filePath = tree.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);

            // RootNamespace は .vbproj / Directory.Build.props 側で
            // <CompilerVisibleProperty Include="RootNamespace" /> の指定が必要
            options.TryGetValue("build_property.RootNamespace", out var rootNamespace);
            if (rootNamespace == null)
            {
                rootNamespace = string.Empty;
            }

            // ProjectDir も同様に
            // <CompilerVisibleProperty Include="ProjectDir" /> の指定が必要
            if (!options.TryGetValue("build_property.ProjectDir", out var projectDir)
                || string.IsNullOrEmpty(projectDir))
            {
                // プロジェクトディレクトリが取得できない場合は判定不能なので何もしない
                return;
            }

            var relativeDir = GetRelativeDirectory(projectDir, filePath);
            var folderSegments = relativeDir
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !ExcludedFolders.Contains(segment, StringComparer.OrdinalIgnoreCase))
                .Select(SanitizeIdentifier)
                .ToArray();

            // VB.NET は Namespace ステートメントに RootNamespace を自動的に前置するため、
            // 正しい書き方は以下の2通りになる。
            //   1. 暗黙形式: フォルダー名だけを書く (例: Namespace Hoge)
            //      -> コンパイラが RootNamespace を自動的に前置する
            //   2. Global明示形式: Global キーワードで自動前置をバイパスし、フルパスを自分で書く
            //      (例: Namespace Global.ClassLibrary2.Hoge)
            // "Namespace ClassLibrary2.Hoge" のように Global を使わず RootNamespace を書いてしまうと、
            // 実際には "ClassLibrary2.ClassLibrary2.Hoge" と二重に前置されてしまうので、これは誤りとして扱う。
            var implicitExpected = string.Join(".", folderSegments);

            var fullPathSegments = new[] { rootNamespace }.Concat(folderSegments)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            var explicitGlobalExpected = fullPathSegments.Length == 0
                ? "Global"
                : "Global." + string.Join(".", fullPathSegments);

            if (string.IsNullOrEmpty(implicitExpected) && fullPathSegments.Length == 0)
            {
                // フォルダーもRootNamespaceも空なら比較のしようがない
                return;
            }

            var root = tree.GetRoot(context.CancellationToken);

            // Namespace で囲まれず、ファイル直下に置かれている型を検出する。
            // プロジェクト直下のファイルであっても、チームの規約として
            // 常に明示的な Namespace 宣言を要求する(implicitExpected が空でも対象にする)。
            if (root is CompilationUnitSyntax compilationUnit)
            {
                var suggestion = BuildSuggestion(implicitExpected, explicitGlobalExpected);

                foreach (var member in compilationUnit.Members)
                {
                    var (typeName, location) = GetTypeNameAndLocation(member);
                    if (typeName is null)
                    {
                        continue;
                    }

                    var diagnostic = Diagnostic.Create(
                        MissingNamespaceRule,
                        location,
                        typeName,
                        suggestion);

                    context.ReportDiagnostic(diagnostic);
                }
            }

            foreach (var block in root.DescendantNodes().OfType<NamespaceBlockSyntax>())
            {
                // 型を直接含まない「外側だけ」の Namespace ブロックはスキップし、
                // 実際に型が置かれている最も内側のブロックだけを検証する
                if (!HasDirectTypeMember(block))
                {
                    continue;
                }

                var actualNamespace = GetFullNamespaceName(block);

                var matchesImplicitForm = !string.IsNullOrEmpty(implicitExpected)
                    && string.Equals(actualNamespace, implicitExpected, StringComparison.OrdinalIgnoreCase);
                var matchesGlobalForm =
                    string.Equals(actualNamespace, explicitGlobalExpected, StringComparison.OrdinalIgnoreCase);

                if (!matchesImplicitForm && !matchesGlobalForm)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        block.NamespaceStatement.Name.GetLocation(),
                        actualNamespace,
                        BuildSuggestion(implicitExpected, explicitGlobalExpected));

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static string BuildSuggestion(string implicitExpected, string explicitGlobalExpected)
        {
            // プロジェクト直下 (フォルダーなし) の場合、「フォルダー名だけを書く」暗黙形式は
            // 存在しないため、Global 形式のみを提示する。
            if (string.IsNullOrEmpty(implicitExpected))
            {
                return $"'{explicitGlobalExpected}' のように Global を使って囲んでください";
            }

            return $"'{implicitExpected}' と記述するか、Global を使う場合は '{explicitGlobalExpected}' と記述してください";
        }

        private static (string? Name, Location Location) GetTypeNameAndLocation(SyntaxNode member)
        {
            switch (member)
            {
                case ClassBlockSyntax c:
                    return (c.ClassStatement.Identifier.Text, c.ClassStatement.Identifier.GetLocation());
                case ModuleBlockSyntax m:
                    return (m.ModuleStatement.Identifier.Text, m.ModuleStatement.Identifier.GetLocation());
                case StructureBlockSyntax s:
                    return (s.StructureStatement.Identifier.Text, s.StructureStatement.Identifier.GetLocation());
                case InterfaceBlockSyntax i:
                    return (i.InterfaceStatement.Identifier.Text, i.InterfaceStatement.Identifier.GetLocation());
                case EnumBlockSyntax e:
                    return (e.EnumStatement.Identifier.Text, e.EnumStatement.Identifier.GetLocation());
                case DelegateStatementSyntax d:
                    return (d.Identifier.Text, d.Identifier.GetLocation());
                default:
                    return (null, member.GetLocation());
            }
        }

        private static bool HasDirectTypeMember(NamespaceBlockSyntax block)
        {
            foreach (var member in block.Members)
            {
                if (member is ClassBlockSyntax)
                {
                    return true;
                }
                if (member is ModuleBlockSyntax)
                {
                    return true;
                }
                if (member is StructureBlockSyntax)
                {
                    return true;
                }
                if (member is InterfaceBlockSyntax)
                {
                    return true;
                }
                if (member is EnumBlockSyntax)
                {
                    return true;
                }
                if (member is DelegateStatementSyntax)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetFullNamespaceName(NamespaceBlockSyntax block)
        {
            // 単一ステートメントで "Namespace A.B.C" と書かれている場合も、
            // "Namespace A" -> "Namespace B" のようにネストしている場合も、
            // 祖先を遡ってフルネームを組み立てる
            var names = new List<string>();
            SyntaxNode current = block;

            while (current is NamespaceBlockSyntax nsBlock)
            {
                names.Insert(0, nsBlock.NamespaceStatement.Name.ToString());
                current = nsBlock.Parent;
            }

            return string.Join(".", names);
        }

        private static string GetRelativeDirectory(string projectDir, string filePath)
        {
            var normalizedProjectDir = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullProjectDir = Path.GetFullPath(normalizedProjectDir) + Path.DirectorySeparatorChar;
            var fullFilePath = Path.GetFullPath(filePath);
            var fileDir = Path.GetDirectoryName(fullFilePath) ?? string.Empty;

            if (!fileDir.StartsWith(fullProjectDir, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return fileDir.Substring(fullProjectDir.Length);
        }

        private static string SanitizeIdentifier(string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return segment;
            }

            var sb = new StringBuilder(segment.Length);
            foreach (var c in segment)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            if (char.IsDigit(sb[0]))
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }
    }
}