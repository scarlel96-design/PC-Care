using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;

namespace SmartPerformanceDoctor.Setup;

public partial class MainWindow : Window
{
    private static string ProductVersion => InstallerPaths.ProductVersion;
    private const string VaultDeletePhrase = "금고삭제";
    private int _step;
    private bool _welcomeActionPending;
    private bool _installUpgradeFlow;
    private InstallMode _mode = InstallMode.Recommended;
    private SetupLaunchMode _launchMode = SetupLaunchMode.Install;
    private UninstallScope _uninstallScope = UninstallScope.ProgramOnly;
    private readonly InstalledFeaturesManifest? _existingManifest;
    private readonly Dictionary<string, CheckBox> _featureChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly InstallerRunner _runner = new();
    private readonly RepairRunner _repairRunner = new();
    private readonly UninstallRunner _uninstallRunner = new();

    private CheckBox? _consentCheck;
    private TextBox? _pathBox;
    private TextBox? _vaultConfirmBox;
    private ProgressBar? _progressBar;
    private TextBlock? _progressText;
    private ListBox? _operationLogList;
    private bool _operationRunning;

    public MainWindow()
    {
        InitializeComponent();
        VersionLine.Text = $"설치 프로그램 {ProductVersion} · 오프라인 패키지";
        _launchMode = SetupLaunchModeParser.Parse(Environment.GetCommandLineArgs());
        _existingManifest = TryLoadExistingManifest();

        switch (_launchMode)
        {
            case SetupLaunchMode.Modify when _existingManifest is not null:
                _mode = InstallMode.Custom;
                _step = 2;
                Title = "PC 케어 프로 — 기능 변경";
                break;
            case SetupLaunchMode.Repair:
                _step = 5;
                Title = "PC 케어 프로 — 복구";
                break;
            case SetupLaunchMode.Uninstall:
                _step = 0;
                Title = "PC 케어 프로 — 제거";
                break;
        }

        ApplyWindowIcon();
        RenderStep();
    }

    private void ApplyWindowIcon()
    {
        var iconRoot = InstallerPaths.ResolveLayoutDirectory() ?? AppContext.BaseDirectory;
        var iconPath = ProductIconService.ResolveIconPath(iconRoot);
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return;
        }

        try
        {
            Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }
        catch
        {
            // Cosmetic only.
        }
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            _step--;
            RenderStep();
        }
    }

    private async void NextClick(object sender, RoutedEventArgs e)
    {
        if (_operationRunning || !CanProceed())
        {
            return;
        }

        if (_step == 6 && _launchMode == SetupLaunchMode.Repair)
        {
            await RunOperationAsync(RunRepairAsync);
            _step = 7;
            RenderStep();
            return;
        }

        if (_step == 6 && _launchMode != SetupLaunchMode.Uninstall)
        {
            await RunOperationAsync(RunInstallAsync);
            _step = 7;
            RenderStep();
            return;
        }

        if (_launchMode == SetupLaunchMode.Uninstall && _step == 1)
        {
            _step = 2;
            RenderStep();
            await RunOperationAsync(RunUninstallAsync);
            _step = 3;
            RenderStep();
            return;
        }

        var maxStep = _launchMode == SetupLaunchMode.Uninstall ? 3 : 7;
        if (_step < maxStep)
        {
            _step++;
            RenderStep();
        }
        else
        {
            Close();
        }
    }

    private bool CanProceed()
    {
        if (_launchMode == SetupLaunchMode.Uninstall)
        {
            return _step switch
            {
                0 => true,
                1 => VaultDeleteConfirmed(),
                _ => true
            };
        }

        return _step switch
        {
            0 => !_welcomeActionPending,
            1 => ConsentOk(),
            2 => true,
            3 => true,
            4 => !string.IsNullOrWhiteSpace(_pathBox?.Text),
            _ => true
        };
    }

    private bool VaultDeleteConfirmed() => true;

    private bool ConsentOk() => _consentCheck?.IsChecked == true;
    private void RenderStep()
    {
        HighlightSteps();
        ContentPanel.Children.Clear();
        if (_launchMode == SetupLaunchMode.Uninstall)
        {
            BackButton.IsEnabled = _step > 0 && _step < 2 && !_operationRunning;
            NextButton.Content = _step switch { 1 => "제거", 3 => "완료", _ => "다음" };
            NextButton.IsEnabled = _step != 2 && !_operationRunning;
            switch (_step)
            {
                case 0: BuildUninstallOptions(); break;
                case 1: BuildUninstallConfirm(); break;
                case 2: BuildUninstallProgress(); break;
                case 3: BuildUninstallDone(); break;
            }
            return;
        }

        BackButton.IsEnabled = _step > 0 && _step < 7;
        NextButton.Content = _step switch
        {
            6 => _launchMode == SetupLaunchMode.Repair ? "복구" : "설치",
            7 => "완료",
            _ => "다음"
        };

        switch (_step)
        {
            case 0: BuildWelcome(); break;
            case 1: BuildConsent(); break;
            case 2: BuildFeatures(); break;
            case 3: BuildWarnings(); break;
            case 4: BuildPath(); break;
            case 5: BuildPreflight(); break;
            case 6: BuildInstallPreview(); break;
            case 7: BuildDone(); break;
        }
    }

    private void HighlightSteps()
    {
        var stage = _step switch
        {
            0 => 0,
            1 => 1,
            2 or 3 or 4 => 2,
            5 => 3,
            6 => 4,
            _ => 5
        };
        var labels = new[] { StepWelcome, StepEula, StepFeatures, StepPreflight, StepInstall, StepDone };
        var titles = new[] { "시작", "약관 및 동의", "설치 구성", "준비 확인", "설치 진행", "완료" };
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i].Foreground = i == stage
                ? Brushes.White
                : (Brush)FindResource("SpdSidebarMutedBrush");
            labels[i].FontWeight = i == stage ? FontWeights.SemiBold : FontWeights.Normal;
            labels[i].Opacity = i <= stage ? 1.0 : 0.76;
        }

        StepCounterLine.Text = $"{stage + 1} / {labels.Length}";
        StepTitleLine.Text = titles[stage];
        StepProgressBar.Value = stage + 1;
    }
    private void BuildWelcome()
    {
        AddTitle("PC 케어 프로 설치");
        AddBody("시스템 진단·복구·보안 기능을 원하는 구성만 선택해 설치할 수 있습니다.");

        if (_existingManifest is not null && _launchMode == SetupLaunchMode.Install && !_installUpgradeFlow)
        {
            _welcomeActionPending = true;
            AddBody("기존 설치가 감지되었습니다. 원하는 작업을 선택하세요.");
            AddBody("「새 설치 / 업그레이드」를 선택하면 권장·전체·사용자 지정·최소 설치 중 원하는 유형을 고를 수 있습니다.");
            var actions = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            actions.Children.Add(MakeActionButton("새 설치 / 업그레이드", BeginInstallUpgrade));
            actions.Children.Add(MakeActionButton("기능 변경 (Modify)", () =>
            {
                _welcomeActionPending = false;
                _launchMode = SetupLaunchMode.Modify;
                _mode = InstallMode.Custom;
                _step = 2;
                RenderStep();
            }));
            actions.Children.Add(MakeActionButton("복구 (Repair)", () =>
            {
                _welcomeActionPending = false;
                _launchMode = SetupLaunchMode.Repair;
                _step = 5;
                RenderStep();
            }));
            actions.Children.Add(MakeActionButton("제거 (Uninstall)", () =>
            {
                _welcomeActionPending = false;
                _launchMode = SetupLaunchMode.Uninstall;
                _step = 0;
                RenderStep();
            }));
            ContentPanel.Children.Add(actions);
            return;
        }

        _welcomeActionPending = false;
        AddBody(_installUpgradeFlow
            ? "새 설치 / 업그레이드 유형을 선택하세요. 다음 단계에서 기능을 개별 조정할 수 있습니다."
            : "설치 유형을 선택하세요.");
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(MakeRadio("권장 설치", InstallMode.Recommended, _mode == InstallMode.Recommended));
        panel.Children.Add(MakeRadio("전체 설치", InstallMode.Full, _mode == InstallMode.Full));
        panel.Children.Add(MakeRadio("사용자 지정 설치", InstallMode.Custom, _mode == InstallMode.Custom));
        panel.Children.Add(MakeRadio("최소 설치", InstallMode.Minimal, _mode == InstallMode.Minimal));
        ContentPanel.Children.Add(panel);
    }

    private void BeginInstallUpgrade()
    {
        _welcomeActionPending = false;
        _installUpgradeFlow = true;
        _launchMode = SetupLaunchMode.Install;
        _mode = InstallMode.Recommended;
        _step = 0;
        RenderStep();
    }

    private Button MakeActionButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)FindResource("SpdSecondaryButtonStyle")
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private RadioButton MakeRadio(string label, InstallMode mode, bool selected)
    {
        var radio = new RadioButton
        {
            Content = label,
            IsChecked = selected,
            Margin = new Thickness(0, 6, 0, 0),
            GroupName = "install-mode"
        };
        radio.Checked += (_, _) => _mode = mode;
        if (selected)
        {
            _mode = mode;
        }

        return radio;
    }

    private void BuildConsent()
    {
        AddTitle("약관 및 통합 동의");
        AddBody("아래 내용을 확인한 뒤, 한 번의 동의로 설치에 필요한 약관·개인정보 처리·선택 기능의 주의 사항을 함께 승인할 수 있습니다.");
        AddScrollText(
            """
            【사용권 계약 및 데이터 처리】
            • PC 케어 프로는 본인 소유 또는 사용 권한이 있는 Windows PC에서만 사용해야 합니다.
            • 진단 보고서·감사 로그·세션 기록·스냅샷은 기본적으로 이 PC에 로컬 저장됩니다.
            • 업데이트 확인에는 버전·채널 정보만 사용하며, 개인 파일 내용은 사용자 동의 없이 외부 서버로 전송하지 않습니다.

            【시스템 점검·복구】
            • 시스템 케어, 드라이버·오디오 복구, 레지스트리·디스크 검사, DISM/SFC는 Windows 설정·서비스를 변경하거나 재부팅을 요구할 수 있습니다.
            • 실제 변경 전에는 프로그램 안에서 다시 미리보기·위험도·승인 절차를 제공합니다. 중요 데이터는 별도 백업을 권장합니다.

            【보안 기능】
            • 보안 금고는 암호화된 로컬 보관함을 사용합니다. 비밀번호·복구 키를 잃으면 데이터를 복구하지 못할 수 있습니다.
            • 보안 삭제와 개인정보 정리는 일부 항목을 되돌릴 수 없으며, SSD TRIM·동기화·백업 매체에는 별도 잔존 데이터가 있을 수 있습니다.

            【책임 제한】
            • 본 프로그램은 있는 그대로 제공되며, 부적절한 사용·백업 부재·타사 드라이버 또는 하드웨어 문제로 생기는 손해는 법령이 허용하는 범위에서 제한됩니다.
            """);
        _consentCheck = AddCheck("사용권 계약, 로컬 데이터 처리, 시스템 변경 및 보안 기능의 주의 사항을 읽고 이해했으며 설치에 동의합니다.");
    }
    private void BuildFeatures()
    {
        AddTitle("기능 선택");
        _featureChecks.Clear();
        if (_mode != InstallMode.Custom)
        {
            AddBody($"「{GetInstallModeLabel(_mode)}」 프리셋이 적용됩니다. 사용자 지정 설치를 선택하거나 아래 버튼으로 전체 선택·해제하면 개별 기능을 변경할 수 있습니다.");
        }

        var bulkActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0)
        };
        bulkActions.Children.Add(MakeFeatureBulkButton("전체 선택", SelectAllOptionalFeatures));
        bulkActions.Children.Add(MakeFeatureBulkButton("전체 해제", DeselectAllOptionalFeatures));
        ContentPanel.Children.Add(bulkActions);

        foreach (var feature in FeatureCatalog.All)
        {
            var enabled = IsFeatureEnabledByMode(feature);
            var check = new CheckBox
            {
                Content = $"{feature.DisplayName} — {feature.Description}",
                IsChecked = enabled,
                IsEnabled = !feature.IsRequired && _mode == InstallMode.Custom,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _featureChecks[feature.Id] = check;
            ContentPanel.Children.Add(check);
        }
    }

    private Button MakeFeatureBulkButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6),
            Style = (Style)FindResource("SpdSecondaryButtonStyle")
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void SelectAllOptionalFeatures()
    {
        EnsureCustomFeatureMode();
        foreach (var (id, check) in _featureChecks)
        {
            if (FeatureCatalog.All.First(f => f.Id == id).IsRequired)
            {
                continue;
            }

            check.IsChecked = true;
        }
    }

    private void DeselectAllOptionalFeatures()
    {
        EnsureCustomFeatureMode();
        foreach (var (id, check) in _featureChecks)
        {
            if (FeatureCatalog.All.First(f => f.Id == id).IsRequired)
            {
                continue;
            }

            check.IsChecked = false;
        }
    }

    private void EnsureCustomFeatureMode()
    {
        if (_mode == InstallMode.Custom)
        {
            return;
        }

        _mode = InstallMode.Custom;
        foreach (var (id, check) in _featureChecks)
        {
            var feature = FeatureCatalog.All.First(f => f.Id == id);
            check.IsEnabled = !feature.IsRequired;
        }
    }

    private bool IsFeatureEnabledByMode(FeatureDefinition feature)
    {
        if (feature.IsRequired)
        {
            return true;
        }

        if (!_installUpgradeFlow && _existingManifest?.IsEnabled(feature.Id) == true)
        {
            return true;
        }

        return _mode switch
        {
            InstallMode.Full => true,
            InstallMode.Recommended => feature.IncludedInRecommended,
            InstallMode.Minimal => feature.IncludedInMinimal,
            _ => false
        };
    }

    private static string GetInstallModeLabel(InstallMode mode) => mode switch
    {
        InstallMode.Recommended => "권장 설치",
        InstallMode.Full => "전체 설치",
        InstallMode.Custom => "사용자 지정 설치",
        InstallMode.Minimal => "최소 설치",
        _ => mode.ToString()
    };

    private void BuildWarnings()
    {
        AddTitle("선택 기능 주의사항");
        foreach (var id in SelectedFeatureIds())
        {
            var warning = id switch
            {
                InstallFeatureIds.DriverAudioRepair => "드라이버/오디오 복구는 관리자 권한·재시작이 필요할 수 있습니다.",
                InstallFeatureIds.SecureVault => "금고 비밀번호를 잃으면 파일을 복구할 수 없습니다.",
                InstallFeatureIds.ProfessionalSecureDelete => "삭제된 파일은 복구가 불가능하거나 극도로 어려울 수 있습니다.",
                InstallFeatureIds.RegistryDoctor => "레지스트리 변경 전 백업과 위험도 분류가 수행됩니다.",
                InstallFeatureIds.DiskDoctor => "디스크 검사는 시간이 오래 걸릴 수 있으며 SSD에는 조각 모음을 적용하지 않습니다.",
                InstallFeatureIds.VulnerabilityFix => "취약점 수정은 항목별 승인 방식으로 동작합니다.",
                _ => null
            };

            if (warning is not null)
            {
                AddBody($"• {FeatureCatalog.All.First(f => f.Id == id).DisplayName}: {warning}");
            }
        }
    }

    private void BuildPath()
    {
        AddTitle("설치 위치");
        var defaultPath = ResolveInstallPathDefault();
        var pathReadOnly = _launchMode == SetupLaunchMode.Modify;
        _pathBox = new TextBox
        {
            Text = defaultPath,
            IsReadOnly = pathReadOnly,
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center
        };

        var pathRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_pathBox, 0);
        pathRow.Children.Add(_pathBox);
        if (!pathReadOnly)
        {
            var browse = new Button
            {
                Content = "찾아보기…",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Style = (Style)FindResource("SpdSecondaryButtonStyle")
            };
            browse.Click += (_, _) => BrowseInstallPath();
            Grid.SetColumn(browse, 1);
            pathRow.Children.Add(browse);
        }

        ContentPanel.Children.Add(pathRow);
        AddBody(_launchMode == SetupLaunchMode.Modify
            ? "기능 변경 모드에서는 기존 설치 경로를 사용합니다."
            : "설치 경로를 직접 입력하거나 찾아보기로 지정할 수 있습니다. 관리자 권한이 필요할 수 있습니다.");
    }

    private void BuildPreflight()
    {
        EnsurePathBox();
        AddTitle("사전 환경 점검");
        var drive = Path.GetPathRoot(_pathBox?.Text ?? "C:\\") ?? "C:\\";
        var freeGb = GetFreeSpaceGb(drive);
        AddBody($"대상 드라이브 {drive.TrimEnd('\\')} 여유 공간: {freeGb:F1} GB");
        AddBody(freeGb >= 1 ? "✓ 디스크 용량 충분" : "⚠ 디스크 여유 공간이 부족할 수 있습니다.");
        AddBody(Environment.Is64BitOperatingSystem ? "✓ 64비트 Windows" : "⚠ 32비트 환경 — 64비트 Windows 11 권장");
        if (SelectedFeatureIds().Any(id => FeatureCatalog.All.First(f => f.Id == id).RequiresElevation))
        {
            AddBody("선택 기능 중 관리자 권한이 필요한 항목이 있습니다.");
        }
    }

    private void EnsurePathBox()
    {
        if (_pathBox is not null)
        {
            return;
        }

        _pathBox = new TextBox
        {
            Text = ResolveInstallPathDefault(),
            IsReadOnly = _launchMode == SetupLaunchMode.Modify
        };
    }

    private string ResolveInstallPathDefault()
    {
        if (!string.IsNullOrWhiteSpace(_pathBox?.Text))
        {
            return _pathBox.Text.Trim();
        }

        return TryReadExistingTarget()
            ?? AppExecutableResolver.DefaultInstallDirectory();
    }

    private void BrowseInstallPath()
    {
        if (_pathBox is null || _launchMode == SetupLaunchMode.Modify)
        {
            return;
        }

        var initialPath = Directory.Exists(_pathBox.Text) ? _pathBox.Text : ResolveInstallPathDefault();
        var dialog = new OpenFolderDialog
        {
            Title = "PC 케어 프로 설치 폴더를 선택하세요.",
            InitialDirectory = initialPath,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _pathBox.Text = dialog.FolderName;
        }
    }

    private void BuildInstallPreview()
    {
        EnsurePathBox();
        AddTitle(_launchMode == SetupLaunchMode.Repair ? "복구 진행" : "설치 진행");
        AddBody($"대상: {_pathBox?.Text}");
        AddBody($"선택 기능: {SelectedFeatureIds().Count}개");
        AddBody(_operationRunning
            ? "파일 복사 및 설정 적용 중입니다. 창을 닫지 마세요."
            : "아래 「설치」를 누르면 진행률이 표시됩니다.");
        _progressBar = new ProgressBar
        {
            Height = 18,
            Margin = new Thickness(0, 16, 0, 0),
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };
        _progressText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = (Brush)FindResource("SpdMutedBrush"),
            Text = _operationRunning ? "작업 준비 중..." : "대기 중"
        };
        ContentPanel.Children.Add(_progressBar);
        ContentPanel.Children.Add(_progressText);
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        _operationRunning = true;
        BackButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        try
        {
            await operation();
        }
        finally
        {
            _operationRunning = false;
            BackButton.IsEnabled = _step > 0 && _step < 7;
            NextButton.IsEnabled = true;
        }
    }

    private void ReportOperationProgress((int percent, string detail) update)
    {
        Dispatcher.Invoke(() =>
        {
            if (_progressBar is not null)
            {
                _progressBar.Value = update.percent;
            }

            if (_progressText is not null)
            {
                _progressText.Text = $"{update.percent}% — {update.detail}";
            }

            if (_operationLogList is not null && !string.IsNullOrWhiteSpace(update.detail))
            {
                var stamp = DateTime.Now.ToString("HH:mm:ss");
                _operationLogList.Items.Add($"[{stamp}] {update.detail}");
                if (_operationLogList.Items.Count > 0)
                {
                    _operationLogList.ScrollIntoView(_operationLogList.Items[^1]);
                }
            }
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void BuildUninstallOptions()
    {
        AddTitle("제거 옵션");
        AddBody("제거 범위를 선택하세요. Secure Vault 데이터는 기본적으로 보존됩니다.");
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(MakeScopeRadio("프로그램만 제거", UninstallScope.ProgramOnly, true));
        panel.Children.Add(MakeScopeRadio("프로그램 + 설정 제거", UninstallScope.ProgramAndSettings, false));
        panel.Children.Add(MakeScopeRadio("프로그램 + 설정 + 보고서 제거", UninstallScope.ProgramSettingsAndReports, false));
        ContentPanel.Children.Add(panel);
    }

    private RadioButton MakeScopeRadio(string label, UninstallScope scope, bool selected)
    {
        var radio = new RadioButton
        {
            Content = label,
            IsChecked = selected,
            Margin = new Thickness(0, 6, 0, 0),
            GroupName = "uninstall-scope"
        };
        radio.Checked += (_, _) => _uninstallScope = scope;
        if (selected)
        {
            _uninstallScope = scope;
        }

        return radio;
    }

    private void BuildUninstallConfirm()
    {
        AddTitle("Secure Vault 데이터");
        AddBody("금고 데이터는 기본적으로 삭제하지 않습니다.");
        AddBody("일반 제거: 입력 없이 「제거」만 누르세요.");
        AddBody($"금고 데이터까지 삭제할 때만 아래에 「{VaultDeletePhrase}」 를 입력하세요.");
        _vaultConfirmBox = new TextBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(8),
            ToolTip = $"선택 사항 — 금고 삭제 시에만 {VaultDeletePhrase}"
        };
        ContentPanel.Children.Add(_vaultConfirmBox);
    }

    private void BuildUninstallProgress()
    {
        AddTitle("제거 진행 중");
        AddBody("아래 진행률과 로그를 확인하세요. 완료되면 자동으로 다음 화면으로 이동합니다.");
        _progressBar = new ProgressBar { Height = 20, Margin = new Thickness(0, 12, 0, 0), Minimum = 0, Maximum = 100 };
        _progressText = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = (Brush)FindResource("SpdMutedBrush"),
            Text = _operationRunning ? "작업 준비 중..." : "대기 중"
        };
        _operationLogList = new ListBox
        {
            Height = 220,
            Margin = new Thickness(0, 12, 0, 0),
            FontFamily = new FontFamily("Consolas, Malgun Gothic")
        };
        ContentPanel.Children.Add(_progressBar);
        ContentPanel.Children.Add(_progressText);
        ContentPanel.Children.Add(_operationLogList);
    }

    private void BuildUninstallDone()
    {
        AddTitle("제거 완료");
        AddBody("PC 케어 프로가 제거되었습니다.");
    }

    private void BuildDone()
    {
        AddTitle(_launchMode == SetupLaunchMode.Repair ? "복구 완료" : "설치 완료");
        AddBody($"작업 보고서는 ProgramData\\{AegisProduct.ProgramDataFolder}에 저장되었습니다.");
        var launch = new Button
        {
            Content = "프로그램 실행",
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(16, 8, 16, 8)
        };
        launch.Click += (_, _) =>
        {
            var root = _pathBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(root))
            {
                root = TryReadExistingTarget() ?? AppExecutableResolver.DefaultInstallDirectory();
            }

            var exe = AppExecutableResolver.ResolveMainExecutable(root);
            if (exe is not null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
                return;
            }

            MessageBox.Show(
                $"설치 폴더에서 PCCare.exe를 찾을 수 없습니다.\n\n경로: {root}\n\n" +
                "복구(Repair)를 실행하거나 설치를 다시 진행하세요.",
                "실행 파일 없음");
        };
        ContentPanel.Children.Add(launch);
    }

    private async Task RunRepairAsync()
    {
        var layout = InstallerPaths.ResolveLayoutDirectory();
        if (layout is null)
        {
            MessageBox.Show("설치 파일 레이아웃을 찾을 수 없습니다.", "복구 오류");
            return;
        }

        var target = _pathBox?.Text?.Trim()
            ?? TryReadExistingTarget()
            ?? AppExecutableResolver.DefaultInstallDirectory();
        var progress = new Progress<(int percent, string detail)>(ReportOperationProgress);

        try
        {
            await _repairRunner.RepairAsync(layout, target, _existingManifest, progress);
            MsiHelper.TryRepairMsi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "복구 실패");
        }
    }

    private async Task RunUninstallAsync()
    {
        var target = TryReadExistingTarget()
            ?? AppExecutableResolver.DefaultInstallDirectory();
        var deleteVault = string.Equals(_vaultConfirmBox?.Text?.Trim(), VaultDeletePhrase, StringComparison.OrdinalIgnoreCase);
        var progress = new Progress<(int percent, string detail)>(ReportOperationProgress);

        try
        {
            await _uninstallRunner.UninstallAsync(target, _uninstallScope, deleteVault, progress);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "제거 실패");
        }
    }

    private async Task RunInstallAsync()
    {
        var layout = InstallerPaths.ResolveLayoutDirectory();
        if (layout is null)
        {
            MessageBox.Show("설치 파일 레이아웃을 찾을 수 없습니다.", "설치 오류");
            return;
        }

        var targetDir = _pathBox!.Text.Trim();
        if (InstallFileOperations.RequiresElevation(targetDir) && !InstallFileOperations.IsAdministrator())
        {
            MessageBox.Show(
                "Program Files 경로에 설치하려면 관리자 권한이 필요합니다.\n\n" +
                "설치 프로그램을 마우스 오른쪽 버튼으로 클릭한 뒤 「관리자 권한으로 실행」을 선택하세요.",
                "관리자 권한 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var manifest = FeatureCatalog.CreateManifest(_mode, ProductVersion, SelectedFeatureIds());
        var progress = new Progress<(int percent, string detail)>(ReportOperationProgress);

        try
        {
            await _runner.InstallAsync(layout, targetDir, manifest, progress);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message + "\n\nPC 케어 프로가 실행 중이면 종료한 뒤 다시 시도하세요. " +
                "Program Files에 설치할 때는 설치 프로그램을 관리자 권한으로 실행해야 합니다.",
                "설치 실패");
        }
    }

    private List<string> SelectedFeatureIds()
    {
        if (_featureChecks.Count == 0)
        {
            return FeatureCatalog.CreateManifest(_mode, ProductVersion, []).Features
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();
        }

        return _featureChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
    }

    private bool IsFeatureSelected(string featureId)
    {
        var feature = FeatureCatalog.All.First(f => f.Id == featureId);
        if (feature.IsRequired)
        {
            return true;
        }

        if (SelectedFeatureIds().Contains(featureId, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return _mode switch
        {
            InstallMode.Full => true,
            InstallMode.Recommended => feature.IncludedInRecommended,
            InstallMode.Minimal => feature.IncludedInMinimal,
            _ => false
        };
    }

    private bool NeedsSystemConsent() =>
        IsFeatureSelected(InstallFeatureIds.SystemCare)
        || IsFeatureSelected(InstallFeatureIds.DriverAudioRepair)
        || IsFeatureSelected(InstallFeatureIds.RegistryDoctor)
        || IsFeatureSelected(InstallFeatureIds.DiskDoctor)
        || IsFeatureSelected(InstallFeatureIds.VulnerabilityFix);

    private static InstalledFeaturesManifest? TryLoadExistingManifest()
    {
        var candidates = new[]
        {
            Path.Combine(InstallerPaths.ProgramDataRoot, "installed_features.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SmartPerformanceDoctor", "installed_features.json")
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<InstalledFeaturesManifest>(
                File.ReadAllText(path),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadExistingTarget()
    {
        var candidates = new[]
        {
            Path.Combine(InstallerPaths.ProgramDataRoot, "install_report.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SmartPerformanceDoctor", "install_report.json")
        };
        var reportPath = candidates.FirstOrDefault(File.Exists);
        if (reportPath is null)
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(reportPath));
            return doc.RootElement.TryGetProperty("targetDirectory", out var target)
                ? target.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static double GetFreeSpaceGb(string driveRoot)
    {
        try
        {
            var drive = new DriveInfo(driveRoot);
            return drive.IsReady ? drive.AvailableFreeSpace / (1024.0 * 1024 * 1024) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private void AddTitle(string text) =>
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("SpdTextBrush")
        });

    private void AddBody(string text) =>
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = (Brush)FindResource("SpdMutedBrush")
        });

    private void AddScrollText(string text)
    {
        var viewer = new ScrollViewer { MaxHeight = 180, Margin = new Thickness(0, 10, 0, 0) };
        viewer.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ContentPanel.Children.Add(viewer);
    }

    private CheckBox AddCheck(string text)
    {
        var check = new CheckBox { Content = text, Margin = new Thickness(0, 12, 0, 0) };
        ContentPanel.Children.Add(check);
        return check;
    }
}