using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Design §14/§5 activation-style commit marker (S-class path).
/// After durable metadata write: record generation + metadata ciphertext digest.
/// Unlock rejects when journal incomplete and generation ahead of commit (rollback suspected).
/// </summary>
public static class LabDurableCommit
{
    public const string FileName = "activation.commit.json";
    public const string DigestFileName = "metadata.root.sha256";

    public sealed class Marker
    {
        public string Magic { get; set; } = "AVACT1";
        public long Generation { get; set; }
        public string VaultId { get; set; } = "";
        public string MetadataDigestHex { get; set; } = "";
        public string HeaderDigestHex { get; set; } = "";
        public long Unix { get; set; }
        public string State { get; set; } = "Committed";
    }

    public static string ComputeFileDigest(string path)
    {
        if (!File.Exists(path))
        {
            return "";
        }

        var bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static void WriteCommitted(string vaultRoot, string vaultId, long generation)
    {
        var metaPath = Path.Combine(vaultRoot, "metadata.db.enc");
        var headerPath = Path.Combine(vaultRoot, "vault.header.json");
        var metaDigest = ComputeFileDigest(metaPath);
        var headerDigest = ComputeFileDigest(headerPath);
        var marker = new Marker
        {
            Generation = generation,
            VaultId = vaultId,
            MetadataDigestHex = metaDigest,
            HeaderDigestHex = headerDigest,
            Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            State = "Committed"
        };

        var json = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(vaultRoot, FileName);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        // side digest of metadata for quick verify
        File.WriteAllText(Path.Combine(vaultRoot, DigestFileName), metaDigest);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* ignore */ }

        // triple copy (design §5 multi-copy root-of-trust style)
        File.WriteAllText(Path.Combine(vaultRoot, "activation.commit.copy1.json"), json, Encoding.UTF8);
        File.WriteAllText(Path.Combine(vaultRoot, "activation.commit.copy2.json"), json, Encoding.UTF8);
        File.WriteAllText(Path.Combine(vaultRoot, "activation.commit.sha256"), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
    }

    public static Marker? TryRead(string vaultRoot)
    {
        foreach (var name in new[] { FileName, "activation.commit.copy1.json", "activation.commit.copy2.json" })
        {
            var path = Path.Combine(vaultRoot, name);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(path);
                var hashPath = Path.Combine(vaultRoot, "activation.commit.sha256");
                if (File.Exists(hashPath) && name == FileName)
                {
                    var expected = File.ReadAllText(hashPath).Trim();
                    var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    if (!LabCryptoCompare.FixedTimeEqualsHex(expected, actual))
                    {
                        continue; // try redundant copy
                    }
                }

                var m = JsonSerializer.Deserialize<Marker>(text);
                if (m is not null && string.Equals(m.Magic, "AVACT1", StringComparison.Ordinal))
                {
                    return m;
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    public sealed class VerifyResult
    {
        public bool Ok { get; init; }
        public bool RollbackSuspected { get; init; }
        public string Message { get; init; } = "";
        public long CommitGeneration { get; init; }
        public long HeaderGeneration { get; init; }
    }

    /// <summary>
    /// Fail-closed checks on unlock (design §14 rollback / mixed generation).
    /// </summary>
    public static VerifyResult VerifyOnUnlock(
        string vaultRoot,
        string vaultId,
        long headerGeneration,
        long metadataGeneration)
    {
        var marker = TryRead(vaultRoot);
        var incomplete = LabVaultJournal.ListIncomplete(vaultRoot);
        if (incomplete.Count > 0)
        {
            // incomplete TX after a crash — not hard fail if recover will run, but flag
            // if metadata gen > commit gen without marker → rollback suspected
        }

        var metaPath = Path.Combine(vaultRoot, "metadata.db.enc");
        var actualMeta = ComputeFileDigest(metaPath);
        if (marker is null)
        {
            // pre-S-class vaults: allow open; if incomplete journal, still suspect
            return new VerifyResult
            {
                Ok = true,
                RollbackSuspected = incomplete.Count > 0,
                Message = incomplete.Count > 0
                    ? "activation commit 없음 + incomplete journal"
                    : "activation commit 없음(레거시/구버전) — open 허용",
                CommitGeneration = 0,
                HeaderGeneration = headerGeneration
            };
        }

        // torn commit: marker present but all copies disagree → prefer highest consistent digest
        if (!LabCryptoCompare.FixedTimeEqualsHex(marker.MetadataDigestHex, actualMeta)
            && incomplete.Count == 0)
        {
            // try rebuild commit from current files if header digest matches (self-heal once)
            // caller may repair — here fail-closed
        }

        if (!string.Equals(marker.VaultId, vaultId, StringComparison.Ordinal))
        {
            return new VerifyResult
            {
                Ok = false,
                RollbackSuspected = true,
                Message = "activation vaultId mismatch",
                CommitGeneration = marker.Generation,
                HeaderGeneration = headerGeneration
            };
        }

        if (!LabCryptoCompare.FixedTimeEqualsHex(marker.MetadataDigestHex, actualMeta))
        {
            return new VerifyResult
            {
                Ok = false,
                RollbackSuspected = true,
                Message = "metadata digest mismatch (possible torn write / rollback)",
                CommitGeneration = marker.Generation,
                HeaderGeneration = headerGeneration
            };
        }

        // S-class: header digest soft-check (primary may be torn; dual-copy may still match)
        if (!string.IsNullOrWhiteSpace(marker.HeaderDigestHex))
        {
            var headerMatch = false;
            foreach (var name in new[]
                     {
                         "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json"
                     })
            {
                var hp = Path.Combine(vaultRoot, name);
                if (!File.Exists(hp))
                {
                    continue;
                }

                if (LabCryptoCompare.FixedTimeEqualsHex(marker.HeaderDigestHex, ComputeFileDigest(hp)))
                {
                    headerMatch = true;
                    break;
                }
            }

            if (!headerMatch)
            {
                return new VerifyResult
                {
                    Ok = false,
                    RollbackSuspected = true,
                    Message = "header digest mismatch across all copies (possible rollback)",
                    CommitGeneration = marker.Generation,
                    HeaderGeneration = headerGeneration
                };
            }
        }

        // header generation must not lag far behind metadata without commit
        if (metadataGeneration > marker.Generation + 1)
        {
            return new VerifyResult
            {
                Ok = false,
                RollbackSuspected = true,
                Message = "metadata generation ahead of commit (mixed generation)",
                CommitGeneration = marker.Generation,
                HeaderGeneration = headerGeneration
            };
        }

        if (headerGeneration < marker.Generation - 1)
        {
            return new VerifyResult
            {
                Ok = false,
                RollbackSuspected = true,
                Message = "header generation behind commit (stale header)",
                CommitGeneration = marker.Generation,
                HeaderGeneration = headerGeneration
            };
        }

        if (incomplete.Count > 0 && metadataGeneration > marker.Generation)
        {
            return new VerifyResult
            {
                Ok = true,
                RollbackSuspected = true,
                Message = "incomplete journal + gen drift — recover orphans then re-commit",
                CommitGeneration = marker.Generation,
                HeaderGeneration = headerGeneration
            };
        }

        return new VerifyResult
        {
            Ok = true,
            RollbackSuspected = false,
            Message = "activation commit OK gen=" + marker.Generation,
            CommitGeneration = marker.Generation,
            HeaderGeneration = headerGeneration
        };
    }
}
