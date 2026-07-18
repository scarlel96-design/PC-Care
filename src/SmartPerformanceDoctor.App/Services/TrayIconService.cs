using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.App.Services;

/// <summary>PC 케어 프로 아이콘 — 전용 STA 스레드에서 NotifyIcon 호스팅.</summary>
public sealed class TrayIconService : IDisposable
{
    public static TrayIconService Shared { get; } = new();

    private readonly object _gate = new();
    private Thread? _staThread;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _initialized;
    private bool _disposed;

    private TrayIconService()
    {
    }

    public void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_initialized || _disposed)
            {
                return;
            }

            _initialized = true;
            _staThread = new Thread(RunStaMessageLoop)
            {
                IsBackground = true,
                Name = "PCCare-TrayIcon"
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
        }
    }

    public void ShowBalloon(string title, string message, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info)
    {
        EnsureInitialized();
        if (_notifyIcon is null)
        {
            return;
        }

        try
        {
            _notifyIcon.ShowBalloonTip(4000, title, message, icon);
        }
        catch
        {
            // cosmetic
        }
    }

    private void RunStaMessageLoop()
    {
        try
        {
            System.Drawing.Icon? icon = null;
            var iconPath = ProductIconService.ResolveIconPath();
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                try
                {
                    icon = new System.Drawing.Icon(iconPath);
                }
                catch
                {
                    icon = null;
                }
            }

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = icon ?? System.Drawing.SystemIcons.Application,
                Text = AppInfo.ProductName,
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("PC 케어 프로 열기", null, (_, _) => ShowMainWindow());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("종료", null, (_, _) => ExitApplication());
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("tray-icon-init", ex, ex.Message);
        }
    }

    private static void ShowMainWindow()
    {
        UiDispatcher.Run(() =>
        {
            TrayIconService.Shared.EnsureInitialized();
            App.Shell?.ShowFromTray();
        });
    }

    private static void ExitApplication()
    {
        UiDispatcher.Run(() =>
        {
            App.Shell?.RequestForceShutdown();
            Shared.Dispose();
            if (Microsoft.UI.Xaml.Application.Current is not null)
            {
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
        });
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            try
            {
                System.Windows.Forms.Application.ExitThread();
            }
            catch
            {
                // ignore
            }

            _staThread = null;
            _initialized = false;
        }
    }
}