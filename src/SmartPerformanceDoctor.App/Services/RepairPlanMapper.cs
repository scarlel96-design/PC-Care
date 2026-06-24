using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.Contracts;
using SmartPerformanceDoctor.Contracts.Services;

namespace SmartPerformanceDoctor.App.Services;

public static class RepairPlanMapper
{
    public static IReadOnlyList<RepairActionDescriptor> BuildPlan(
        string scope,
        IReadOnlyList<IntelligenceSummary> diagnoses,
        bool onlyWhenIssuesFound,
        InferenceResult? inference = null)
    {
        if (onlyWhenIssuesFound)
        {
            var fusedScore = inference?.FusedScore ?? (diagnoses.Count == 0 ? 100 : diagnoses.Min(d => d.Score));
            var healthy = fusedScore >= 90
                && diagnoses.All(d => d.Status is "ok" or "healthy" or "양호");
            if (healthy && (inference?.RecommendedRepairActionIds.Count ?? 0) == 0)
            {
                return Array.Empty<RepairActionDescriptor>();
            }
        }

        if (inference?.RecommendedRepairActionIds.Count > 0)
        {
            return ScopeRepairFilter.FilterForScope(inference.RecommendedRepairActionIds, scope)
                .Select(FindAction)
                .Where(action => action is not null)
                .Cast<RepairActionDescriptor>()
                .DistinctBy(action => action.Id)
                .ToArray();
        }

        var ids = ScopeRepairFilter.DefaultActionsForScope(scope);

        return ids
            .Select(FindAction)
            .Where(action => action is not null)
            .Cast<RepairActionDescriptor>()
            .DistinctBy(action => action.Id)
            .ToArray();
    }

    private static RepairActionDescriptor? FindAction(string id)
    {
        return RepairActionRegistry.AllActions
            .FirstOrDefault(action => string.Equals(action.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}