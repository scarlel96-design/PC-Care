using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.Progress;
using SmartPerformanceDoctor.SecurityLab.ShredNext;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}

try
{
    return args[0].ToLowerInvariant() switch
    {
        "vault-create" => CmdVaultCreate(args),
        "vault-roundtrip" => CmdVaultRoundtrip(args),
        "shred-dry-run" => CmdShredDryRun(args),
        "migrate-dry-run" => CmdMigrateDryRun(args),
        "migrate-execute" => CmdMigrateExecute(args),
        "progress" => CmdProgress(),
        "hardening" => CmdHardening(),
        "ship-ready" => CmdShipReady(),
        "remaining-gaps" => CmdRemainingGaps(),
        "flags" => CmdFlags(),
        "av3-gate" => CmdAv3Gate(),
        "container-probe" => CmdContainerProbe(args),
        "migrate-av3-gate" => CmdMigrateAv3Gate(),
        "self-check" => CmdSelfCheck(args),
        "vault-health" => CmdVaultHealth(args),
        "audit-verify" => CmdAuditVerify(args),
        "policy-selfcheck" => CmdPolicySelfCheck(),
        _ => Unknown(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine("ERROR: " + ex.Message);
    return 2;
}

static int Unknown(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}");
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        SecurityLab.Cli — PCCare security lab tools (NOT product)

        Commands:
          vault-create --path <dir> --password <pw> [--profile LabFast|Strong]
          vault-roundtrip --path <dir> --password <pw> --file <path>
          shred-dry-run --target <file>
          migrate-dry-run --vault <product-v3-folder> [--json]
          migrate-execute --vault <product-v3-folder> --lab <lab-folder> --password <pw>
          progress
          hardening
          ship-ready
          remaining-gaps
          flags
          av3-gate
          migrate-av3-gate
          container-probe --vault <lab-folder>
          self-check [--vault <lab-folder>]
          vault-health --vault <lab-folder>
          audit-verify --vault <lab-folder>
          policy-selfcheck

        Product App must NOT reference this project.
        """);
}

static string? GetOpt(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static int CmdVaultCreate(string[] args)
{
    var path = GetOpt(args, "--path") ?? throw new ArgumentException("--path required");
    var password = GetOpt(args, "--password") ?? throw new ArgumentException("--password required");
    var profileName = GetOpt(args, "--profile") ?? "Strong";
    var profile = Enum.TryParse<LabKdfProfile>(profileName, true, out var p) ? p : LabKdfProfile.Strong;

    Directory.CreateDirectory(path);
    using var vault = new LabVaultService(path);
    var result = vault.Create(password, profile);
    Console.WriteLine(result.Success ? "OK " + result.Message : "FAIL " + result.Message);
    if (result.Success)
    {
        Console.WriteLine("VaultId: " + result.VaultId);
        Console.WriteLine("Recovery codes (store offline):");
        foreach (var c in result.RecoveryCodes)
        {
            Console.WriteLine("  " + c);
        }
    }

    return result.Success ? 0 : 1;
}

static int CmdVaultRoundtrip(string[] args)
{
    var path = GetOpt(args, "--path") ?? throw new ArgumentException("--path required");
    var password = GetOpt(args, "--password") ?? throw new ArgumentException("--password required");
    var file = GetOpt(args, "--file") ?? throw new ArgumentException("--file required");

    using var vault = new LabVaultService(path);
    if (!LabVaultService.Exists(path))
    {
        var created = vault.Create(password, LabKdfProfile.LabFast);
        if (!created.Success)
        {
            Console.WriteLine(created.Message);
            return 1;
        }
    }

    var unlock = vault.Unlock(password);
    if (!unlock.Success)
    {
        Console.WriteLine(unlock.Message);
        return 1;
    }

    var imp = vault.ImportFile(file);
    Console.WriteLine(imp.Message);
    return imp.Success ? 0 : 1;
}

static int CmdShredDryRun(string[] args)
{
    var target = GetOpt(args, "--target") ?? throw new ArgumentException("--target required");
    var report = LabShredEngine.DryRun([target]);
    Console.WriteLine(JsonSerializerCompat(report));
    return 0;
}

static int CmdMigrateDryRun(string[] args)
{
    var vault = GetOpt(args, "--vault") ?? throw new ArgumentException("--vault required");
    var json = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
    var report = V3MigrationDryRun.Analyze(vault);
    Console.WriteLine(json ? V3MigrationDryRun.ToJson(report) : V3MigrationDryRun.ToHumanSummary(report));
    return report.LooksLikeProductVault ? 0 : 3;
}

static int CmdMigrateExecute(string[] args)
{
    var vault = GetOpt(args, "--vault") ?? throw new ArgumentException("--vault required");
    var lab = GetOpt(args, "--lab") ?? throw new ArgumentException("--lab required");
    var password = GetOpt(args, "--password") ?? throw new ArgumentException("--password required");
    var result = V3ToLabMigrator.Execute(vault, password, lab, LabKdfProfile.LabFast);
    Console.WriteLine(result.Success ? "OK " + result.Message : "FAIL " + result.Message);
    Console.WriteLine($"Imported={result.Imported} Failed={result.Failed} Skipped={result.Skipped}");
    Console.WriteLine("Lab: " + result.LabVaultPath);
    foreach (var e in result.Errors)
    {
        Console.WriteLine("  err: " + e);
    }

    return result.Success ? 0 : 4;
}

static int CmdProgress()
{
    var p = LabWorkProgress.Calculate();
    Console.WriteLine(p.ToHumanSummary());
    Console.WriteLine($"OVERALL_PERCENT={p.OverallDesignBlendPercent:0.#}");
    Console.WriteLine($"SHIPPING_PERCENT={p.ShippingTrackPercent:0.#}");
    Console.WriteLine($"DESIGN_SCLASS_PERCENT={p.DesignSClassPercent:0.#}");
    return 0;
}

static int CmdHardening()
{
    var r = LabHardeningProbe.Probe();
    Console.WriteLine(r.Summary);
    Console.WriteLine($"DebuggerAttached={r.DebuggerAttached}; 64bit={r.Is64BitProcess}; {r.Runtime}");
    foreach (var rec in r.Recommendations)
    {
        Console.WriteLine(" - " + rec);
    }

    Console.WriteLine();
    var release = LabReleaseHardeningChecklist.Evaluate();
    Console.WriteLine(release.ToHumanSummary());
    return release.ShipCoreReady && release.Av3WriterAuthorized ? 0 : 3;
}

static int CmdShipReady()
{
    var r = LabShipReadiness.Evaluate();
    Console.WriteLine(r.ToHumanSummary());
    return r.LabCoreShipReady ? 0 : 7;
}

static int CmdRemainingGaps()
{
    var r = LabRemainingGaps.Evaluate();
    Console.WriteLine(r.ToHumanSummary());
    return r.LabCodeComplete ? 0 : 9;
}

static int CmdFlags()
{
    Console.WriteLine(ProductFeatureFlags.StatusSummary);
    Console.WriteLine("LabProductGate vault visible: " + LabProductGate.IsFeatureVisible("vault"));
    return 0;
}

static int CmdAv3Gate()
{
    Console.WriteLine(Av3GateSnapshot.ToHumanSummary());
    Console.WriteLine();
    Console.WriteLine(Av3LabEnableChecklist.Evaluate().ToHumanSummary());
    Console.WriteLine();
    Console.WriteLine(LabToAv3MigrationGate.Evaluate().ToHumanSummary());
    return 0;
}

static int CmdMigrateAv3Gate()
{
    var r = LabToAv3MigrationGate.Evaluate();
    Console.WriteLine(r.ToHumanSummary());
    var (ok, msg) = LabToAv3MigrationGate.TryAuthorizeExecute();
    Console.WriteLine(ok ? "EXECUTE: allowed (unexpected)" : "EXECUTE: " + msg);
    return r.ExecuteAllowed ? 4 : 0;
}

static int CmdContainerProbe(string[] args)
{
    var vault = GetOpt(args, "--vault") ?? throw new ArgumentException("--vault required");
    var r = LabContainerProbe.Probe(vault);
    Console.WriteLine(r.ToHumanSummary());
    return r.Healthy ? 0 : 5;
}

static int CmdSelfCheck(string[] args)
{
    var vault = GetOpt(args, "--vault");
    var r = LabSelfCheckSuite.Run(vault);
    Console.WriteLine(r.ToHumanSummary());
    return r.AllPass && r.Av3WriterAuthorized ? 0 : 6;
}

static int CmdVaultHealth(string[] args)
{
    var vault = GetOpt(args, "--vault") ?? throw new ArgumentException("--vault required");
    var r = LabVaultHealth.Probe(vault);
    Console.WriteLine(r.ToHumanSummary());
    return r.OverallOk ? 0 : 8;
}

static int CmdAuditVerify(string[] args)
{
    var vault = GetOpt(args, "--vault") ?? throw new ArgumentException("--vault required");
    var issues = LabAuditChain.Verify(vault);
    if (issues.Count == 0)
    {
        Console.WriteLine("OK audit chain");
        return 0;
    }

    Console.WriteLine("FAIL audit chain:");
    foreach (var i in issues)
    {
        Console.WriteLine(" - " + i);
    }

    return 5;
}

static int CmdPolicySelfCheck()
{
    Console.WriteLine("=== SecurityLab policy self-check ===");
    Console.WriteLine(ProductFeatureFlags.StatusSummary);
    Console.WriteLine(LabHardeningProbe.Probe().Summary);
    Console.WriteLine("Password weak rejected: " + !LabPasswordPolicy.ValidateForCreate("short").IsValid);
    Console.WriteLine("UNC blocked: " + !LabSecurePath.Evaluate(@"\\server\share\a.txt").Allowed);
    Console.WriteLine("System32 blocked: " + !LabSecurePath.Evaluate(@"C:\Windows\System32\x.dll").Allowed);
    var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    var pack = LabRulePack.CreateSigned("self", 1, "{}", key);
    Console.WriteLine("Rule pack signed OK: " + LabRulePack.TryVerify(pack, key, out _));
    pack.SignatureHex = "DEAD";
    Console.WriteLine("Rule pack tamper rejected: " + !LabRulePack.TryVerify(pack, key, out _));
    Console.WriteLine("Gate blocks product: " + !LabProductGate.IsFeatureVisible("vault"));
    Console.WriteLine(LabWorkProgress.Calculate().SummaryLine);
    return 0;
}

static string JsonSerializerCompat(object o) =>
    System.Text.Json.JsonSerializer.Serialize(o, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
