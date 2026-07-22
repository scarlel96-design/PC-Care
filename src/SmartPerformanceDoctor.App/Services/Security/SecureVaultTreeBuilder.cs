using SmartPerformanceDoctor.App.Models.Security;

namespace SmartPerformanceDoctor.App.Services.Security;

internal static class SecureVaultTreeBuilder
{
    public static IReadOnlyList<SecureVaultBrowsableItem> Build(
        IReadOnlyList<SecureVaultEntry> entries,
        string? bundleId,
        string relativePrefix)
    {
        if (bundleId is null)
        {
            return BuildRoot(entries);
        }

        return BuildBundleLevel(entries, bundleId, NormalizePrefix(relativePrefix));
    }

    private static IReadOnlyList<SecureVaultBrowsableItem> BuildRoot(IReadOnlyList<SecureVaultEntry> entries)
    {
        var items = new List<SecureVaultBrowsableItem>();
        var roots = entries.Where(e => e.Kind == SecureVaultEntryKind.FolderRoot).OrderBy(e => e.DisplayLabel).ToArray();
        var duplicateNames = roots
            .GroupBy(r => r.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            var members = entries.Where(e => e.BundleId == root.BundleId && e.Kind == SecureVaultEntryKind.FolderMember).ToArray();
            var displayName = duplicateNames.Contains(root.DisplayLabel)
                ? $"{root.DisplayLabel} — {root.OriginalPath}"
                : root.DisplayLabel;
            items.Add(new SecureVaultBrowsableItem
            {
                Key = $"root:{root.EntryId}",
                DisplayName = displayName,
                Kind = SecureVaultBrowsableKind.FolderRoot,
                EntryId = root.EntryId,
                BundleId = root.BundleId,
                ItemCount = members.Length,
                TotalSize = members.Sum(m => m.OriginalSize),
                OriginalPath = root.OriginalPath,
                IsSealedAtOrigin = root.IsSealedAtOrigin,
                IconGlyph = "🔒📁",
                DetailLine = $"{members.Length}개 파일 · {root.OriginalPath ?? "원본 경로 없음"}"
            });
        }

        // Vault v4 stores a folder import as files with a shared relative-path root.
        // Represent that root explicitly so the user sees the same folder/file model as v3.
        var inferredFolders = entries
            .Where(e => e.Kind == SecureVaultEntryKind.StandaloneFile && HasPathRoot(e.RelativePath))
            .GroupBy(e => GetPathRoot(e.RelativePath!), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var folder in inferredFolders)
        {
            var members = folder.ToArray();
            items.Add(new SecureVaultBrowsableItem
            {
                Key = $"path:{folder.Key}",
                DisplayName = folder.Key,
                Kind = SecureVaultBrowsableKind.FolderRoot,
                BundleId = $"path:{folder.Key}",
                ItemCount = members.Length,
                TotalSize = members.Sum(member => member.OriginalSize),
                IconGlyph = "🔒📁",
                DetailLine = $"폴더 · {members.Length}개 파일 · {members.Sum(member => member.OriginalSize):N0} bytes"
            });
        }

        foreach (var file in entries
                     .Where(e => e.Kind == SecureVaultEntryKind.StandaloneFile && !HasPathRoot(e.RelativePath))
                     .OrderBy(e => e.DisplayLabel))
        {
            items.Add(ToFileItem(file));
        }

        var legacyGroups = entries
            .Where(e => e.Kind == SecureVaultEntryKind.LegacyFolderFile)
            .GroupBy(e => e.DisplayLabel.Split('/')[0], StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);
        foreach (var group in legacyGroups)
        {
            var members = group.ToArray();
            items.Add(new SecureVaultBrowsableItem
            {
                Key = $"legacy:{group.Key}",
                DisplayName = group.Key,
                Kind = SecureVaultBrowsableKind.FolderRoot,
                BundleId = $"legacy:{group.Key}",
                ItemCount = members.Length,
                TotalSize = members.Sum(m => m.OriginalSize),
                IconGlyph = "🔒📁",
                DetailLine = $"{members.Length}개 파일 (이전 형식)"
            });
        }

        return items;
    }

    private static IReadOnlyList<SecureVaultBrowsableItem> BuildBundleLevel(
        IReadOnlyList<SecureVaultEntry> entries,
        string bundleId,
        string relativePrefix)
    {
        IEnumerable<SecureVaultEntry> members;
        if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            var folderName = bundleId["legacy:".Length..];
            members = entries
                .Where(e => e.Kind == SecureVaultEntryKind.LegacyFolderFile && e.DisplayLabel.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase))
                .Select(e => MapLegacyMember(e, folderName));
        }
        else if (bundleId.StartsWith("path:", StringComparison.Ordinal))
        {
            var folderName = bundleId["path:".Length..];
            members = entries
                .Where(e => e.Kind == SecureVaultEntryKind.StandaloneFile
                    && e.RelativePath?.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(e => CloneWithLabel(e, e.DisplayLabel, e.RelativePath![(folderName.Length + 1)..]));
        }
        else
        {
            members = entries.Where(e => e.BundleId == bundleId && e.Kind == SecureVaultEntryKind.FolderMember);
        }

        var scoped = members
            .Select(m => new
            {
                Entry = m,
                Relative = NormalizeRelative(m.RelativePath ?? m.DisplayLabel)
            })
            .Where(x => string.IsNullOrEmpty(relativePrefix) || x.Relative.StartsWith(relativePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => new
            {
                x.Entry,
                Remainder = string.IsNullOrEmpty(relativePrefix)
                    ? x.Relative
                    : x.Relative[relativePrefix.Length..]
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Remainder))
            .ToArray();

        var items = new List<SecureVaultBrowsableItem>();
        var subFolders = scoped
            .Where(x => x.Remainder.Contains('/'))
            .Select(x => x.Remainder.Split('/')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);
        foreach (var folder in subFolders)
        {
            var prefix = string.IsNullOrEmpty(relativePrefix) ? folder + "/" : relativePrefix + folder + "/";
            var folderMembers = scoped.Where(x => x.Remainder.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)).ToArray();
            items.Add(new SecureVaultBrowsableItem
            {
                Key = $"folder:{bundleId}:{prefix}",
                DisplayName = folder,
                Kind = SecureVaultBrowsableKind.SubFolder,
                BundleId = bundleId,
                RelativePrefix = prefix,
                ItemCount = folderMembers.Length,
                TotalSize = folderMembers.Sum(m => m.Entry.OriginalSize),
                IconGlyph = "📁",
                DetailLine = $"{folderMembers.Length}개 항목"
            });
        }

        foreach (var file in scoped.Where(x => !x.Remainder.Contains('/')).OrderBy(x => x.Entry.DisplayLabel))
        {
            items.Add(ToFileItem(CloneWithLabel(file.Entry, file.Remainder)));
        }

        return items;
    }

    private static SecureVaultBrowsableItem ToFileItem(SecureVaultEntry file) =>
        new()
        {
            Key = $"file:{file.EntryId}",
            DisplayName = file.DisplayLabel,
            Kind = SecureVaultBrowsableKind.File,
            EntryId = file.EntryId,
            BundleId = file.BundleId,
            RelativePrefix = file.RelativePath ?? "",
            ItemCount = 1,
            TotalSize = file.OriginalSize,
            OriginalPath = file.OriginalPath,
            IsSealedAtOrigin = file.IsSealedAtOrigin,
            IconGlyph = file.IsSealedAtOrigin ? "🔒📄" : "📄",
            DetailLine = $"{file.OriginalSize:N0} bytes · {file.OriginalPath ?? "원본 경로 없음"}"
        };

    private static bool HasPathRoot(string? path) =>
        !string.IsNullOrWhiteSpace(path) && NormalizeRelative(path).Contains('/');

    private static string GetPathRoot(string path) =>
        NormalizeRelative(path).Split('/', 2)[0];

    private static string NormalizePrefix(string prefix) =>
        string.IsNullOrWhiteSpace(prefix) ? "" : prefix.Replace('\\', '/').TrimEnd('/') + "/";

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static SecureVaultEntry MapLegacyMember(SecureVaultEntry entry, string folderName)
    {
        var relative = entry.DisplayLabel[(folderName.Length + 1)..];
        return CloneWithLabel(entry, relative, relative);
    }

    private static SecureVaultEntry CloneWithLabel(SecureVaultEntry entry, string displayLabel, string? relativePath = null) =>
        new()
        {
            EntryId = entry.EntryId,
            DisplayLabel = displayLabel,
            ShardName = entry.ShardName,
            OriginalSize = entry.OriginalSize,
            AddedAt = entry.AddedAt,
            IsFolderBundle = entry.IsFolderBundle,
            Kind = entry.Kind,
            BundleId = entry.BundleId,
            RelativePath = relativePath ?? entry.RelativePath,
            OriginalPath = entry.OriginalPath,
            IsSealedAtOrigin = entry.IsSealedAtOrigin,
            BlobFormat = entry.BlobFormat
        };
}