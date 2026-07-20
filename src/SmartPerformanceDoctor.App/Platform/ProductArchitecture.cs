using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Installation;

namespace SmartPerformanceDoctor.App.Platform;

public enum ProductArea
{
    Home,
    Care,
    Security,
    History,
    Settings,
    Advanced
}

public enum EngineKind
{
    Diagnostics,
    Optimization,
    Security,
    Recovery,
    Intelligence,
    Infrastructure
}

public sealed record ProductFeatureDescriptor(
    string Id,
    string Title,
    string Description,
    string Glyph,
    ProductArea Area,
    Type PageType,
    int Order,
    bool IsPrimaryNavigation = false,
    UserMode MinimumMode = UserMode.Basic,
    string? InstallFeatureId = null,
    IReadOnlyList<string>? SearchTerms = null)
{
    public IReadOnlyList<string> Keywords { get; } = SearchTerms ?? [];
}

public sealed record EngineDescriptor(
    string Id,
    string DisplayName,
    EngineKind Kind,
    string Version,
    IReadOnlyList<string> Capabilities,
    bool RequiresElevation = false,
    bool RunsOutOfProcess = false);

public interface IProductModule
{
    string Id { get; }
    Version ContractVersion { get; }
    IEnumerable<ProductFeatureDescriptor> Features { get; }
    IEnumerable<EngineDescriptor> Engines { get; }
}

public sealed class ProductCatalog
{
    private readonly Dictionary<string, ProductFeatureDescriptor> _features = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EngineDescriptor> _engines = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _modules = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ProductFeatureDescriptor> Features => _features.Values.OrderBy(x => x.Order).ToArray();
    public IReadOnlyList<EngineDescriptor> Engines => _engines.Values.OrderBy(x => x.DisplayName).ToArray();
    public IReadOnlyCollection<string> Modules => _modules;

    public void Register(IProductModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!_modules.Add(module.Id))
        {
            throw new InvalidOperationException($"Duplicate product module id: {module.Id}");
        }

        foreach (var feature in module.Features)
        {
            ValidateFeature(feature);
            if (!_features.TryAdd(feature.Id, feature))
            {
                throw new InvalidOperationException($"Duplicate feature id: {feature.Id}");
            }
        }

        foreach (var engine in module.Engines)
        {
            ValidateEngine(engine);
            if (!_engines.TryAdd(engine.Id, engine))
            {
                throw new InvalidOperationException($"Duplicate engine id: {engine.Id}");
            }
        }
    }

    public ProductFeatureDescriptor GetFeature(string id) =>
        _features.TryGetValue(id, out var feature)
            ? feature
            : throw new KeyNotFoundException($"Unknown product feature: {id}");

    public bool TryGetFeature(string id, out ProductFeatureDescriptor feature) =>
        _features.TryGetValue(id, out feature!);

    public IReadOnlyList<ProductFeatureDescriptor> GetPrimaryNavigation(
        UserModeService userMode,
        InstalledFeaturesService installedFeatures) =>
        Features.Where(feature =>
                feature.IsPrimaryNavigation &&
                userMode.Meets(feature.MinimumMode) &&
                NavigationFeatureMap.ShouldShow(feature.InstallFeatureId, installedFeatures))
            .ToArray();

    private static void ValidateFeature(ProductFeatureDescriptor feature)
    {
        if (string.IsNullOrWhiteSpace(feature.Id) || string.IsNullOrWhiteSpace(feature.Title))
        {
            throw new ArgumentException("Feature id and title are required.");
        }

        if (!typeof(Microsoft.UI.Xaml.Controls.Page).IsAssignableFrom(feature.PageType))
        {
            throw new ArgumentException($"Feature page must derive from Page: {feature.Id}");
        }
    }

    private static void ValidateEngine(EngineDescriptor engine)
    {
        if (string.IsNullOrWhiteSpace(engine.Id) || string.IsNullOrWhiteSpace(engine.DisplayName))
        {
            throw new ArgumentException("Engine id and display name are required.");
        }

        if (engine.Capabilities.Count == 0)
        {
            throw new ArgumentException($"At least one capability is required: {engine.Id}");
        }
    }
}
