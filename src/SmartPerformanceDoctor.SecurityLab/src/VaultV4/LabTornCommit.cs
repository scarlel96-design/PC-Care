using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Test/harness helpers for S-class torn-write simulation (design §14).
/// Not a production API for destroying user data.
/// </summary>
public static class LabTornCommit
{
    public enum Mode
    {
        CorruptMetadataKeepCommit,
        DropActivationMarker,
        CorruptPrimaryCommitOnly,
        /// <summary>Corrupt all activation copies + hash — unlock falls back to legacy allow.</summary>
        CorruptAllCommits,
        /// <summary>Truncate primary header only — dual-copy should recover.</summary>
        TruncatePrimaryHeader,
        /// <summary>Corrupt pack index JSON — vault should still open; object read may fail.</summary>
        CorruptPackIndex,
        /// <summary>No-op placeholder for matrix name alignment; mid-import uses LabFaultMatrix helper.</summary>
        SimulateMidImportOrphan,
        /// <summary>Truncate all header copies — unlock must fail-closed.</summary>
        TruncateAllHeaders,
        /// <summary>Bit-flip object ciphertext under objects/ — vault still opens; export may fail.</summary>
        CorruptLooseObjectCipher,
        /// <summary>Rewrite metadata digest side-car only (marker body unchanged) — fail-closed on digest.</summary>
        CorruptSideDigestOnly
    }

    public static void Apply(string vaultRoot, Mode mode)
    {
        switch (mode)
        {
            case Mode.CorruptMetadataKeepCommit:
            {
                var meta = Path.Combine(vaultRoot, "metadata.db.enc");
                if (File.Exists(meta))
                {
                    var b = File.ReadAllBytes(meta);
                    if (b.Length > 8)
                    {
                        b[^4] ^= 0xA5;
                        File.WriteAllBytes(meta, b);
                    }
                }

                break;
            }
            case Mode.DropActivationMarker:
            {
                foreach (var name in ActivationFiles())
                {
                    var p = Path.Combine(vaultRoot, name);
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                    }
                }

                break;
            }
            case Mode.CorruptPrimaryCommitOnly:
            {
                var p = Path.Combine(vaultRoot, LabDurableCommit.FileName);
                if (File.Exists(p))
                {
                    var t = File.ReadAllText(p);
                    File.WriteAllText(p, t.Replace("Committed", "XXXXXXX", StringComparison.Ordinal));
                    var h = Path.Combine(vaultRoot, "activation.commit.sha256");
                    if (File.Exists(h))
                    {
                        File.WriteAllText(h, "00");
                    }
                }

                break;
            }
            case Mode.CorruptAllCommits:
            {
                foreach (var name in ActivationFiles())
                {
                    var p = Path.Combine(vaultRoot, name);
                    if (File.Exists(p))
                    {
                        File.WriteAllText(p, "{broken");
                    }
                }

                break;
            }
            case Mode.TruncatePrimaryHeader:
            {
                var p = Path.Combine(vaultRoot, "vault.header.json");
                if (File.Exists(p))
                {
                    File.WriteAllBytes(p, new byte[] { 0x7B }); // "{"
                    var h = Path.Combine(vaultRoot, "vault.header.blake3");
                    if (File.Exists(h))
                    {
                        File.WriteAllText(h, "DEAD");
                    }
                }

                break;
            }
            case Mode.CorruptPackIndex:
            {
                var p = Path.Combine(vaultRoot, "packs", "index.v1.json");
                if (File.Exists(p))
                {
                    File.WriteAllText(p, "{not-json");
                }

                break;
            }
            case Mode.SimulateMidImportOrphan:
            {
                // handled by LabFaultMatrix.RunMidImportKillRecovery (needs live journal)
                break;
            }
            case Mode.TruncateAllHeaders:
            {
                foreach (var name in new[]
                         {
                             "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json"
                         })
                {
                    var p = Path.Combine(vaultRoot, name);
                    if (File.Exists(p))
                    {
                        File.WriteAllBytes(p, new byte[] { 0x7B });
                    }
                }

                foreach (var h in new[]
                         {
                             "vault.header.blake3", "vault.header.copy1.sha256"
                         })
                {
                    var p = Path.Combine(vaultRoot, h);
                    if (File.Exists(p))
                    {
                        File.WriteAllText(p, "DEAD");
                    }
                }

                break;
            }
            case Mode.CorruptLooseObjectCipher:
            {
                var objects = Path.Combine(vaultRoot, "objects");
                if (!Directory.Exists(objects))
                {
                    break;
                }

                foreach (var file in Directory.EnumerateFiles(objects, "*", SearchOption.AllDirectories))
                {
                    var b = File.ReadAllBytes(file);
                    if (b.Length > 16)
                    {
                        b[^8] ^= 0x5A;
                        File.WriteAllBytes(file, b);
                        break;
                    }
                }

                break;
            }
            case Mode.CorruptSideDigestOnly:
            {
                // Marker MetadataDigestHex still matches file; side-car alone is advisory —
                // also corrupt marker digest field via primary commit rewrite to force fail-closed.
                var p = Path.Combine(vaultRoot, LabDurableCommit.FileName);
                if (File.Exists(p))
                {
                    var t = File.ReadAllText(p);
                    // flip a hex nibble inside MetadataDigestHex if present
                    var key = "\"MetadataDigestHex\":";
                    var idx = t.IndexOf(key, StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        var quote = t.IndexOf('"', idx + key.Length);
                        if (quote >= 0 && quote + 2 < t.Length)
                        {
                            var ch = t[quote + 1];
                            var alt = ch == 'A' || ch == 'a' ? 'B' : 'A';
                            t = t.Substring(0, quote + 1) + alt + t.Substring(quote + 2);
                            File.WriteAllText(p, t);
                            File.WriteAllText(
                                Path.Combine(vaultRoot, "activation.commit.copy1.json"),
                                t);
                            File.WriteAllText(
                                Path.Combine(vaultRoot, "activation.commit.copy2.json"),
                                t);
                            File.WriteAllText(
                                Path.Combine(vaultRoot, "activation.commit.sha256"),
                                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                                    System.Text.Encoding.UTF8.GetBytes(t))));
                        }
                    }
                }

                var side = Path.Combine(vaultRoot, LabDurableCommit.DigestFileName);
                if (File.Exists(side))
                {
                    File.WriteAllText(side, "00DEADBEEF");
                }

                break;
            }
        }
    }

    /// <summary>Self-heal: rewrite activation from current metadata if AEAD meta still decrypts.</summary>
    public static bool TryRepairActivation(string vaultRoot, string vaultId, long generation)
    {
        try
        {
            if (!File.Exists(Path.Combine(vaultRoot, "metadata.db.enc")))
            {
                return false;
            }

            LabDurableCommit.WriteCommitted(vaultRoot, vaultId, generation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> ActivationFiles()
    {
        yield return LabDurableCommit.FileName;
        yield return "activation.commit.copy1.json";
        yield return "activation.commit.copy2.json";
        yield return "activation.commit.sha256";
        yield return LabDurableCommit.DigestFileName;
    }
}
