using System;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.Helpers;

internal sealed record LibraryTransferProgress(Guid Id, long Downloaded, long Total, bool Preparing = false);
internal sealed record LibraryProgressSnapshot(bool Visible, bool Indeterminate, double Percent, string Text);

// Retain completed transfers until overlapping downloads finish, keeping totals stable.
internal sealed class LibraryDownloadProgress
{
    readonly Dictionary<Guid, LibraryTransferProgress> _session = [];

    public LibraryProgressSnapshot Update(IEnumerable<LibraryTransferProgress> transfers,
        IEnumerable<LibraryTransferProgress>? observed = null)
    {
        var active = transfers.ToArray();
        if (active.Length == 0)
        {
            _session.Clear();
            return new(false, false, 0, "");
        }
        if (observed is not null)
            foreach (var item in observed) _session[item.Id] = item;
        foreach (var item in active) _session[item.Id] = item;
        var bytes = _session.Values.Sum(item => Math.Max(0, item.Downloaded));
        var total = _session.Values.Sum(item => Math.Max(0, item.Total));
        var preparing = active.All(item => item.Preparing);
        var unknown = active.Any(item => item.Total <= 0);
        var percent = total > 0 ? Math.Clamp(bytes * 100.0 / total, 0, 100) : 0;
        var text = preparing ? "Preparing files…"
            : unknown ? $"Downloading · {bytes / 1048576.0:0.0} MiB received"
            : $"Downloading · {percent:0}% · {bytes / 1048576.0:0.0} / {total / 1048576.0:0.0} MiB";
        return new(true, preparing || unknown, percent, text);
    }
}
