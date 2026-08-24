using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace VbStyleAnalyzer
{
    /// <summary>
    /// ファイル内で最初に宣言されているトップレベルの
    /// Class / Structure / Interface / Module / Enum / Delegate の名前が、
    /// ファイル名と大文字小文字を区別して一致しているかを検証するアナライザー。
    ///
    /// Partial (Class / Structure / Interface / Module のみ対応。Enum / Delegate は
    /// VB.NET の仕様上 Partial にできない) の場合は、ファイル名の最初の "." が現れるまでの
    /// 部分だけが一致していればよい (例: "MyForm.Designer.vb" は "MyForm" という
    /// Partial Class と一致する)。Partial でない場合は、拡張子を除いたファイル名全体と
    /// 完全に一致している必要がある。
    ///
    /// 1ファイルに複数のトップレベル宣言がある場合、全てを一致させることは原理上不可能なため
    /// (どれか1つしかファイル名と一致し得ない)、最初の宣言のみを対象にする。
    /// 他の型の中にネストされた宣言は対象外。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class TypeNameMatchFileNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSA1110";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "型名がファイル名と一致していません",
            messageFormat: "'{0}' の名前がファイル名 '{1}' と一致していません (大文字小文字も含めて一致させてください)",
            category: "Naming",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ファイル内で最初に宣言されているトップレベルの " +
                         "Class / Structure / Interface / Module / Enum / Delegate の名前は、" +
                         "ファイル名と大文字小文字を区別して一致している必要があります。" +
                         "Partial な型の場合は、ファイル名の最初の \".\" までの部分が一致していれば十分です " +
                         "(例: MyForm.Designer.vb は Partial Class MyForm と一致します)。" +
                         "一致させるには、.editorconfig で dotnet_diagnostic.MSA1110.severity を設定してください。");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
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

            var root = tree.GetRoot(context.CancellationToken);

            // トップレベル (CompilationUnit 直下、または Namespace 直下) の宣言のみを対象にする。
            // 他の型にネストされた宣言は対象外。
            var firstTopLevelDeclaration = root.DescendantNodes()
                .FirstOrDefault(IsTopLevelTargetNode);

            if (firstTopLevelDeclaration is null)
            {
                return;
            }

            var (name, location, isPartial) = GetInfo(firstTopLevelDeclaration);
            var fileNameForComparison = GetFileNameForComparison(filePath, isPartial);

            if (string.Equals(name, fileNameForComparison, StringComparison.Ordinal))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, location, name, fileNameForComparison);
            context.ReportDiagnostic(diagnostic);
        }

        private static string GetFileNameForComparison(string filePath, bool isPartial)
        {
            if (!isPartial)
            {
                // 通常の型は、拡張子を除いたファイル名全体と完全に一致している必要がある
                return Path.GetFileNameWithoutExtension(filePath);
            }

            // Partial な型は、ファイル名の最初の "." までの部分が一致していればよい
            // (例: "MyForm.Designer.vb" -> "MyForm")
            var fullFileName = Path.GetFileName(filePath);
            var firstDotIndex = fullFileName.IndexOf('.');

            return firstDotIndex >= 0 ? fullFileName.Substring(0, firstDotIndex) : fullFileName;
        }

        private static bool IsTopLevelTargetNode(SyntaxNode node)
        {
            var isTargetKind = node is ClassBlockSyntax
                || node is StructureBlockSyntax
                || node is InterfaceBlockSyntax
                || node is ModuleBlockSyntax
                || node is EnumBlockSyntax
                || node is DelegateStatementSyntax;

            if (!isTargetKind)
            {
                return false;
            }

            return node.Parent is CompilationUnitSyntax || node.Parent is NamespaceBlockSyntax;
        }

        private static (string Name, Location Location, bool IsPartial) GetInfo(SyntaxNode node)
        {
            switch (node)
            {
                case ClassBlockSyntax c:
                    return (c.ClassStatement.Identifier.ValueText, c.ClassStatement.Identifier.GetLocation(),
                        HasPartialModifier(c.ClassStatement.Modifiers));
                case StructureBlockSyntax s:
                    return (s.StructureStatement.Identifier.ValueText, s.StructureStatement.Identifier.GetLocation(),
                        HasPartialModifier(s.StructureStatement.Modifiers));
                case InterfaceBlockSyntax i:
                    return (i.InterfaceStatement.Identifier.ValueText, i.InterfaceStatement.Identifier.GetLocation(),
                        HasPartialModifier(i.InterfaceStatement.Modifiers));
                case ModuleBlockSyntax m:
                    return (m.ModuleStatement.Identifier.ValueText, m.ModuleStatement.Identifier.GetLocation(),
                        HasPartialModifier(m.ModuleStatement.Modifiers));
                case EnumBlockSyntax e:
                    // Enum は Partial にできない
                    return (e.EnumStatement.Identifier.ValueText, e.EnumStatement.Identifier.GetLocation(), false);
                case DelegateStatementSyntax d:
                    // Delegate は Partial にできない
                    return (d.Identifier.ValueText, d.Identifier.GetLocation(), false);
                default:
                    return (node.ToString(), node.GetLocation(), false);
            }
        }

        private static bool HasPartialModifier(SyntaxTokenList modifiers)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}