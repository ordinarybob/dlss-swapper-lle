using DLSS_Swapper.Data;

internal static class AssetDiscoveryTests
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("streamline-discovery-tests-").FullName;
        try
        {
            var definitions = GameAssetPathIndex.GetAssetFiles().ToArray()
                .Where(file => file.FileName.StartsWith("sl.", StringComparison.Ordinal)).ToArray();
            if (definitions.Length != 11) throw new Exception("Expected eleven retained SDK components.");
            foreach (var file in definitions)
            {
                File.WriteAllText(Path.Combine(root, file.FileName), "non-executable discovery fixture");
                if (!GameAssetPathIndex.TryGetAssetType(file.FileName.ToUpperInvariant(), out var type) || type != file.AssetType)
                    throw new Exception("Streamline filename recognition is not case insensitive.");
            }
            var scan = GameAssetPathIndex.PrepareFind(root);
            var candidates = await scan.ExecuteCandidatesAsync();
            var exhaustive = await scan.ExecuteAsync();
            foreach (var results in new[] { candidates, exhaustive })
                if (results.Count != 11 || definitions.Any(file => !results.Any(result => result.AssetType == file.AssetType)))
                    throw new Exception("Streamline-only folder was not fully discovered.");

            var nested = Directory.CreateDirectory(Path.Combine(root, "unusual", "deep", "location")).FullName;
            File.WriteAllText(Path.Combine(nested, "sl.reflex.dll"), "nested fixture");
            File.WriteAllText(Path.Combine(nested, "nvngx_dlss.dll"), "ordinary fixture");
            File.WriteAllText(Path.Combine(nested, "unrelated.dll"), "ignored fixture");
            exhaustive = await GameAssetPathIndex.PrepareFind(root).ExecuteAsync();
            if (exhaustive.Count != 13 || exhaustive.Count(asset => asset.AssetType == GameAssetType.DLSS) != 1)
                throw new Exception("Exhaustive mixed-folder discovery lost or misclassified components.");
            Console.WriteLine("Asset discovery: production candidate/exhaustive scanners find all eleven SDK components and mixed nested DLLs in isolated folders (not database/UI proof).");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
