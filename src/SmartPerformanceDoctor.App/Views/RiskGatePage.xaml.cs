using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class RiskGatePage : Page
{
    private readonly RepairHelperClient _repairHelperClient = new();
    private readonly OperationProgressHub _progress = OperationProgressHub.Shared;

    public RiskGatePage()
    {
        InitializeComponent();
    }

    private async void GeneratePlan(object sender, RoutedEventArgs e)
    {
        await SendRepairRequest(dryRun: true);
    }

    private async void ApplyRepair(object sender, RoutedEventArgs e)
    {
        await SendRepairRequest(dryRun: false);
    }

    private async Task SendRepairRequest(bool dryRun)
    {
        var action = ((ActionBox.SelectedItem as ComboBoxItem)?.Content as string) ?? "dism_checkhealth";
        var operationId = $"riskgate-{action}-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        _progress.Publish(operationId, "RepairHelper", action, "Running", 10, dryRun ? "dry-run 요청" : "apply 요청", canCancel: !dryRun);

        var response = await _repairHelperClient.SendAsync(new RepairHelperRequest
        {
            Action = action,
            Target = "online-image",
            DryRun = dryRun,
            RiskAccepted = ConfirmCheck.IsChecked == true
        }, CancellationToken.None);

        var phase = response.Status is "dry-run" or "ok" or "planned" ? "Completed" : "Failed";
        _progress.Publish(operationId, "RepairHelper", action, phase, 100, response.Message, canCancel: false);

        ResultText.Text =
            $"상태: {response.Status}\n" +
            $"메시지: {response.Message}\n" +
            $"종료 코드: {response.ExitCode}\n" +
            $"관리자 권한: {response.Elevated}\n" +
            $"STDOUT: {response.Stdout}\n" +
            $"STDERR: {response.Stderr}";
    }
}
