using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdateHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _historyFile = Path.Combine(UpdatePaths.History, "applied_updates.json");

    public IReadOnlyList<UpdateHistoryEntry> LoadRecent(int limit = 10)
    {
        UpdatePaths.EnsureLayout();
        if (!File.Exists(_historyFile))
        {
            return Array.Empty<UpdateHistoryEntry>();
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(File.ReadAllText(_historyFile), JsonOptions)
                          ?? new List<UpdateHistoryEntry>();
            return entries.OrderByDescending(x => x.AppliedAt).Take(limit).ToArray();
        }
        catch
        {
            return Array.Empty<UpdateHistoryEntry>();
        }
    }

    public void Append(UpdateHistoryEntry entry)
    {
        UpdatePaths.EnsureLayout();
        var entries = LoadRecent(100).ToList();
        entries.Insert(0, entry);
        File.WriteAllText(_historyFile, JsonSerializer.Serialize(entries.Take(50), JsonOptions));
    }
}