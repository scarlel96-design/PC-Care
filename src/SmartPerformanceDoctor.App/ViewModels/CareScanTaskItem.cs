using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class CareScanTaskItem : ObservableObject
{
    private bool _isSelected = true;

    public string Id { get; init; } = "";
    public CareModuleKind Module { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IncludedInSmart { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}