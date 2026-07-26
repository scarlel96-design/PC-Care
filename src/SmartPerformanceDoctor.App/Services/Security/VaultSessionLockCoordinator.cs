using Microsoft.Win32;

namespace SmartPerformanceDoctor.App.Services.Security;

/// <summary>
/// Locks every live vault session when Windows changes session state or power state.
/// A single static subscription avoids every page instance retaining its own OS event hook.
/// </summary>
internal static class VaultSessionLockCoordinator
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<SecureVaultService>> Sessions = new();
    private static bool _subscribed;

    public static void Register(SecureVaultService service)
    {
        lock (Gate)
        {
            RemoveDeadAndMatching(service);
            Sessions.Add(new WeakReference<SecureVaultService>(service));
            EnsureSubscribed();
        }
    }

    public static void Unregister(SecureVaultService service)
    {
        lock (Gate)
        {
            RemoveDeadAndMatching(service);
        }
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            SystemEvents.SessionSwitch += (_, _) => LockAll();
            SystemEvents.PowerModeChanged += (_, _) => LockAll();
            _subscribed = true;
        }
        catch
        {
            // Idle lock remains active if the operating system event provider is unavailable.
        }
    }

    private static void LockAll()
    {
        SecureVaultService[] live;
        lock (Gate)
        {
            live = Sessions
                .Select(reference => reference.TryGetTarget(out var session) ? session : null)
                .Where(session => session is not null)
                .Cast<SecureVaultService>()
                .ToArray();
            Sessions.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        foreach (var session in live)
        {
            try { session.Lock(); }
            catch { }
        }
    }

    private static void RemoveDeadAndMatching(SecureVaultService service) =>
        Sessions.RemoveAll(reference => !reference.TryGetTarget(out var item) || ReferenceEquals(item, service));
}
