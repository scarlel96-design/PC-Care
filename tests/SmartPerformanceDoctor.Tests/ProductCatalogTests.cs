using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Platform;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class ProductCatalogTests
{
    [Fact]
    public void DefaultCatalog_ExposesFivePrimaryProductAreas()
    {
        var catalog = ProductComposition.CreateDefault();

        var primary = catalog.Features.Where(feature => feature.IsPrimaryNavigation).ToArray();

        Assert.Equal(5, primary.Length);
        Assert.Equal(
            ["home", "care", "security", "history", "settings"],
            primary.Select(feature => feature.Id).ToArray());
        Assert.Equal(3, catalog.Modules.Count);
    }

    [Fact]
    public void DefaultCatalog_HasUniqueFeatureAndEngineIds()
    {
        var catalog = ProductComposition.CreateDefault();

        Assert.Equal(catalog.Features.Count, catalog.Features.Select(feature => feature.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(catalog.Engines.Count, catalog.Engines.Select(engine => engine.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(catalog.Engines, engine => Assert.NotEmpty(engine.Capabilities));
    }

    [Fact]
    public void Register_RejectsDuplicateModuleIds()
    {
        var catalog = new ProductCatalog();
        var module = new TestModule("test.module", "test.feature", "test.engine");
        catalog.Register(module);

        var error = Assert.Throws<InvalidOperationException>(() => catalog.Register(module));

        Assert.Contains("Duplicate product module id", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_RejectsDuplicateFeatureIdsAcrossModules()
    {
        var catalog = new ProductCatalog();
        catalog.Register(new TestModule("test.one", "same.feature", "engine.one"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(new TestModule("test.two", "same.feature", "engine.two")));

        Assert.Contains("Duplicate feature id", error.Message, StringComparison.Ordinal);
    }

    private sealed class TestModule(string id, string featureId, string engineId) : IProductModule
    {
        public string Id => id;
        public Version ContractVersion => new(1, 0);
        public IEnumerable<ProductFeatureDescriptor> Features =>
        [
            new(featureId, "Test", "Test feature", "T", ProductArea.Advanced, typeof(TestPage), 1)
        ];
        public IEnumerable<EngineDescriptor> Engines =>
        [
            new(engineId, "Test engine", EngineKind.Infrastructure, "1", ["test"])
        ];
    }

    private sealed class TestPage : Page;
}
