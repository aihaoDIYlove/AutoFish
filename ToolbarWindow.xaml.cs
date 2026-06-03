using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AutoFish.Services;

namespace AutoFish;

public partial class ToolbarWindow : Window
{
    private DetectionLoop? _detection;
    private bool _selecting;

    public event Action? ToggleDetectionRequested;
    public event Action? SelectRegionRequested;
    public event Action? SettingsRequested;
    public event Action? HideRequested;

    public ToolbarWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    public void Bind(DetectionLoop detection)
    {
        _detection = detection;
        _detection.StateChanged += OnDetectionStateChanged;
        _detection.FishStateChanged += OnFishStateChanged;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_detection == null) return;
        _detection.StateChanged -= OnDetectionStateChanged;
        _detection.FishStateChanged -= OnFishStateChanged;
    }

    public void SetSelectingMode(bool selecting)
    {
        _selecting = selecting;
        BtnSelect.Content = selecting ? "取消" : "框选区";
    }

    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleDetectionRequested?.Invoke();

    private void BtnSelect_Click(object sender, RoutedEventArgs e) =>
        SelectRegionRequested?.Invoke();

    private void BtnSettings_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke();

    private void BtnHide_Click(object sender, RoutedEventArgs e) =>
        HideRequested?.Invoke();

    private void OnDetectionStateChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            if (running)
            {
                BtnToggle.Content = "⏸ 停止检测";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x44));
            }
            else
            {
                BtnToggle.Content = "▶ 开始检测";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x44, 0x44));
            }
        });
    }

    private void OnFishStateChanged(FishingState oldState, FishingState newState)
    {
        Dispatcher.Invoke(() =>
        {
            StateLabel.Text = newState switch
            {
                FishingState.Idle => "未钓鱼 — 请手动抛竿",
                FishingState.Fishing => "钓鱼中 — 等待咬钩...",
                FishingState.ReelingIn => "收回中 — 已提竿",
                FishingState.ReeledIn => "已收回 — 准备重抛",
                _ => "状态未知"
            };
        });
    }

}
