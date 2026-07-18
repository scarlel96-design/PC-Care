using System.Collections.ObjectModel;
using SmartPerformanceDoctor.AstraVault.Legacy;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Security;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class SecureVaultViewModel : ObservableObject, IDisposable
{
    private readonly SecureVaultService _service = new();

    private string _statusLine = "파일·폴더를 암호화해 보관하는 보안 금고입니다.";
    private string _stateLine = "금고 없음";
    private string _breadcrumb = "보관 항목";
    private string _securityLine = "";
    private string _securityStateLine = "Locked";
    private string _sessionCountdownLine = "";
    private string _cryptoLine = "레거시: Argon2id · HKDF · AES-GCM · → 목표: AVLT v3 · XChaCha20-Poly1305 · VMK/DEK · journal";
    private int _autoLockMinutes = 5;
    private bool _isBusy;
    private bool _isNotCreated = true;
    private bool _isLocked;
    private bool _isUnlocked;
    private bool _canNavigateBack;
    private bool _needsKdfMigration;
    private string? _currentBundleId;
    private string _currentRelativePrefix = "";

    public ObservableCollection<SecureVaultBrowsableItem> VisibleItems { get; } = new();

    public string StatusLine { get => _statusLine; private set => Set(ref _statusLine, value); }
    public string StateLine { get => _stateLine; private set => Set(ref _stateLine, value); }
    public string Breadcrumb { get => _breadcrumb; private set => Set(ref _breadcrumb, value); }
    public string SecurityLine { get => _securityLine; private set => Set(ref _securityLine, value); }
    public string SecurityStateLine { get => _securityStateLine; private set => Set(ref _securityStateLine, value); }
    public string SessionCountdownLine { get => _sessionCountdownLine; private set => Set(ref _sessionCountdownLine, value); }
    public string CryptoLine { get => _cryptoLine; private set => Set(ref _cryptoLine, value); }
    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set
        {
            var clamped = Math.Clamp(value, 1, 60);
            if (_autoLockMinutes == clamped)
            {
                return;
            }

            Set(ref _autoLockMinutes, clamped);
            _service.ApplyLabAutoLockMinutes(_autoLockMinutes);
            RefreshSessionCountdown();
        }
    }

    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public bool IsNotCreated { get => _isNotCreated; private set => Set(ref _isNotCreated, value); }
    public bool IsLocked { get => _isLocked; private set => Set(ref _isLocked, value); }
    public bool IsUnlocked { get => _isUnlocked; private set => Set(ref _isUnlocked, value); }
    public bool CanNavigateBack { get => _canNavigateBack; private set => Set(ref _canNavigateBack, value); }
    public bool NeedsKdfMigration { get => _needsKdfMigration; private set => Set(ref _needsKdfMigration, value); }

    public SecureVaultViewModel() => RefreshState();

    public void SetStatus(string message) => StatusLine = message;

    public event EventHandler? AutoLockRequested;

    public void TouchActivity()
    {
        _service.TouchLabActivity();
        AutoLockRequested?.Invoke(this, EventArgs.Empty);
        RefreshSessionCountdown();
    }

    public void RefreshSessionCountdown()
    {
        if (!_service.IsLabVaultFormat || !IsUnlocked)
        {
            SessionCountdownLine = "";
            return;
        }

        SessionCountdownLine = _service.GetLabSessionCountdownLine();
        // keep SecurityStateLine in sync when idle warning flips AutoLockScheduled
        if (_service.IsLabVaultFormat)
        {
            SecurityStateLine = _service.GetLabSecurityStateLabel();
        }
    }

    public void RefreshState()
    {
        IsNotCreated = _service.State == SecureVaultState.NotCreated;
        IsLocked = _service.State == SecureVaultState.Locked;
        IsUnlocked = _service.State == SecureVaultState.Unlocked;
        var rootCount = _service.State == SecureVaultState.Unlocked
            ? _service.GetBrowsableItems(null, "").Count
            : 0;
        StateLine = _service.State switch
        {
            SecureVaultState.NotCreated => "금고 없음 · 새 금고 만들기",
            SecureVaultState.Locked => "잠김 · 비밀번호로 열기",
            _ => $"열림 · 항목 {rootCount}개"
        };
        ReloadVisibleItems();
        RefreshSecurityStatus();
        if (IsUnlocked && _service.IsLabVaultFormat)
        {
            _service.ApplyLabAutoLockMinutes(AutoLockMinutes);
        }

        RefreshSessionCountdown();
        NeedsKdfMigration = _service.NeedsKdfMigration;
    }

    public void NavigateInto(SecureVaultBrowsableItem item)
    {
        if (!item.IsNavigable)
        {
            return;
        }

        _currentBundleId = item.BundleId ?? item.EntryId;
        _currentRelativePrefix = item.Kind == SecureVaultBrowsableKind.FolderRoot
            ? ""
            : item.RelativePrefix;
        Breadcrumb = item.Kind == SecureVaultBrowsableKind.FolderRoot
            ? item.DisplayName
            : $"{Breadcrumb} / {item.DisplayName}";
        ReloadVisibleItems();
    }

    public void NavigateBack()
    {
        if (_currentBundleId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentRelativePrefix))
        {
            ResetNavigation();
        }
        else
        {
            var trimmed = _currentRelativePrefix.TrimEnd('/').Replace('\\', '/');
            var lastSlash = trimmed.LastIndexOf('/');
            _currentRelativePrefix = lastSlash < 0 ? "" : trimmed[..(lastSlash + 1)];
            var folderName = _service.Entries.FirstOrDefault(e => e.BundleId == _currentBundleId && e.Kind == SecureVaultEntryKind.FolderRoot)?.DisplayLabel ?? "폴더";
            Breadcrumb = string.IsNullOrWhiteSpace(_currentRelativePrefix)
                ? folderName
                : $"{folderName} / {trimmed[(lastSlash + 1)..]}";
        }

        ReloadVisibleItems();
    }

    public SecureVaultOperationResult CreateVault(string password, string? recoveryHint)
    {
        var result = _service.CreateVault(password, recoveryHint);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public SecureVaultOperationResult Unlock(string password)
    {
        var result = _service.Unlock(password);
        StatusLine = result.Message;
        ResetNavigation();
        RefreshState();
        if (result.Success)
        {
            TouchActivity();
        }

        return result;
    }

    public SecureVaultOperationResult UnlockReadOnly(string password)
    {
        if (!_service.IsLabVaultFormat)
        {
            var msg = "읽기 전용 열기는 보안 금고 v5 Lab 경로에서만 지원됩니다.";
            StatusLine = msg;
            return new SecureVaultOperationResult { Success = false, Message = msg };
        }

        var result = _service.UnlockReadOnlyLab(password);
        StatusLine = result.Message;
        ResetNavigation();
        RefreshState();
        if (result.Success)
        {
            SecurityStateLine = _service.GetLabSecurityStateLabel();
            TouchActivity();
        }

        return result;
    }

    public SecureVaultOperationResult UnlockWithRecoveryKey(string recoveryKey)
    {
        var result = _service.UnlockWithRecoveryKey(recoveryKey);
        StatusLine = result.Message;
        ResetNavigation();
        RefreshState();
        if (result.Success)
        {
            TouchActivity();
        }

        return result;
    }

    public SecureVaultOperationResult MigrateKdfToArgon2(string masterPassword)
    {
        var result = _service.MigrateKdfToArgon2(masterPassword);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public SecureVaultOperationResult MigrateLegacyToLabV4(string masterPassword)
    {
        var result = _service.MigrateLegacyVaultToLabV4(masterPassword);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public bool IsLabVaultFormat => _service.IsLabVaultFormat;

    public SecureVaultOperationResult ChangeMasterPassword(string currentPassword, string newPassword)
    {
        var result = _service.ChangeMasterPassword(currentPassword, newPassword);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public SecureVaultOperationResult ReissueRecoveryCodes(string password)
    {
        if (!_service.IsLabVaultFormat)
        {
            var msg = "복구 코드 재발급은 보안 금고 v5 Lab 경로에서만 지원됩니다.";
            StatusLine = msg;
            return new SecureVaultOperationResult { Success = false, Message = msg };
        }

        var result = _service.ReissueLabRecoveryCodes(password);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    /// <summary>Lab v5: change password may return new recovery codes list.</summary>
    public bool LastChangeReturnedRecoveryCodes(SecureVaultOperationResult result) =>
        result.RecoveryCodes is { Count: > 0 };

    public void Lock()
    {
        _service.Lock();
        StatusLine = "금고를 잠갔습니다.";
        ResetNavigation();
        RefreshState();
    }

    public async Task<SecureVaultOperationResult> AddFileAsync(
        string path,
        IProgress<SecureVaultProgressReport>? progress = null,
        bool sealOrigin = true)
    {
        IsBusy = true;
        try
        {
            TouchActivity();
            var result = await Task.Run(async () =>
                await _service.AddFileAsync(path, sealOrigin, cancellationToken: default, progress: progress)
                    .ConfigureAwait(false));
            StatusLine = result.Message;
            if (result.Success)
            {
                ResetNavigation();
            }

            RefreshState();
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<SecureVaultOperationResult> AddFolderAsync(
        string path,
        IProgress<SecureVaultProgressReport>? progress = null,
        bool sealOrigin = true)
    {
        IsBusy = true;
        try
        {
            TouchActivity();
            var result = await Task.Run(async () =>
                await _service.AddFolderAsync(path, sealOrigin, cancellationToken: default, progress: progress)
                    .ConfigureAwait(false));
            StatusLine = result.Message;
            if (result.Success)
            {
                ResetNavigation();
            }

            RefreshState();
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<SecureVaultOperationResult> ExportEntryAsync(
        SecureVaultBrowsableItem item,
        string destination,
        bool stepUpConfirmed = false)
    {
        IsBusy = true;
        try
        {
            SecureVaultOperationResult result;
            if (item.Kind is SecureVaultBrowsableKind.FolderRoot)
            {
                result = item.EntryId is not null
                    ? await _service.ExportEntryAsync(item.EntryId, destination, stepUpConfirmed: stepUpConfirmed)
                    : item.BundleId is not null
                        ? await _service.ExportBundleAsync(item.BundleId, destination)
                        : new SecureVaultOperationResult { Success = false, Message = "폴더 항목을 찾을 수 없습니다." };
            }
            else if (item.Kind == SecureVaultBrowsableKind.SubFolder && item.BundleId is not null)
            {
                result = new SecureVaultOperationResult
                {
                    Success = false,
                    Message = "하위 폴더는 최상위 폴더를 선택해 보내기하세요."
                };
            }
            else
            {
                result = await _service.ExportEntryAsync(item.EntryId!, destination, stepUpConfirmed: stepUpConfirmed);
            }

            StatusLine = result.Message;
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<SecureVaultOperationResult> RestoreToOriginAsync(
        SecureVaultBrowsableItem item,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        IsBusy = true;
        try
        {
            TouchActivity();
            SecureVaultOperationResult result = await Task.Run(async () =>
            {
                if (item.Kind == SecureVaultBrowsableKind.FolderRoot)
                {
                    return item.EntryId is not null
                        ? await _service.RestoreToOriginAsync(item.EntryId, progress: progress).ConfigureAwait(false)
                        : item.BundleId is not null
                            ? await _service.RestoreBundleToOriginAsync(item.BundleId, progress: progress).ConfigureAwait(false)
                            : new SecureVaultOperationResult { Success = false, Message = "폴더 항목을 찾을 수 없습니다." };
                }

                if (item.Kind is SecureVaultBrowsableKind.File or SecureVaultBrowsableKind.StandaloneFile)
                {
                    return await _service.RestoreToOriginAsync(item.EntryId!, progress: progress).ConfigureAwait(false);
                }

                return new SecureVaultOperationResult { Success = false, Message = "파일 또는 최상위 폴더를 선택하세요." };
            });

            StatusLine = result.Message;
            if (result.Success)
            {
                ResetNavigation();
                RefreshState();
            }

            return result;
        }
        catch (Exception ex)
        {
            StatusLine = $"복원 실패: {ex.Message}";
            return new SecureVaultOperationResult { Success = false, Message = StatusLine };
        }
        finally
        {
            IsBusy = false;
        }
    }

    public SecureVaultOperationResult RemoveFromVault(SecureVaultBrowsableItem item)
    {
        SecureVaultOperationResult result;
        if (item.Kind == SecureVaultBrowsableKind.FolderRoot)
        {
            result = item.EntryId is not null
                ? _service.RemoveFromVault(item.EntryId)
                : item.BundleId is not null
                    ? _service.RemoveBundleFromVault(item.BundleId)
                    : new SecureVaultOperationResult { Success = false, Message = "폴더 항목을 찾을 수 없습니다." };
        }
        else if (item.Kind is SecureVaultBrowsableKind.File or SecureVaultBrowsableKind.StandaloneFile)
        {
            result = _service.RemoveFromVault(item.EntryId!);
        }
        else
        {
            result = new SecureVaultOperationResult { Success = false, Message = "파일 또는 최상위 폴더를 선택하세요." };
        }

        StatusLine = result.Message;
        ResetNavigation();
        RefreshState();
        return result;
    }

    public SecureVaultIntegrityResult VerifyIntegrity()
    {
        var result = _service.RepairIntegrity();
        StatusLine = result.Message;
        if (result.RepairedEntries > 0 || result.Success)
        {
            RefreshState();
        }

        return result;
    }

    public SecureVaultOperationResult CompactPacks()
    {
        var result = _service.CompactLabPacks(userConfirmed: true);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public SecureVaultOperationResult RepairActivation()
    {
        var result = _service.RepairLabActivation();
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public void Dispose() => _service.Dispose();

    private void RefreshSecurityStatus()
    {
        if (_service.State == SecureVaultState.NotCreated)
        {
            SecurityStateLine = "금고 없음";
            SecurityLine = "마스터 비밀번호로 새 금고를 만드세요.";
            CryptoLine = "강력한 암호화 · 복구 코드 지원";
            return;
        }

        var status = _service.GetSecurityStatus();
        if (_service.IsLabVaultFormat)
        {
            SecurityStateLine = _service.GetLabSecurityStateLabel();
            var recLine = string.IsNullOrWhiteSpace(status.RecoveryStatusLine)
                ? _service.GetLabRecoveryStatusLine()
                : status.RecoveryStatusLine;
            SecurityLine = recLine;
            CryptoLine = status.AuditChainValid ? "보안 상태 정상" : "보안 점검 필요";
            return;
        }

        SecurityStateLine = _service.State switch
        {
            SecureVaultState.NotCreated => "금고 없음",
            SecureVaultState.Locked => "잠김",
            SecureVaultState.Unlocked => "열림",
            _ => _service.State.ToString()
        };

        SecurityLine = status.RecoveryKeyConfigured ? "복구 키 설정됨" : "복구 키 없음";
        CryptoLine = "암호화 보호 중";
    }



    private void ResetNavigation()
    {
        _currentBundleId = null;
        _currentRelativePrefix = "";
        Breadcrumb = "보관 항목";
    }

    private void ReloadVisibleItems()
    {
        VisibleItems.Clear();
        CanNavigateBack = _currentBundleId is not null;
        if (_service.State != SecureVaultState.Unlocked)
        {
            OnPropertyChanged(nameof(VisibleItems));
            return;
        }

        foreach (var item in _service.GetBrowsableItems(_currentBundleId, _currentRelativePrefix))
        {
            VisibleItems.Add(item);
        }

        OnPropertyChanged(nameof(VisibleItems));
    }
}