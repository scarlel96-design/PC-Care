using System.Collections.ObjectModel;
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
    private string _cryptoLine = "Argon2id · HKDF · AES-256-GCM · DEK 이중 봉인 · 샤드 MAC · DPAPI · NTFS ACL";
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
    public string CryptoLine { get => _cryptoLine; private set => Set(ref _cryptoLine, value); }
    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set => Set(ref _autoLockMinutes, Math.Clamp(value, 1, 60));
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

    public void TouchActivity() => AutoLockRequested?.Invoke(this, EventArgs.Empty);

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

    public SecureVaultOperationResult ChangeMasterPassword(string currentPassword, string newPassword)
    {
        var result = _service.ChangeMasterPassword(currentPassword, newPassword);
        StatusLine = result.Message;
        RefreshState();
        return result;
    }

    public void Lock()
    {
        _service.Lock();
        StatusLine = "금고를 잠갔습니다.";
        ResetNavigation();
        RefreshState();
    }

    public async Task<SecureVaultOperationResult> AddFileAsync(
        string path,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        IsBusy = true;
        try
        {
            TouchActivity();
            var result = await Task.Run(async () =>
                await _service.AddFileAsync(path, progress: progress).ConfigureAwait(false));
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
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        IsBusy = true;
        try
        {
            TouchActivity();
            var result = await Task.Run(async () =>
                await _service.AddFolderAsync(path, progress: progress).ConfigureAwait(false));
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

    public async Task<SecureVaultOperationResult> ExportEntryAsync(SecureVaultBrowsableItem item, string destination)
    {
        IsBusy = true;
        try
        {
            SecureVaultOperationResult result;
            if (item.Kind is SecureVaultBrowsableKind.FolderRoot)
            {
                result = item.EntryId is not null
                    ? await _service.ExportEntryAsync(item.EntryId, destination)
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
                result = await _service.ExportEntryAsync(item.EntryId!, destination);
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

    public void Dispose() => _service.Dispose();

    private void RefreshSecurityStatus()
    {
        if (_service.State == SecureVaultState.NotCreated)
        {
            SecurityLine = $"신규 금고: {SecureVaultPasswordPolicy.MinLengthNewVault}자+ · 대소문자·숫자·특수문자";
            CryptoLine = "Argon2id · HKDF · AES-256-GCM · DEK 이중 봉인 · 샤드 MAC · DPAPI · NTFS ACL";
            return;
        }

        var status = _service.GetSecurityStatus();
        SecurityLine = $"KDF {status.KdfIterations:N0}회 · ACL {(status.AclHardened ? "적용" : "미적용")} · 복구키 {(status.RecoveryKeyConfigured ? "설정됨" : "없음")} · 감사 {status.AuditEntryCount}건 {(status.AuditChainValid ? "정상" : "주의")}";
        CryptoLine = status.CryptoStack;
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