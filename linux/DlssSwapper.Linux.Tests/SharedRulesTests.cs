using System.Security.Cryptography;
using System.Text;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Shared;

namespace DlssSwapper.Linux.Tests;

internal static class SharedRulesTests
{
    internal static void Run()
    {
        // Compare the extracted calculation with the frozen pre-refactor Windows algorithm.
        foreach (var width in new double[] { double.NaN, double.PositiveInfinity, -1, 0, 0.5, 44, 54, 100, 732, 1000, 1920, 3840 })
        foreach (var padding in new double[] { double.NaN, -1, 0, 10, 100 })
        foreach (var scale in new double[] { double.NaN, 0, 1, 1.25, 1.5, 1.75, 2, 2.5 })
        for (var size = 0; size <= 11; size++)
        {
            var expected = BaselineGridLayout.Calculate(width, padding, size, scale);
            var actual = DlssSwapper.Shared.ResponsiveGridLayout.Calculate(width, padding, size, scale);
            if (expected.ColumnCount != actual.ColumnCount || expected.CellWidth != actual.CellWidth
                || expected.CardWidth != actual.CardWidth || expected.CardHeight != actual.CardHeight
                || expected.CellHeight != actual.CellHeight) throw new Exception("Grid extraction changed geometry.");
            if (padding == 0)
            {
                var linux = DlssSwapper.Linux.Cli.Core.ResponsiveGridLayout.Calculate(width, size, scale);
                if (actual.CellWidth != linux.CellWidth || actual.CardHeight != linux.CardHeight)
                    throw new Exception("Linux geometry diverged from shared rules.");
            }
        }

        if (ScanPatternRules.BuiltInPatterns.Count != 82) throw new Exception("Built-in pattern count changed.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', ScanPatternRules.BuiltInPatterns))));
        if (fingerprint != "079034F26AB15CA1EBF03D508DC2BF27EC47A69DABC8A66595C2B32FB4809A67")
            throw new Exception("Built-in pattern contents or ordering changed.");
        var patterns = new[] { "*/binaries/*", "Game/binaries/win64", "*/binaries/*", "*/custom/deep/bin", "Other/custom/deep/bin", "../escape", "C:/root", "*/CUSTOM/deep/bin" };
        var custom = FastScanPatternIndex.NormalizeCustomPatterns(patterns);
        if (!custom.SequenceEqual(new[] { "*/custom/deep/bin" })) throw new Exception("Pattern normalization/subsumption changed.");
        var retained = ScanPatternRules.RemoveSubsumed(new[] { "", "*", "*/*", "*/custom/deep", "name/custom/deep", "name/custom/deep/extra" }, p => p, '/');
        if (!retained.SequenceEqual(new[] { "", "*", "*/*", "*/custom/deep", "name/custom/deep/extra" })) throw new Exception("Wildcard depth semantics changed.");
        foreach (var pattern in ScanPatternRules.BuiltInPatterns)
        {
            var reduced = ScanPatternRules.RemoveSubsumed(ScanPatternRules.BuiltInPatterns, p => p, '/');
            if (!reduced.Contains(pattern)) throw new Exception("Built-in antichain changed.");
        }
        if (ScanPatternRules.TryCreateAdaptivePattern(Path.Combine("..", "outside"), '/', true, out _))
            throw new Exception("Adaptive pattern escaped game root.");
        if (!ScanPatternRules.TryCreateAdaptivePattern(Path.Combine("Game", "Custom", "Bin"), '/', true, out var adaptive)
            || adaptive != "*/custom/bin") throw new Exception("Adaptive Linux normalization changed.");
        if (!ScanPatternRules.TryCreateAdaptivePattern(Path.Combine("Game", "Custom", "Bin"), Path.DirectorySeparatorChar, false, out adaptive)
            || adaptive != string.Join(Path.DirectorySeparatorChar, "*", "Custom", "Bin")) throw new Exception("Adaptive Windows casing changed.");
    }
}
