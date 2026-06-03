using System.Windows;
using System.Windows.Interop;
using AutoFish.Helpers;

namespace AutoFish;

public partial class App : Application
{
    private OverlayWindow? _overlay;
    private ToolbarWindow? _toolbar;
    private HwndSource? _hwndSource;
    private TrayIcon? _trayIcon;

    private const int HOTKEY_TOGGLE = 1;
    private const int HOTKEY_SELECT = 2;
    private const int HOTKEY_DEBUG = 3;
    private const int HOTKEY_QUIT = 4;
    private const int HOTKEY_SETTINGS = 5;

    // 虚拟键码: F=0x46, S=0x53, D=0x44, Q=0x51, O=0x4F
    private const int VK_F = 0x46;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;
    private const int VK_Q = 0x51;
    private const int VK_O = 0x4F;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger.Clear();
        Logger.Info("=== AutoFish 启动 ===");

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("未处理异常", args.Exception);
            args.Handled = true;
        };

        try
        {
            Logger.Info("创建 OverlayWindow...");
            _overlay = new OverlayWindow();
            _overlay.Show();

            Logger.Info("创建 ToolbarWindow...");
            _toolbar = new ToolbarWindow();
            _toolbar.Bind(_overlay.Detection);
            _toolbar.ToggleDetectionRequested += () => _overlay.ToggleDetection();
            _toolbar.SelectRegionRequested += () =>
            {
                _overlay.ToggleSelectMode();
                _toolbar.SetSelectingMode(_overlay.IsSelecting);
            };
            _toolbar.SettingsRequested += () => _overlay.OpenSettings();
            _toolbar.HideRequested += () => { _overlay.HideOverlay(); _toolbar.Hide(); };

            _overlay.SelectModeChanged += (selecting) =>
                _toolbar.SetSelectingMode(selecting);
            _toolbar.Left = SystemParameters.PrimaryScreenWidth - 400;
            _toolbar.Top = 20;
            _toolbar.Show();

            CreateMessageWindow();

            _trayIcon = new TrayIcon(_hwndSource!.Handle);
            _trayIcon.OpenRequested += OnTrayOpen;
            _trayIcon.ExitRequested += OnTrayExit;
            _trayIcon.Show("AutoFish — 自动钓鱼");

            RegisterHotKeys();
            Logger.Info("启动完成");
        }
        catch (Exception ex)
        {
            Logger.Error("启动失败", ex);
            MessageBox.Show($"启动失败:\n\n{ex.Message}",
                "AutoFish 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("=== AutoFish 退出 ===");
        UnregisterHotKeys();
        _trayIcon?.Dispose();
        _hwndSource?.Dispose();
        _toolbar?.Close();
        _overlay?.Close();
        base.OnExit(e);
    }

    private void OnTrayOpen()
    {
        Dispatcher.Invoke(() =>
        {
            if (_overlay?.IsVisible == true)
            {
                _overlay.HideOverlay();
                _toolbar?.Hide();
            }
            else
            {
                _overlay?.ShowOverlay();
                _toolbar?.Show();
            }
        });
    }

    private void OnTrayExit()
    {
        Dispatcher.Invoke(() =>
        {
            _overlay?.Close();
            _toolbar?.Close();
            Shutdown();
        });
    }

    private void CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("AutoFish_HotkeyWindow")
        {
            Width = 0, Height = 0, WindowStyle = 0,
            ExtendedWindowStyle = Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HOTKEY_TOGGLE:
                    _overlay?.Dispatcher.Invoke(() => _overlay.ToggleDetection());
                    break;
                case HOTKEY_SELECT:
                    _overlay?.Dispatcher.Invoke(() =>
                    {
                        _overlay.ToggleSelectMode();
                        _toolbar?.SetSelectingMode(_overlay.IsSelecting);
                    });
                    break;
                case HOTKEY_DEBUG:
                    _overlay?.Dispatcher.Invoke(() => _overlay.ToggleDebug());
                    break;
                case HOTKEY_QUIT:
                    _overlay?.Dispatcher.Invoke(() => { _overlay?.HideOverlay(); _toolbar?.Hide(); });
                    break;
                case HOTKEY_SETTINGS:
                    _overlay?.Dispatcher.Invoke(() => _overlay?.OpenSettings());
                    break;
            }
            handled = true;
        }

        if (msg == 0x0111)
        {
            _trayIcon?.HandleCommand((uint)(int)wParam);
            handled = true;
        }

        _trayIcon?.HandleMessage((uint)msg, wParam, lParam);
        return IntPtr.Zero;
    }

    private void RegisterHotKeys()
    {
        var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            Win32.RegisterHotKey(hwnd, HOTKEY_TOGGLE,   Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, VK_F);
            Win32.RegisterHotKey(hwnd, HOTKEY_SELECT,   Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, VK_S);
            Win32.RegisterHotKey(hwnd, HOTKEY_DEBUG,    Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, VK_D);
            Win32.RegisterHotKey(hwnd, HOTKEY_QUIT,     Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, VK_Q);
            Win32.RegisterHotKey(hwnd, HOTKEY_SETTINGS, Win32.MOD_CONTROL | Win32.MOD_SHIFT | Win32.MOD_NOREPEAT, VK_O);
        }
        catch (Exception) { Logger.Info("热键注册失败（可能被其他程序占用）"); }
    }

    private void UnregisterHotKeys()
    {
        var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;
        Win32.UnregisterHotKey(hwnd, HOTKEY_TOGGLE);
        Win32.UnregisterHotKey(hwnd, HOTKEY_SELECT);
        Win32.UnregisterHotKey(hwnd, HOTKEY_DEBUG);
        Win32.UnregisterHotKey(hwnd, HOTKEY_QUIT);
        Win32.UnregisterHotKey(hwnd, HOTKEY_SETTINGS);
    }
}
