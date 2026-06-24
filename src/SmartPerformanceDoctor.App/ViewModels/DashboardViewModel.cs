using Microsoft.UI.Xaml;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly DashboardIntelligenceService _service = new();
    private readonly UserModeService _userMode = new();

    private string _overallStatus = "대기";
    private string _summary = "새로 고침을 눌러 현재 상태를 확인하세요.";
    private int _healthScore = 0;
    private IReadOnlyList<string> _topIssues = Array.Empty<string>();
    private IReadOnlyList<DashboardAction> _quickActions = Array.Empty<DashboardAction>();
    private IReadOnlyList<AttentionCard> _attentionCards = Array.Empty<AttentionCard>();
    private IReadOnlyList<ReportPreview> _recentReports = Array.Empty<ReportPreview>();
    private DashboardAction? _primaryRecommendation;
    private IReadOnlyList<DashboardAction> _secondaryRecommendations = Array.Empty<DashboardAction>();

    public string OverallStatus { get => _overallStatus; private set => Set(ref _overallStatus, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public int HealthScore { get => _healthScore; private set => Set(ref _healthScore, value); }
    public IReadOnlyList<string> TopIssues { get => _topIssues; private set => Set(ref _topIssues, value); }
    public IReadOnlyList<DashboardAction> QuickActions { get => _quickActions; private set => Set(ref _quickActions, value); }
    public IReadOnlyList<AttentionCard> AttentionCards { get => _attentionCards; private set => Set(ref _attentionCards, value); }
    public IReadOnlyList<ReportPreview> RecentReports { get => _recentReports; private set => Set(ref _recentReports, value); }
    public DashboardAction? PrimaryRecommendation { get => _primaryRecommendation; private set => Set(ref _primaryRecommendation, value); }
    public IReadOnlyList<DashboardAction> SecondaryRecommendations { get => _secondaryRecommendations; private set => Set(ref _secondaryRecommendations, value); }
    public bool ShowDeveloperDetails => _userMode.Meets(UserMode.Developer);
    public bool HasAttentionCards => AttentionCards.Count > 0;
    public Visibility AttentionCardsVisibility => HasAttentionCards ? Visibility.Visible : Visibility.Collapsed;

    public void Refresh()
    {
        var snapshot = _service.BuildSnapshot();
        OverallStatus = snapshot.OverallStatus;
        Summary = snapshot.Summary;
        HealthScore = snapshot.HealthScore;
        TopIssues = snapshot.TopIssues;
        QuickActions = snapshot.QuickActions;
        AttentionCards = snapshot.AttentionCards;
        RecentReports = snapshot.RecentReports;
        PrimaryRecommendation = snapshot.PrimaryRecommendation;
        SecondaryRecommendations = snapshot.SecondaryRecommendations;
        OnPropertyChanged(nameof(ShowDeveloperDetails));
        OnPropertyChanged(nameof(HasAttentionCards));
        OnPropertyChanged(nameof(AttentionCardsVisibility));
    }
}