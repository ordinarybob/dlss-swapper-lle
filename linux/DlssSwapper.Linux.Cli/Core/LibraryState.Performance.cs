namespace DlssSwapper.Linux.Cli.Core;

public sealed partial class LinuxLibraryState
{
    public void ResetPerformanceDefaults()
    {
        ScanConcurrency = 15;
        ArtworkConcurrency = 38;
        BatchSwapConcurrency = 15;
        UiCollectionBatchSize = 550;
    }
}
