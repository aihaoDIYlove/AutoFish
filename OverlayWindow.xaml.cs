using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AutoFish.Helpers;
using AutoFish.Models;
using AutoFish.Services;

namespace AutoFish;

public partial class OverlayWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly OcrService _ocr;
    private readonly InputService _input;
    private double _dpiScaleX = 1.0, _dpiScaleY = 1.0;

    private bool _isSelecting;
    private bool _isAdjusting;
    private Rect _savedRect;
    public bool IsSelecting => _isSelecting || _isAdjusting;
    public event Action<bool>? SelectModeChanged;

    private enum DragMode { None, Draw, Move, ResizeNW, ResizeNE, ResizeSW, ResizeSE }
    private DragMode _dragMode;
    private Point _dragStart;
    private bool _isDragging;
    private bool _showDebug;

    public DetectionLoop Detection { get; }

    public OverlayWindow()
    {
        InitializeComponent();
        Logger.Info("OverlayWindow 瀹屾垚");

        _settingsService = new SettingsService();
        _settingsService.Load();
        _settings = _settingsService.Settings;

        _ocr = new OcrService();
        _input = new InputService(_settings.ClickDurationMs);
        var rodSwitch = new RodSwitchService(_settings, _input);
        Detection = new DetectionLoop(_settings, _ocr, _input, rodSwitch);

        Detection.StateChanged += _ => { };
        Detection.TextRecognized += OnTextRecognized;
        Detection.FishStateChanged += (_, _) => { };
        Detection.DebugInfo += OnDebugInfo;

        Loaded += OnLoaded;
        Closed += (_, _) => { Detection.Stop(); Detection.Dispose(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            _dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }
        SetClickThrough(true);
        UpdateSelectionVisual();
    }

    private void SetClickThrough(bool ct)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var es = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE).ToInt32();
        if (ct) { es |= Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE; }
        else { es &= ~Win32.WS_EX_TRANSPARENT; es &= ~Win32.WS_EX_NOACTIVATE; }
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, (IntPtr)es);
    }

    private void UpdateSelectionVisual()
    {
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        var sx = _settings.CaptureX / _dpiScaleX;
        var sy = _settings.CaptureY / _dpiScaleY;
        var sw2 = Math.Max(1, _settings.CaptureWidth / _dpiScaleX);
        var sh2 = Math.Max(1, _settings.CaptureHeight / _dpiScaleY);

        OverlayPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, sw, sh)),
            new RectangleGeometry(new Rect(sx, sy, sw2, sh2)));

        SelectionBorder.Data = new RectangleGeometry(
            new Rect(sx - 1, sy - 1, sw2 + 2, sh2 + 2));

        UpdateHandlePositions(sx, sy, sw2, sh2);
    }

    private void UpdateHandlePositions(double x, double y, double w, double h)
    {
        HandleNW.Margin = new Thickness(x - 5, y - 5, 0, 0);
        HandleNE.Margin = new Thickness(x + w - 5, y - 5, 0, 0);
        HandleSW.Margin = new Thickness(x - 5, y + h - 5, 0, 0);
        HandleSE.Margin = new Thickness(x + w - 5, y + h - 5, 0, 0);

        ConfirmLabel.Margin = new Thickness(x + w + 8, y - 4, 0, 0);

        double labelX = x, labelY = y - 28;
        if (labelY < 0) labelY = y + h + 6;
        SizeLabel.Margin = new Thickness(labelX, labelY, 0, 0);
        SizeLabelText.Text = $"{_settings.CaptureWidth} 脳 {_settings.CaptureHeight}";
    }

    private void ShowHandles(bool show)
    {
        var v = show ? Visibility.Visible : Visibility.Collapsed;
        HandleNW.Visibility = v;
        HandleNE.Visibility = v;
        HandleSW.Visibility = v;
        HandleSE.Visibility = v;
        ConfirmLabel.Visibility = v;
    }

    public void EnterSelectMode()
    {
        if (_isSelecting || _isAdjusting) return;
        if (Detection.IsRunning) Detection.Stop();

        _savedRect = new Rect(
            _settings.CaptureX, _settings.CaptureY,
            _settings.CaptureWidth, _settings.CaptureHeight);

        _isSelecting = true;
        SelectModeChanged?.Invoke(true);
        SetClickThrough(false);
        Cursor = Cursors.Cross;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x9F, 0x0A));
        SelectionBorder.StrokeDashArray = null;
        SelectionBorder.StrokeThickness = 3;

        ShowHandles(false);
        SizeLabel.Visibility = Visibility.Collapsed;

        MouseLeftButtonDown += OnDrawMouseDown;
        MouseMove += OnDrawMouseMove;
        MouseLeftButtonUp += OnDrawMouseUp;
        KeyDown += OnDrawKeyDown;
        Focusable = true;
        Focus();
    }

    private void EnterAdjustMode()
    {
        _isSelecting = false;
        _isAdjusting = true;
        _dragMode = DragMode.None;
        Cursor = Cursors.Arrow;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
        SelectionBorder.StrokeDashArray = new DoubleCollection { 6, 3 };
        SelectionBorder.StrokeThickness = 2;

        ShowHandles(true);
        SizeLabel.Visibility = Visibility.Visible;
        UpdateSelectionVisual();

        MouseLeftButtonDown -= OnDrawMouseDown;
        MouseMove -= OnDrawMouseMove;
        MouseLeftButtonUp -= OnDrawMouseUp;

        MouseLeftButtonDown += OnAdjustMouseDown;
        MouseMove += OnAdjustMouseMove;
        MouseLeftButtonUp += OnAdjustMouseUp;
        KeyDown += OnAdjustKeyDown;
        Focusable = true;
        Focus();
    }

    public void ExitSelectMode(bool save)
    {
        if (!_isSelecting && !_isAdjusting) return;

        if (!save && _isAdjusting)
        {
            _settings.CaptureX = (int)_savedRect.X;
            _settings.CaptureY = (int)_savedRect.Y;
            _settings.CaptureWidth = (int)_savedRect.Width;
            _settings.CaptureHeight = (int)_savedRect.Height;
        }

        _isSelecting = false;
        _isAdjusting = false;
        _dragMode = DragMode.None;
        _isDragging = false;
        SelectModeChanged?.Invoke(false);

        SetClickThrough(true);
        Cursor = Cursors.Arrow;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
        SelectionBorder.StrokeDashArray = new DoubleCollection { 6, 3 };
        SelectionBorder.StrokeThickness = 2;

        ShowHandles(false);
        SizeLabel.Visibility = Visibility.Collapsed;

        MouseLeftButtonDown -= OnDrawMouseDown;
        MouseMove -= OnDrawMouseMove;
        MouseLeftButtonUp -= OnDrawMouseUp;
        MouseLeftButtonDown -= OnAdjustMouseDown;
        MouseMove -= OnAdjustMouseMove;
        MouseLeftButtonUp -= OnAdjustMouseUp;
        KeyDown -= OnDrawKeyDown;
        KeyDown -= OnAdjustKeyDown;

        if (save) _settingsService.Save();
        UpdateSelectionVisual();
    }

    private void OnDrawMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = true;
        _settings.CaptureX = (int)(_dragStart.X * _dpiScaleX);
        _settings.CaptureY = (int)(_dragStart.Y * _dpiScaleY);
        _settings.CaptureWidth = 1;
        _settings.CaptureHeight = 1;
        CaptureMouse();
    }

    private void OnDrawMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cur = e.GetPosition(this);
        var x1 = Math.Min(_dragStart.X, cur.X);
        var y1 = Math.Min(_dragStart.Y, cur.Y);
        var x2 = Math.Max(_dragStart.X, cur.X);
        var y2 = Math.Max(_dragStart.Y, cur.Y);
        _settings.CaptureX = (int)(x1 * _dpiScaleX);
        _settings.CaptureY = (int)(y1 * _dpiScaleY);
        _settings.CaptureWidth = (int)((x2 - x1) * _dpiScaleX);
        _settings.CaptureHeight = (int)((y2 - y1) * _dpiScaleY);
        UpdateSelectionVisual();
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabelText.Text = $"{_settings.CaptureWidth} 脳 {_settings.CaptureHeight}";
    }

    private void OnDrawMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        if (_settings.CaptureWidth <= 10 && _settings.CaptureHeight <= 10 && _savedRect.Width > 10)
        {
            _settings.CaptureX = (int)_savedRect.X;
            _settings.CaptureY = (int)_savedRect.Y;
            _settings.CaptureWidth = (int)_savedRect.Width;
            _settings.CaptureHeight = (int)_savedRect.Height;
        }
        else
        {
            if (_settings.CaptureWidth < 10) _settings.CaptureWidth = 10;
            if (_settings.CaptureHeight < 10) _settings.CaptureHeight = 10;
        }

        UpdateSelectionVisual();
        EnterAdjustMode();
    }

    private void OnDrawKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ExitSelectMode(false);
    }

    private void OnAdjustMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = true;

        var sx = _settings.CaptureX / _dpiScaleX;
        var sy = _settings.CaptureY / _dpiScaleY;
        var sw = _settings.CaptureWidth / _dpiScaleX;
        var sh = _settings.CaptureHeight / _dpiScaleY;
        double cx = _dragStart.X, cy = _dragStart.Y;
        const double hz = 16;

        if (Math.Abs(cx - sx) < hz && Math.Abs(cy - sy) < hz)
            _dragMode = DragMode.ResizeNW;
        else if (Math.Abs(cx - (sx + sw)) < hz && Math.Abs(cy - sy) < hz)
            _dragMode = DragMode.ResizeNE;
        else if (Math.Abs(cx - sx) < hz && Math.Abs(cy - (sy + sh)) < hz)
            _dragMode = DragMode.ResizeSW;
        else if (Math.Abs(cx - (sx + sw)) < hz && Math.Abs(cy - (sy + sh)) < hz)
            _dragMode = DragMode.ResizeSE;
        else if (cx >= sx && cx <= sx + sw && cy >= sy && cy <= sy + sh)
            _dragMode = DragMode.Move;
        else
        {
            _isDragging = false;

            double distX = 0, distY = 0;
            if (cx < sx) distX = sx - cx;
            else if (cx > sx + sw) distX = cx - (sx + sw);
            if (cy < sy) distY = sy - cy;
            else if (cy > sy + sh) distY = cy - (sy + sh);
            if (Math.Sqrt(distX * distX + distY * distY) > 60)
                ExitSelectMode(true);
        }

        if (_isDragging)
            CaptureMouse();
    }

    private void OnAdjustMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cur = e.GetPosition(this);
        var dx = (int)((cur.X - _dragStart.X) * _dpiScaleX);
        var dy = (int)((cur.Y - _dragStart.Y) * _dpiScaleY);

        switch (_dragMode)
        {
            case DragMode.Move:
                _settings.CaptureX += dx;
                _settings.CaptureY += dy;
                break;
            case DragMode.ResizeNW:
                _settings.CaptureX += dx; _settings.CaptureY += dy;
                _settings.CaptureWidth -= dx; _settings.CaptureHeight -= dy;
                break;
            case DragMode.ResizeNE:
                _settings.CaptureY += dy;
                _settings.CaptureWidth += dx; _settings.CaptureHeight -= dy;
                break;
            case DragMode.ResizeSW:
                _settings.CaptureX += dx;
                _settings.CaptureWidth -= dx; _settings.CaptureHeight += dy;
                break;
            case DragMode.ResizeSE:
                _settings.CaptureWidth += dx; _settings.CaptureHeight += dy;
                break;
        }

        if (_settings.CaptureWidth < 10) _settings.CaptureWidth = 10;
        if (_settings.CaptureHeight < 10) _settings.CaptureHeight = 10;

        _dragStart = cur;
        UpdateSelectionVisual();
    }

    private void OnAdjustMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _dragMode = DragMode.None;
        ReleaseMouseCapture();
    }

    private void OnAdjustKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ExitSelectMode(true);
        else if (e.Key == Key.Escape)
            ExitSelectMode(false);
    }

    private void Confirm_Click(object sender, MouseButtonEventArgs e)
    {
        ExitSelectMode(true);
    }

    private void Handle_MouseDown(object sender, MouseButtonEventArgs e) { }

    private void OnTextRecognized(string text)
    {
        if (!_showDebug) return;
        Dispatcher.Invoke(() =>
            DebugText.Text = string.IsNullOrWhiteSpace(text) ? "(绌?" : text);
    }

    private void OnDebugInfo(string msg)
    {
        if (!_showDebug) return;
        Dispatcher.Invoke(() =>
        {
            if (msg.Length > 50) msg = msg[..50];
            DebugState.Text = msg;
        });
    }

    public void ToggleDetection()
    {
        if (Detection.IsRunning) Detection.Stop(); else Detection.Start();
    }

    public void ToggleSelectMode()
    {
        if (_isSelecting)
            ExitSelectMode(true);
        else if (_isAdjusting)
            ExitSelectMode(true);
        else
            EnterSelectMode();
    }

    public void ToggleDebug()
    {
        _showDebug = !_showDebug;
        DebugPanel.Visibility = _showDebug ? Visibility.Visible : Visibility.Collapsed;
    }

    public void OpenSettings()
    {
        var win = new SettingsWindow(_settings, _settingsService);
        win.SettingsSaved += () => Detection.ResetStateMachine();
        win.ShowDialog();
    }

    public void HideOverlay()
    {
        if (Detection.IsRunning) Detection.Stop();
        if (_isSelecting || _isAdjusting) ExitSelectMode(true);
        Hide();
    }

    public void ShowOverlay()
    {
        Show();
        WindowState = WindowState.Maximized;
    }
}
