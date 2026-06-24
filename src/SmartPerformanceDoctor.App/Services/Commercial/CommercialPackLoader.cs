using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Commercial;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class CommercialPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static CommercialPackLoader? _shared;
    private IReadOnlyList<CommercialRule> _rules = Array.Empty<CommercialRule>();
    private IReadOnlyList<ResponseProtocol> _protocols = Array.Empty<ResponseProtocol>();
    private bool _loaded;

    public static CommercialPackLoader Shared => _shared ??= new CommercialPackLoader();

    public IReadOnlyList<CommercialRule> Rules
    {
        get { EnsureLoaded(); return _rules; }
    }

    public IReadOnlyList<ResponseProtocol> Protocols
    {
        get { EnsureLoaded(); return _protocols; }
    }

    public string PackVersion { get; private set; } = "45.0.0";

    public void EnsureLoaded()
    {
        if (_loaded) return;

        CommercialPackTrustState.Initialize(RuntimePaths.CommercialDataDirectory);
        var root = RuntimePaths.CommercialDataDirectory;
        if (CommercialPackTrustState.IsFullyTrusted)
        {
            _rules = LoadRules(Path.Combine(root, "rules.pack.json"));
            _protocols = LoadProtocols(Path.Combine(root, "protocols.pack.json"));
            var version = ReadPackVersion(Path.Combine(root, "rules.pack.json"));
            if (!string.IsNullOrWhiteSpace(version))
            {
                PackVersion = version;
            }
        }
        else
        {
            _rules = Array.Empty<CommercialRule>();
            _protocols = Array.Empty<ResponseProtocol>();
        }

        _loaded = true;
    }

    private static string ReadPackVersion(string rulesPath)
    {
        if (!File.Exists(rulesPath))
        {
            return "";
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(rulesPath));
            if (doc.RootElement.TryGetProperty("version", out var versionNode))
            {
                return versionNode.GetString() ?? "";
            }
        }
        catch
        {
            // Ignore malformed pack metadata.
        }

        return "";
    }

    private static IReadOnlyList<CommercialRule> LoadRules(string path)
    {
        if (!File.Exists(path)) return Array.Empty<CommercialRule>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("version", out var v)) { }
            var list = new List<CommercialRule>();
            if (!doc.RootElement.TryGetProperty("rules", out var rules)) return list;
            foreach (var r in rules.EnumerateArray())
            {
                list.Add(new CommercialRule
                {
                    RuleId = r.GetProperty("ruleId").GetString() ?? "",
                    Area = r.TryGetProperty("area", out var a) ? a.GetString() ?? "" : "",
                    Category = r.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "",
                    Severity = r.TryGetProperty("severity", out var s) ? s.GetString() ?? "" : "",
                    Risk = r.TryGetProperty("risk", out var rk) ? rk.GetString() ?? "" : "",
                    ProtocolId = r.TryGetProperty("protocolId", out var p) ? p.GetString() ?? "" : "",
                    UserMessage = r.TryGetProperty("userMessage", out var m) ? m.GetString() ?? "" : "",
                    ConfidenceBase = r.TryGetProperty("confidenceBase", out var cb) ? (float)cb.GetDouble() : 0.5f
                });
            }
            return list;
        }
        catch { return Array.Empty<CommercialRule>(); }
    }

    private static IReadOnlyList<ResponseProtocol> LoadProtocols(string path)
    {
        if (!File.Exists(path)) return Array.Empty<ResponseProtocol>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var list = new List<ResponseProtocol>();
            if (!doc.RootElement.TryGetProperty("protocols", out var protocols)) return list;
            foreach (var p in protocols.EnumerateArray())
            {
                list.Add(new ResponseProtocol
                {
                    ProtocolId = p.GetProperty("protocolId").GetString() ?? "",
                    Area = p.TryGetProperty("area", out var a) ? a.GetString() ?? "" : "",
                    Risk = p.TryGetProperty("risk", out var r) ? r.GetString() ?? "" : "",
                    RequiresElevation = p.TryGetProperty("requiresElevation", out var e) && e.GetBoolean(),
                    RequiresTarget = p.TryGetProperty("requiresTarget", out var t) && t.GetBoolean()
                });
            }
            return list;
        }
        catch { return Array.Empty<ResponseProtocol>(); }
    }
}