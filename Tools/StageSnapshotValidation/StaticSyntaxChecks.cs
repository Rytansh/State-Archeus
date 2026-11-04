using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class StaticSyntaxChecks
{
    public static int Run(string root)
    {
        int files = 0;
        int errors = 0;
        string catalog = File.ReadAllText(
            Path.Combine(root, "Assets/Battle/Simulation/Snapshots/SnapshotAotCatalog.cs")
        );
        foreach (
            string path in Directory.EnumerateFiles(
                Path.Combine(root, "Assets"),
                "*.cs",
                SearchOption.AllDirectories
            )
        )
        {
            files++;
            var tree = CSharpSyntaxTree.ParseText(
                File.ReadAllText(path),
                new CSharpParseOptions(LanguageVersion.CSharp9),
                path
            );
            foreach (
                Diagnostic diagnostic in tree.GetDiagnostics()
                    .Where(item => item.Severity == DiagnosticSeverity.Error)
            )
            {
                Console.Error.WriteLine(diagnostic);
                errors++;
            }
            string relative = Path.GetRelativePath(Path.Combine(root, "Assets"), path)
                .Replace('\\', '/');
            if (
                relative.StartsWith("Testing/", StringComparison.Ordinal)
                || relative.StartsWith("Other/", StringComparison.Ordinal)
            )
                continue;
            foreach (
                StructDeclarationSyntax type in tree.GetRoot()
                    .DescendantNodes()
                    .OfType<StructDeclarationSyntax>()
            )
            {
                if (
                    type.BaseList == null
                    || !type.BaseList.Types.Any(item =>
                        item.Type.ToString() == "IComponentData"
                        || item.Type.ToString() == "IBufferElementData"
                    )
                )
                    continue;
                string ns = string.Join(
                    ".",
                    type.Ancestors()
                        .OfType<NamespaceDeclarationSyntax>()
                        .Reverse()
                        .Select(item => item.Name.ToString())
                );
                string parents = string.Join(
                    ".",
                    type.Ancestors()
                        .OfType<TypeDeclarationSyntax>()
                        .Reverse()
                        .Select(item => item.Identifier.ValueText)
                );
                string name = string.Join(
                    ".",
                    new[] { ns, parents, type.Identifier.ValueText }.Where(item => item.Length != 0)
                );
                if (!catalog.Contains("typeof(global::" + name + ")", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"AOT snapshot codec is missing for {name} ({path})");
                    errors++;
                }
            }
        }
        Console.WriteLine(
            $"C# 9 syntax checked: {files} files, {errors} errors. This is not Unity compilation."
        );
        return errors == 0 ? 0 : 1;
    }
}
