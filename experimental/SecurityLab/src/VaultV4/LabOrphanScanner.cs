namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// S-class: find loose objects not referenced by metadata (crash leftovers).
/// Does not open packs; only objects/**/*.obj.
/// </summary>
public static class LabOrphanScanner
{
    public sealed class Report
    {
        public required IReadOnlyList<string> OrphanObjectIds { get; init; }
        public int Count => OrphanObjectIds.Count;
        public int Purged { get; init; }
    }

    public static IReadOnlyList<string> ListLooseObjectIds(string vaultRoot)
    {
        var objects = Path.Combine(vaultRoot, "objects");
        if (!Directory.Exists(objects))
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var file in Directory.EnumerateFiles(objects, "*.obj", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            try
            {
                LabParserGuard.EnsureObjectId(name);
                list.Add(name.ToLowerInvariant());
            }
            catch
            {
                // skip invalid names
            }
        }

        return list;
    }

    public static Report Scan(string vaultRoot, IEnumerable<string> knownObjectIds)
    {
        var known = new HashSet<string>(
            knownObjectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.ToLowerInvariant()),
            StringComparer.Ordinal);
        var orphans = ListLooseObjectIds(vaultRoot).Where(id => !known.Contains(id)).ToArray();
        return new Report { OrphanObjectIds = orphans, Purged = 0 };
    }

    /// <summary>Shred orphan loose objects. Returns purge count.</summary>
    public static Report Purge(string vaultRoot, IEnumerable<string> knownObjectIds)
    {
        var scan = Scan(vaultRoot, knownObjectIds);
        var n = 0;
        foreach (var id in scan.OrphanObjectIds)
        {
            try
            {
                LabObjectStore.DeleteEverywhere(vaultRoot, id);
                n++;
            }
            catch
            {
                // best effort
            }
        }

        return new Report { OrphanObjectIds = scan.OrphanObjectIds, Purged = n };
    }
}
