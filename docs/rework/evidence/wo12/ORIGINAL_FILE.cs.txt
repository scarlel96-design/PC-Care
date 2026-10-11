using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Crash-aware journal for import transactions (design §14 subset).
/// Phase4: incomplete ObjectsReady carries object id for orphan shred.
/// </summary>
public static class LabVaultJournal
{
    public enum State
    {
        Prepared = 1,
        ObjectsReady = 2,
        MetadataReady = 3,
        Committed = 4,
        Aborted = 5,
        RolledBack = 6
    }

    public sealed class Entry
    {
        public string TxId { get; set; } = "";
        public State State { get; set; }
        public string Subject { get; set; } = "";
        public long Unix { get; set; }
        public long Generation { get; set; }
    }

    public sealed class IncompleteTx
    {
        public required string TxId { get; init; }
        public required State LastState { get; init; }
        public string? ObjectId { get; init; }
    }

    public static string Begin(string vaultRoot, string subject, long generation)
    {
        var tx = Guid.NewGuid().ToString("N")[..16];
        Append(vaultRoot, new Entry
        {
            TxId = tx,
            State = State.Prepared,
            Subject = Sanitize(subject),
            Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Generation = generation
        });
        return tx;
    }

    public static void Mark(string vaultRoot, string txId, State state, string subject, long generation) =>
        Append(vaultRoot, new Entry
        {
            TxId = txId,
            State = state,
            Subject = Sanitize(subject),
            Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Generation = generation
        });

    public static IReadOnlyList<string> ListIncomplete(string vaultRoot) =>
        ListIncompleteDetailed(vaultRoot).Select(x => x.TxId).ToArray();

    public static IReadOnlyList<IncompleteTx> ListIncompleteDetailed(string vaultRoot)
    {
        var path = PathOf(vaultRoot);
        if (!File.Exists(path))
        {
            return Array.Empty<IncompleteTx>();
        }

        // Keep last entry per tx + last known object id from any ObjectsReady line (kill after MetadataReady).
        var last = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var lastObjectId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            try
            {
                var e = JsonSerializer.Deserialize<Entry>(line);
                if (e is null || string.IsNullOrWhiteSpace(e.TxId))
                {
                    continue;
                }

                last[e.TxId] = e;
                if (e.State == State.ObjectsReady
                    && e.Subject.StartsWith("obj:", StringComparison.Ordinal)
                    && e.Subject.Length > 4)
                {
                    lastObjectId[e.TxId] = e.Subject["obj:".Length..];
                }
            }
            catch
            {
                // skip
            }
        }

        var list = new List<IncompleteTx>();
        foreach (var (txId, e) in last)
        {
            if (e.State is State.Committed or State.Aborted or State.RolledBack)
            {
                continue;
            }

            string? objectId = null;
            if (e.State == State.ObjectsReady && e.Subject.StartsWith("obj:", StringComparison.Ordinal))
            {
                objectId = e.Subject["obj:".Length..];
            }
            else if (lastObjectId.TryGetValue(txId, out var histObj))
            {
                // S-class: recover orphan after crash past ObjectsReady (e.g. MetadataReady)
                objectId = histObj;
            }

            list.Add(new IncompleteTx
            {
                TxId = txId,
                LastState = e.State,
                ObjectId = objectId
            });
        }

        return list;
    }

    /// <summary>Abort incomplete txs and delete orphan object ids when known.</summary>
    public static int RecoverOrphans(string vaultRoot, long generation)
    {
        var incomplete = ListIncompleteDetailed(vaultRoot);
        var n = 0;
        foreach (var tx in incomplete)
        {
            if (!string.IsNullOrWhiteSpace(tx.ObjectId))
            {
                try
                {
                    LabObjectStore.DeleteEverywhere(vaultRoot, tx.ObjectId);
                }
                catch
                {
                    // best effort
                }
            }

            Mark(vaultRoot, tx.TxId, State.Aborted, "recover-orphan", generation);
            n++;
        }

        return n;
    }

    private static void Append(string vaultRoot, Entry entry)
    {
        try
        {
            var dir = Path.Combine(vaultRoot, "journal");
            Directory.CreateDirectory(dir);
            var path = PathOf(vaultRoot);
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            // durable-ish append (design §14 flush subset)
            using var fs = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.WriteThrough);
            var bytes = Encoding.UTF8.GetBytes(line);
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }
        catch
        {
            // best effort
        }
    }

    private static string PathOf(string vaultRoot) =>
        Path.Combine(vaultRoot, "journal", "tx.avj");

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "-";
        }

        var cleaned = new string(s.Where(c => !char.IsControl(c) && c != '\r' && c != '\n').ToArray());
        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }
}
