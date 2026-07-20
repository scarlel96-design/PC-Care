using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Design §7 Concealed: small objects in multi-pack files with optional fixed-size slots.
/// Phase5: pack rotation + fixed 64KiB slots + tombstone overwrite on delete.
/// </summary>
public static class LabPackStore
{
    public const int PackThresholdBytes = 256 * 1024;
    public const int FixedSlotPayload = 64 * 1024 - 36; // id32 + len4 + payload
    public const int FixedRecordSize = 64 * 1024;
    public const long MaxPackBytes = 32L * 1024 * 1024;
    private const string IndexName = "packs/index.v1.json";

    private sealed class IndexDoc
    {
        public int ActivePackIndex { get; set; } = 1;
        public bool FixedSlots { get; set; } = true;
        public Dictionary<string, Loc> Map { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Loc
    {
        public string Pack { get; set; } = "";
        public long Offset { get; set; }
        public int Length { get; set; }
        public int RecordSize { get; set; }
        public bool Tombstone { get; set; }
    }

    public static bool ShouldPack(int cipherLength) =>
        cipherLength > 0 && cipherLength <= PackThresholdBytes;

    public static void Write(string vaultRoot, string objectId, byte[] cipher, bool? fixedSlots = null)
    {
        LabParserGuard.EnsureObjectId(objectId);
        LabParserGuard.EnsureObjectSize(cipher.LongLength);

        if (!ShouldPack(cipher.Length))
        {
            LabObjectStore.WriteLoose(vaultRoot, objectId, cipher);
            RemoveFromIndex(vaultRoot, objectId);
            return;
        }

        var idx = LoadIndex(vaultRoot);
        var useFixed = fixedSlots ?? idx.FixedSlots;
        idx.FixedSlots = useFixed;

        // fixed slots cannot hold > FixedSlotPayload
        if (useFixed && cipher.Length > FixedSlotPayload)
        {
            LabObjectStore.WriteLoose(vaultRoot, objectId, cipher);
            RemoveFromIndex(vaultRoot, objectId);
            return;
        }

        var packRel = EnsureActivePack(vaultRoot, idx, useFixed ? FixedRecordSize : 36 + cipher.Length);
        var packPath = Path.Combine(vaultRoot, packRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(packPath)!);

        long offset;
        int recordSize;
        using (var fs = new FileStream(
                   packPath,
                   FileMode.OpenOrCreate,
                   FileAccess.Write,
                   FileShare.Read,
                   4096,
                   FileOptions.WriteThrough))
        {
            fs.Seek(0, SeekOrigin.End);
            offset = fs.Position;
            var idBytes = System.Text.Encoding.ASCII.GetBytes(objectId.PadRight(32)[..32]);
            fs.Write(idBytes);
            fs.Write(BitConverter.GetBytes(cipher.Length));
            if (useFixed)
            {
                var slot = new byte[FixedSlotPayload];
                Buffer.BlockCopy(cipher, 0, slot, 0, cipher.Length);
                if (cipher.Length < slot.Length)
                {
                    RandomNumberGenerator.Fill(slot.AsSpan(cipher.Length));
                }

                fs.Write(slot);
                recordSize = FixedRecordSize;
                CryptographicOperations.ZeroMemory(slot);
            }
            else
            {
                fs.Write(cipher);
                recordSize = 36 + cipher.Length;
            }

            fs.Flush(true);
        }

        idx.Map[objectId] = new Loc
        {
            Pack = packRel,
            Offset = offset,
            Length = cipher.Length,
            RecordSize = recordSize,
            Tombstone = false
        };
        SaveIndex(vaultRoot, idx);
        LabObjectStore.TryDelete(vaultRoot, objectId);
    }

    public static byte[] Read(string vaultRoot, string objectId)
    {
        LabParserGuard.EnsureObjectId(objectId);
        var idx = LoadIndex(vaultRoot);
        if (!idx.Map.TryGetValue(objectId, out var loc) || loc.Tombstone)
        {
            return LabObjectStore.ReadLooseOrThrow(vaultRoot, objectId);
        }

        var packPath = Path.Combine(vaultRoot, loc.Pack.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packPath))
        {
            throw new FileNotFoundException("pack missing", packPath);
        }

        using var fs = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(loc.Offset, SeekOrigin.Begin);
        var idBuf = new byte[32];
        var lenBuf = new byte[4];
        if (fs.Read(idBuf, 0, 32) != 32 || fs.Read(lenBuf, 0, 4) != 4)
        {
            throw new CryptographicException("pack record truncated");
        }

        var len = BitConverter.ToInt32(lenBuf);
        LabParserGuard.EnsureObjectSize(len);
        if (len != loc.Length || len < 0)
        {
            throw new CryptographicException("pack length mismatch");
        }

        var body = new byte[len];
        if (fs.Read(body, 0, len) != len)
        {
            throw new CryptographicException("pack body truncated");
        }

        return body;
    }

    public static void Delete(string vaultRoot, string objectId)
    {
        var idx = LoadIndex(vaultRoot);
        if (idx.Map.TryGetValue(objectId, out var loc) && !loc.Tombstone)
        {
            try
            {
                var packPath = Path.Combine(vaultRoot, loc.Pack.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(packPath) && loc.RecordSize > 36)
                {
                    using var fs = new FileStream(packPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                    fs.Seek(loc.Offset + 32, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(0)); // length = 0
                    var wipeLen = Math.Max(0, loc.RecordSize - 36);
                    var buf = new byte[Math.Min(8192, Math.Max(1, wipeLen))];
                    var rem = wipeLen;
                    while (rem > 0)
                    {
                        var n = Math.Min(buf.Length, rem);
                        RandomNumberGenerator.Fill(buf.AsSpan(0, n));
                        fs.Write(buf, 0, n);
                        rem -= n;
                    }

                    fs.Flush(true);
                    CryptographicOperations.ZeroMemory(buf);
                }
            }
            catch
            {
                // best effort
            }

            loc.Tombstone = true;
            loc.Length = 0;
            idx.Map[objectId] = loc;
            SaveIndex(vaultRoot, idx);
        }
        else
        {
            RemoveFromIndex(vaultRoot, objectId);
        }

        LabObjectStore.TryDelete(vaultRoot, objectId);
    }

    public static bool IsPacked(string vaultRoot, string objectId)
    {
        var idx = LoadIndex(vaultRoot);
        return idx.Map.TryGetValue(objectId, out var loc) && !loc.Tombstone;
    }

    public static bool Exists(string vaultRoot, string objectId)
    {
        if (IsPacked(vaultRoot, objectId))
        {
            return true;
        }

        return File.Exists(LabObjectStore.AbsolutePath(vaultRoot, objectId));
    }

    public static int ActivePackCount(string vaultRoot)
    {
        var dir = Path.Combine(vaultRoot, "packs");
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        return Directory.GetFiles(dir, "pack-*.avpack").Length;
    }

    public sealed class CompactResult
    {
        public int LiveObjects { get; init; }
        public int TombstonesRemoved { get; init; }
        public int PacksBefore { get; init; }
        public int PacksAfter { get; init; }
        public long BytesBefore { get; init; }
        public long BytesAfter { get; init; }
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// GC: rewrite live (non-tombstone) packed objects into fresh packs, delete old pack files.
    /// Design §7 maintenance — does not touch loose objects/ files.
    /// </summary>
    public static CompactResult Compact(string vaultRoot)
    {
        var packsDir = Path.Combine(vaultRoot, "packs");
        Directory.CreateDirectory(packsDir);
        var beforeFiles = Directory.Exists(packsDir)
            ? Directory.GetFiles(packsDir, "pack-*.avpack")
            : Array.Empty<string>();
        long bytesBefore = beforeFiles.Sum(f => new FileInfo(f).Length);
        var packsBefore = beforeFiles.Length;

        var idx = LoadIndex(vaultRoot);
        var live = idx.Map.Where(kv => !kv.Value.Tombstone && kv.Value.Length > 0).ToList();
        var tombstones = idx.Map.Count(kv => kv.Value.Tombstone);
        var useFixed = idx.FixedSlots;

        // Read all live bodies first (fail closed if any unreadable)
        var bodies = new List<(string Id, byte[] Cipher)>();
        foreach (var (id, _) in live)
        {
            try
            {
                var cipher = Read(vaultRoot, id);
                bodies.Add((id, cipher));
            }
            catch (Exception ex)
            {
                return new CompactResult
                {
                    LiveObjects = bodies.Count,
                    TombstonesRemoved = 0,
                    PacksBefore = packsBefore,
                    PacksAfter = packsBefore,
                    BytesBefore = bytesBefore,
                    BytesAfter = bytesBefore,
                    Message = "compact aborted: unreadable object " + id + " · " + ex.Message
                };
            }
        }

        // Stage into temporary pack directory
        var stageRoot = Path.Combine(vaultRoot, "packs", ".compact-stage-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(stageRoot);
        try
        {
            var newIdx = new IndexDoc
            {
                ActivePackIndex = 1,
                FixedSlots = useFixed,
                Map = new Dictionary<string, Loc>(StringComparer.OrdinalIgnoreCase)
            };
            foreach (var (id, cipher) in bodies)
            {
                WriteIntoIndex(stageRoot, newIdx, id, cipher, useFixed);
            }

            // Replace live packs: delete old pack-*.avpack (not stage)
            foreach (var f in beforeFiles)
            {
                try
                {
                    // shred best-effort
                    var len = new FileInfo(f).Length;
                    if (len > 0 && len < 64L * 1024 * 1024)
                    {
                        using var fs = new FileStream(f, FileMode.Open, FileAccess.Write, FileShare.None);
                        var buf = new byte[Math.Min(65536, (int)len)];
                        RandomNumberGenerator.Fill(buf);
                        fs.Write(buf, 0, (int)Math.Min(buf.Length, len));
                        fs.SetLength(0);
                        fs.Flush(true);
                        CryptographicOperations.ZeroMemory(buf);
                    }

                    File.Delete(f);
                }
                catch
                {
                    try { File.Delete(f); } catch { /* ignore */ }
                }
            }

            // Move staged packs into packs/
            newIdx.ActivePackIndex = Math.Max(1, newIdx.ActivePackIndex);
            foreach (var staged in Directory.GetFiles(stageRoot, "pack-*.avpack"))
            {
                var name = Path.GetFileName(staged);
                var dest = Path.Combine(packsDir, name);
                File.Move(staged, dest, overwrite: true);
            }

            // Rewrite index with pack paths packs/pack-XXXXXX.avpack
            var finalIdx = new IndexDoc
            {
                ActivePackIndex = newIdx.ActivePackIndex,
                FixedSlots = useFixed,
                Map = new Dictionary<string, Loc>(StringComparer.OrdinalIgnoreCase)
            };
            foreach (var (id, loc) in newIdx.Map)
            {
                var packName = Path.GetFileName(loc.Pack.Replace('\\', '/'));
                finalIdx.Map[id] = new Loc
                {
                    Pack = "packs/" + packName,
                    Offset = loc.Offset,
                    Length = loc.Length,
                    RecordSize = loc.RecordSize,
                    Tombstone = false
                };
            }

            SaveIndex(vaultRoot, finalIdx);

            var afterFiles = Directory.GetFiles(packsDir, "pack-*.avpack");
            long bytesAfter = afterFiles.Sum(f => new FileInfo(f).Length);
            return new CompactResult
            {
                LiveObjects = bodies.Count,
                TombstonesRemoved = tombstones,
                PacksBefore = packsBefore,
                PacksAfter = afterFiles.Length,
                BytesBefore = bytesBefore,
                BytesAfter = bytesAfter,
                Message =
                    $"pack compact OK · live {bodies.Count} · tombstones dropped {tombstones} · " +
                    $"packs {packsBefore}→{afterFiles.Length} · bytes {bytesBefore}→{bytesAfter}"
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(stageRoot))
                {
                    Directory.Delete(stageRoot, true);
                }
            }
            catch
            {
                // ignore
            }

            foreach (var (_, cipher) in bodies)
            {
                CryptographicOperations.ZeroMemory(cipher);
            }
        }
    }

    /// <summary>Write cipher into pack files under packRootDir (files named pack-000001.avpack).</summary>
    private static void WriteIntoIndex(
        string packRootDir,
        IndexDoc idx,
        string objectId,
        byte[] cipher,
        bool useFixed)
    {
        var recordSize = useFixed ? FixedRecordSize : 36 + cipher.Length;
        if (idx.ActivePackIndex < 1)
        {
            idx.ActivePackIndex = 1;
        }

        string Rel(int i) => $"pack-{i:D6}.avpack";
        var packName = Rel(idx.ActivePackIndex);
        var packPath = Path.Combine(packRootDir, packName);
        long size = File.Exists(packPath) ? new FileInfo(packPath).Length : 0;
        if (size + recordSize > MaxPackBytes)
        {
            idx.ActivePackIndex++;
            packName = Rel(idx.ActivePackIndex);
            packPath = Path.Combine(packRootDir, packName);
        }

        long offset;
        using (var fs = new FileStream(
                   packPath,
                   FileMode.OpenOrCreate,
                   FileAccess.Write,
                   FileShare.Read,
                   4096,
                   FileOptions.WriteThrough))
        {
            fs.Seek(0, SeekOrigin.End);
            offset = fs.Position;
            var idBytes = System.Text.Encoding.ASCII.GetBytes(objectId.PadRight(32)[..32]);
            fs.Write(idBytes);
            fs.Write(BitConverter.GetBytes(cipher.Length));
            if (useFixed)
            {
                var slot = new byte[FixedSlotPayload];
                Buffer.BlockCopy(cipher, 0, slot, 0, cipher.Length);
                if (cipher.Length < slot.Length)
                {
                    RandomNumberGenerator.Fill(slot.AsSpan(cipher.Length));
                }

                fs.Write(slot);
                CryptographicOperations.ZeroMemory(slot);
            }
            else
            {
                fs.Write(cipher);
            }

            fs.Flush(true);
        }

        idx.Map[objectId] = new Loc
        {
            Pack = packName,
            Offset = offset,
            Length = cipher.Length,
            RecordSize = recordSize,
            Tombstone = false
        };
    }

    private static string EnsureActivePack(string vaultRoot, IndexDoc idx, int upcomingRecordBytes)
    {
        if (idx.ActivePackIndex < 1)
        {
            idx.ActivePackIndex = 1;
        }

        string Rel(int i) => $"packs/pack-{i:D6}.avpack";
        var packRel = Rel(idx.ActivePackIndex);
        var packPath = Path.Combine(vaultRoot, packRel.Replace('/', Path.DirectorySeparatorChar));
        long size = File.Exists(packPath) ? new FileInfo(packPath).Length : 0;
        if (size + upcomingRecordBytes > MaxPackBytes)
        {
            idx.ActivePackIndex++;
            packRel = Rel(idx.ActivePackIndex);
            SaveIndex(vaultRoot, idx);
        }

        return packRel;
    }

    private static void RemoveFromIndex(string vaultRoot, string objectId)
    {
        var idx = LoadIndex(vaultRoot);
        if (idx.Map.Remove(objectId))
        {
            SaveIndex(vaultRoot, idx);
        }
    }

    private static IndexDoc LoadIndex(string vaultRoot)
    {
        var path = Path.Combine(vaultRoot, IndexName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new IndexDoc { FixedSlots = true, ActivePackIndex = 1 };
        }

        try
        {
            return JsonSerializer.Deserialize<IndexDoc>(File.ReadAllText(path))
                   ?? new IndexDoc { FixedSlots = true, ActivePackIndex = 1 };
        }
        catch
        {
            return new IndexDoc { FixedSlots = true, ActivePackIndex = 1 };
        }
    }

    private static void SaveIndex(string vaultRoot, IndexDoc doc)
    {
        var path = Path.Combine(vaultRoot, IndexName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // atomic-ish write
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* ignore */ }
    }
}
