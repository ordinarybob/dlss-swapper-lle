using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DLSS_Swapper.Data.Streamline;

namespace DlssSwapper.Linux.Tests;

internal static class StreamlineSafetyTests
{
    public static byte[] DllBytes(string label)
    {
        var bytes = new byte[1024];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        U16(0, 0x5a4d); U32(60, 128); U32(128, 0x4550);
        U16(132, 0x8664); U16(134, 1); U16(148, 240); U16(150, 0x2022);
        U16(152, 0x20b); U32(184, 4096); U32(188, 512); U32(208, 8192); U32(212, 512);
        U16(220, 3); U32(260, 16);
        Encoding.ASCII.GetBytes(".text").CopyTo(bytes, 392);
        U32(400, 512); U32(404, 4096); U32(408, 512); U32(412, 512); U32(428, 0x60000020);
        Encoding.UTF8.GetBytes(label).CopyTo(bytes, 512);
        return bytes;
    }
    public static void WriteDll(string path, string label) => File.WriteAllBytes(path, DllBytes(label));
    public static string ReadLabel(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path).AsSpan(512)).TrimEnd('\0');
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        TestPreparationFailure();
        TestMultiDirectoryCommit();
        using var fixture = new Fixture();
        var targets = fixture.Targets;
        var first = StreamlineComponentSet.UpdateExisting(fixture.Source, targets);
        Check(first.Success, first.Message);
        var timestamp = File.GetLastWriteTimeUtc(targets[0]);
        var noOp = StreamlineComponentSet.UpdateExisting(fixture.Source, targets);
        Check(noOp.Success && noOp.ComponentCount == 0 && File.GetLastWriteTimeUtc(targets[0]) == timestamp, "Identical set was rewritten.");

        // Validation failure never reaches a target or consumes the original backup.
        File.WriteAllText(Path.Combine(fixture.Source, Path.GetFileName(targets[1])), "not a DLL");
        var invalid = StreamlineComponentSet.UpdateExisting(fixture.Source, targets);
        Check(!invalid.Success && targets.All(path => ReadLabel(path) == "new"), "Invalid binary changed installed components.");
        fixture.SetSource("newer");

        // An outside edit blocks only that entry; rollback continues for all remaining entries.
        var conflict = StreamlineComponentSet.UpdateExisting(fixture.Source, targets, (index, _) =>
        {
            if (index == 2) { WriteDll(targets[0], "external"); throw new IOException("Injected replacement failure"); }
        });
        Check(!conflict.Success && ReadLabel(targets[0]) == "external" && ReadLabel(targets[1]) == "new", "Rollback overwrote conflict or failed to continue.");
        Check(StreamlineComponentSet.HasPendingRecovery(fixture.Game), "Conflict lost its recovery journal.");
        Check(Directory.GetFiles(fixture.Game, "*.streamline-rollback-*").Length == 3, "Conflict deleted recovery images.");
        Check(!StreamlineComponentSet.UpdateExisting(fixture.Source, targets).Success, "Pending recovery allowed another update.");
        Check(!StreamlineComponentSet.RecoverInterrupted(fixture.Game).Success && ReadLabel(targets[0]) == "external", "Recovery overwrote outside changes.");
        WriteDll(targets[0], "newer"); // User resolves conflict back to the transaction's known image.
        var recovery = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(recovery.Success && targets.All(path => ReadLabel(path) == "new"), recovery.Message);

        // Real I/O failure during rollback must retain everything needed to retry.
        FileStream? held = null;
        try
        {
            var failed = StreamlineComponentSet.UpdateExisting(fixture.Source, targets, (index, _) =>
            {
                if (index == 1) { held = new FileStream(targets[0], FileMode.Open, FileAccess.Read, FileShare.None); throw new IOException("Injected failure with locked rollback target"); }
            });
            Check(!failed.Success && StreamlineComponentSet.HasPendingRecovery(fixture.Game), "Rollback I/O failure lost recovery.");
        }
        finally { held?.Dispose(); }
        Check(StreamlineComponentSet.RecoverInterrupted(fixture.Game).Success, "Retry after locked rollback failed.");

        // A second caller cannot overlap even when it supplies a smaller component subset.
        StreamlineComponentOperationResult? overlap = null;
        var owner = StreamlineComponentSet.UpdateExisting(fixture.Source, targets, (index, _) =>
        {
            if (index == 0) overlap = Task.Run(() => StreamlineComponentSet.RestoreOriginals([targets[0]])).GetAwaiter().GetResult();
        });
        Check(owner.Success && overlap is { Success: false }, "Overlapping mutation was not excluded.");
        Check(targets.All(path => ReadLabel(path + StreamlineComponentSet.BackupSuffix) == "old"), "Repeated operations changed original backups.");

        TestInterruptedPreparation(fixture);
        TestInterruptedReplacement(fixture);
        TestCommittedCleanup(fixture);
        Check(StreamlineComponentSet.RestoreOriginals(targets).Success && targets.All(path => ReadLabel(path) == "old"), "Final original restore failed.");
    }

    static void TestPreparationFailure()
    {
        using var fixture = new Fixture();
        // A directory occupying the permanent-backup name causes a real I/O failure after both copies are staged.
        Directory.CreateDirectory(fixture.Targets[0] + StreamlineComponentSet.BackupSuffix);
        var failed = StreamlineComponentSet.UpdateExisting(fixture.Source, fixture.Targets);
        Check(!failed.Success && fixture.Targets.All(path => ReadLabel(path) == "old"), "Preparation failure changed a target.");
        Check(!StreamlineComponentSet.HasPendingRecovery(fixture.Game), "Safely reverted preparation retained a journal.");
        Check(!Directory.EnumerateFiles(fixture.Game).Any(path => path.Contains(".streamline-rollback-") || path.Contains(".streamline-incoming-") || path.Contains(".streamline-original-")), "Preparation failure leaked temporary copies.");
    }

    static void TestMultiDirectoryCommit()
    {
        using var fixture = new Fixture();
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Game, "nested")).FullName;
        var second = Path.Combine(nested, Path.GetFileName(fixture.Targets[0]));
        WriteDll(second, "old");
        var journal = MakeJournal(fixture);
        journal.Entries = [journal.Entries[0], new StreamlineComponentSet.Entry
        {
            Target = second, OldHash = journal.Entries[0].OldHash, NewHash = journal.Entries[0].NewHash,
        }];
        foreach (var entry in journal.Entries)
        {
            File.Copy(entry.Target, entry.Target + ".streamline-rollback-" + journal.Id);
            WriteDll(entry.Target, "interrupted-new");
        }
        // Only the canonical journal becomes committed; discovery replicas intentionally remain prepared.
        File.WriteAllText(Path.Combine(nested, ".streamline-transaction.json"), JsonSerializer.Serialize(journal));
        journal.Committed = true;
        Persist(fixture, journal);
        var recovered = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(recovered.Success && journal.Entries.All(entry => ReadLabel(entry.Target) == "interrupted-new"), "Replica journal rolled back a committed multi-directory operation.");
        Check(!StreamlineComponentSet.HasPendingRecovery(fixture.Game), "Multi-directory journal cleanup was incomplete.");

        // A remaining replica without its authoritative journal is not proof
        // of completed cleanup; preserve it rather than claim recovery succeeded.
        var replica = Path.Combine(nested, ".streamline-transaction.json");
        File.WriteAllText(replica, JsonSerializer.Serialize(journal));
        var incomplete = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(!incomplete.Success && File.Exists(replica)
            && journal.Entries.All(entry => ReadLabel(entry.Target) == "interrupted-new"),
            "Missing canonical journal was treated as successful recovery.");
    }

    static void TestInterruptedPreparation(Fixture fixture)
    {
        var journal = MakeJournal(fixture);
        Persist(fixture, journal);
        // Simulate a process stopping partway through preparation: one partial copy, no live changes.
        File.WriteAllText(journal.Entries[0].Target + ".streamline-rollback-" + journal.Id, "partial");
        var recovered = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(recovered.Success && !StreamlineComponentSet.HasPendingRecovery(fixture.Game), "Interrupted preparation was not cleaned safely.");
        Check(!Directory.EnumerateFiles(fixture.Game).Any(path => path.Contains(journal.Id)), "Partial preparation artifact leaked.");
    }

    static void TestInterruptedReplacement(Fixture fixture)
    {
        var journal = MakeJournal(fixture);
        foreach (var entry in journal.Entries) File.Copy(entry.Target, entry.Target + ".streamline-rollback-" + journal.Id);
        Persist(fixture, journal);
        WriteDll(journal.Entries[0].Target, "interrupted-new");
        var recovered = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(recovered.Success && fixture.Targets.All(path => ReadLabel(path) == "newer"), "Interrupted replacement was not rolled back.");
    }

    static void TestCommittedCleanup(Fixture fixture)
    {
        var journal = MakeJournal(fixture);
        journal.Committed = true;
        Persist(fixture, journal);
        WriteDll(journal.Entries[0].Target, "interrupted-new");
        var recovered = StreamlineComponentSet.RecoverInterrupted(fixture.Game);
        Check(recovered.Success && ReadLabel(journal.Entries[0].Target) == "interrupted-new", "Committed operation was rolled back.");
    }

    static StreamlineComponentSet.Journal MakeJournal(Fixture fixture) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Entries = fixture.Targets.Select(path => new StreamlineComponentSet.Entry
        {
            Target = path, OldHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            NewHash = Convert.ToHexString(SHA256.HashData(DllBytes("interrupted-new"))),
        }).ToList(),
    };
    static void Persist(Fixture fixture, StreamlineComponentSet.Journal journal) =>
        File.WriteAllText(Path.Combine(fixture.Game, ".streamline-transaction.json"), JsonSerializer.Serialize(journal));

    sealed class Fixture : IDisposable
    {
        readonly string root = Path.Combine(Path.GetTempPath(), "streamline-safety-" + Guid.NewGuid().ToString("N"));
        public string Game { get; }
        public string Source { get; }
        public string[] Targets { get; }
        public Fixture()
        {
            Game = Directory.CreateDirectory(Path.Combine(root, "game")).FullName;
            Source = Directory.CreateDirectory(Path.Combine(root, "sdk")).FullName;
            Targets = StreamlineComponentSet.FileNames.Take(3).Select(name => Path.Combine(Game, name)).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var path in Targets) WriteDll(path, "old");
            SetSource("new");
        }
        public void SetSource(string label) { foreach (var path in Targets) WriteDll(Path.Combine(Source, Path.GetFileName(path)), label); }
        public void Dispose() => Directory.Delete(root, true);
    }
}
