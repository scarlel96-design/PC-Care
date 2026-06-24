using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Commercial;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class ProtocolDetail
{
    public string ProtocolId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Risk { get; init; } = "";
    public bool RequiresElevation { get; init; }
    public bool RequiresTarget { get; init; }
    public IReadOnlyList<string> DryRunSteps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ApplySteps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PostChecks { get; init; } = Array.Empty<string>();
}

public sealed class ProtocolCenterService
{
    public IReadOnlyList<ProtocolDetail> LoadAll()
    {
        CommercialPackLoader.Shared.EnsureLoaded();
        var path = Path.Combine(RuntimePaths.CommercialDataDirectory, "protocols.pack.json");
        if (!File.Exists(path))
        {
            return Array.Empty<ProtocolDetail>();
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("protocols", out var protocols))
            {
                return Array.Empty<ProtocolDetail>();
            }

            var list = new List<ProtocolDetail>();
            foreach (var p in protocols.EnumerateArray())
            {
                list.Add(new ProtocolDetail
                {
                    ProtocolId = p.GetProperty("protocolId").GetString() ?? "",
                    Area = p.TryGetProperty("area", out var a) ? a.GetString() ?? "" : "",
                    Risk = p.TryGetProperty("risk", out var r) ? r.GetString() ?? "" : "",
                    RequiresElevation = p.TryGetProperty("requiresElevation", out var e) && e.GetBoolean(),
                    RequiresTarget = p.TryGetProperty("requiresTarget", out var t) && t.GetBoolean(),
                    DryRunSteps = ReadStringArray(p, "dryRun"),
                    ApplySteps = ReadStringArray(p, "apply"),
                    PostChecks = ReadStringArray(p, "postChecks")
                });
            }

            return list;
        }
        catch
        {
            return CommercialPackLoader.Shared.Protocols
                .Select(p => new ProtocolDetail
                {
                    ProtocolId = p.ProtocolId,
                    Area = p.Area,
                    Risk = p.Risk,
                    RequiresElevation = p.RequiresElevation,
                    RequiresTarget = p.RequiresTarget
                })
                .ToArray();
        }
    }

    public string BuildDryRunPreview(ProtocolDetail protocol) =>
        $"[{protocol.ProtocolId}] dry-run\n" +
        string.Join('\n', protocol.DryRunSteps.Select(s => $"  · {s}")) +
        (protocol.ApplySteps.Count > 0
            ? "\n(apply는 사용자 승인 후에만 실행)"
            : "");

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return arr.EnumerateArray()
            .Select(x => x.GetString() ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}