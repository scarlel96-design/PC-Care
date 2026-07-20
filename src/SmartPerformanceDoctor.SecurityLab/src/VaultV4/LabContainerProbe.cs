using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Phase B: non-secret container health probe (locator / dual header / activation / packs / recovery).
/// Does not decrypt content. Safe for locked vaults.
/// </summary>
public static class LabContainerProbe
{
    public sealed class Finding
    {
        public required string Id { get; init; }
        public required bool Ok { get; init; }
        public required string Detail { get; init; }
    }

    public sealed class Report
    {
        public required string Root { get; init; }
        public required bool LooksLikeLabVault { get; init; }
        public required IReadOnlyList<Finding> Findings { get; init; }
        public int Passed => Findings.Count(f => f.Ok);
        public int Total => Findings.Count;
        public bool Healthy => LooksLikeLabVault && Findings.All(f => f.Ok);
        public string FormatId { get; init; } = "";
        public bool Av3WriterFlag { get; init; }

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Container Probe ===");
            sb.AppendLine($"Root: {Root}");
            sb.AppendLine($"LooksLikeLabVault={LooksLikeLabVault} Healthy={Healthy} {Passed}/{Total}");
            sb.AppendLine($"Format={FormatId} Av3WriterFlag={Av3WriterFlag}");
            foreach (var f in Findings)
            {
                sb.AppendLine($"  {(f.Ok ? "OK" : "!!")} [{f.Id}] {f.Detail}");
            }

            return sb.ToString();
        }
    }

    public static Report Probe(string vaultRoot)
    {
        var root = Path.GetFullPath(vaultRoot);
        var findings = new List<Finding>();
        var looks = LabVaultService.Exists(root);
        findings.Add(F("C1", looks, looks ? "header+metadata present" : "missing vault.header.json or metadata.db.enc"));

        var loc = LabVaultLocator.TryRead(root);
        findings.Add(F("C2", loc is not null, loc is null ? "locator missing" : $"locator {loc.Format} v{loc.Version}"));
        var format = loc?.Format ?? "";
        var av3 = loc?.Av3ProductionWriter ?? false;
        findings.Add(F("C3", !av3, av3 ? "Av3ProductionWriter true (unexpected)" : "Av3ProductionWriter=false"));

        var headerNames = new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" };
        var headerOk = 0;
        foreach (var n in headerNames)
        {
            if (File.Exists(Path.Combine(root, n)))
            {
                headerOk++;
            }
        }

        findings.Add(F("C4", headerOk >= 2, $"header copies present {headerOk}/3"));

        // primary header size bound
        var primary = Path.Combine(root, "vault.header.json");
        if (File.Exists(primary))
        {
            try
            {
                var len = (int)new FileInfo(primary).Length;
                LabParserGuard.EnsureHeaderSize(len);
                findings.Add(F("C5", true, $"primary header {len} bytes within bounds"));
            }
            catch (Exception ex)
            {
                findings.Add(F("C5", false, "header bounds: " + ex.Message));
            }
        }
        else
        {
            findings.Add(F("C5", false, "primary header missing"));
        }

        var actPrimary = File.Exists(Path.Combine(root, LabDurableCommit.FileName));
        var actCopy = File.Exists(Path.Combine(root, "activation.commit.copy1.json"));
        findings.Add(F("C6", actPrimary || actCopy,
            actPrimary ? "activation.commit present" : actCopy ? "activation copy present" : "no activation marker (legacy open allowed)"));

        var packsDir = Path.Combine(root, "packs");
        var packsOk = !Directory.Exists(packsDir)
                      || File.Exists(Path.Combine(packsDir, "index.v1.json"))
                      || !Directory.EnumerateFiles(packsDir, "*.avpack").Any();
        findings.Add(F("C7", packsOk,
            Directory.Exists(packsDir)
                ? "packs/ layout ok or empty"
                : "no packs (loose objects only)"));

        var rec = LabRecoverySlots.Snapshot(root);
        findings.Add(F("C8", rec.Format != "corrupt",
            rec.ToUiLine()));

        // journal dir optional
        var journal = Path.Combine(root, "journal");
        findings.Add(F("C9", true,
            Directory.Exists(journal) ? "journal dir present" : "journal dir absent (ok if never imported)"));

        // locator format should prefer v5 when present
        if (loc is not null)
        {
            var fmtOk = string.Equals(loc.Format, LabVaultService.FormatIdV5, StringComparison.Ordinal)
                        || string.Equals(loc.Format, LabVaultService.FormatId, StringComparison.Ordinal);
            findings.Add(F("C10", fmtOk, "locator format id " + loc.Format));
        }
        else
        {
            findings.Add(F("C10", !looks, looks ? "locator missing on lab vault" : "n/a"));
        }

        return new Report
        {
            Root = root,
            LooksLikeLabVault = looks,
            Findings = findings,
            FormatId = format,
            Av3WriterFlag = av3
        };
    }

    private static Finding F(string id, bool ok, string detail) =>
        new() { Id = id, Ok = ok, Detail = detail };
}
