using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Crash-aware journal for import transactions (design §14 subset).
/// Journal entries do not authorize deletion of encrypted objects.
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
        foreach (var e in ReadValidated(path))
        {
            last[e.TxId] = e;
            if (e.State == State.ObjectsReady && e.Subject.StartsWith("obj:", StringComparison.Ordinal))
                lastObjectId[e.TxId] = e.Subject[4..];
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

    /// <summary>Reconcile references after authenticated metadata and commit verification. Preserve all object bytes.</summary>
    public static int RecoverOrphans(string vaultRoot, long generation, IEnumerable<string>? authenticatedObjectIds = null)
    {
        if (authenticatedObjectIds is null) return 0;
        var known = authenticatedObjectIds.ToHashSet(StringComparer.Ordinal);
        var reconciled = 0;
        foreach (var tx in ListIncompleteDetailed(vaultRoot))
        {
            if (tx.ObjectId is not null && known.Contains(tx.ObjectId))
            {
                Mark(vaultRoot, tx.TxId, State.Committed, "authenticated-reference-preserved", generation);
                reconciled++;
            }
        }
        return reconciled;
    }

    private static readonly object JournalGate = new();
    private static IReadOnlyList<Entry> ReadValidated(string path)
    {
        var entries = new List<Entry>();
        var states = new Dictionary<string, State>(StringComparer.Ordinal);
        if (!File.Exists(path)) return entries;
        if (new FileInfo(path).Length > 32L * 1024 * 1024) throw new InvalidDataException("journal budget exceeded");
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.Length > 4096) throw new InvalidDataException("invalid journal record length");
            var entry = JsonSerializer.Deserialize<Entry>(line) ?? throw new InvalidDataException("null journal entry");
            Validate(entry, states);
            states[entry.TxId] = entry.State;
            entries.Add(entry);
        }
        return entries;
    }
    private static void Validate(Entry entry, Dictionary<string, State> states)
    {
        if (entry.TxId is null || entry.TxId.Length != 16 || !entry.TxId.All(Uri.IsHexDigit)
            || !Enum.IsDefined(entry.State) || entry.Generation < 0 || entry.Subject is null)
            throw new InvalidDataException("invalid journal contract");
        if (!states.TryGetValue(entry.TxId, out var old))
        {
            if (entry.State != State.Prepared) throw new InvalidDataException("journal transaction lacks Prepared");
            return;
        }
        var allowed = old switch
        {
            State.Prepared => entry.State is State.ObjectsReady or State.Aborted or State.RolledBack,
            State.ObjectsReady => entry.State is State.MetadataReady or State.Committed or State.Aborted or State.RolledBack,
            State.MetadataReady => entry.State is State.Committed or State.Aborted or State.RolledBack,
            _ => false
        };
        if (!allowed) throw new InvalidDataException("invalid journal state transition");
    }
    private static void Append(string vaultRoot, Entry entry)
    {
        lock (JournalGate)
        {
            var directory = Path.Combine(vaultRoot, "journal");
            Hardening.VaultExportWriter.ValidateAncestors(directory);
            using var parent = Hardening.VaultFileBoundary.PinDirectory(directory, create: true);
            // Hold a distinct cross-process writer lease while validating and appending.
            using var lease = Hardening.VaultFileBoundary.OpenExclusive(directory, "writer.lock",
                !File.Exists(Path.Combine(directory, "writer.lock")));
            var path = PathOf(vaultRoot);
            var entries = ReadValidated(path);
            var states = new Dictionary<string, State>(StringComparer.Ordinal);
            foreach (var previous in entries) states[previous.TxId] = previous.State;
            Validate(entry, states);
            using var file = Hardening.VaultFileBoundary.OpenExclusive(directory, "tx.avj", !File.Exists(path));
            file.Stream.Position = file.Stream.Length;
            file.Stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n"));
            file.Stream.Flush(true);
            file.Validate(); parent.Validate();
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
