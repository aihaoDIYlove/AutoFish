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
        _detection.RodSwitch.StateChanged += OnRodSwitchStateChanged;
        _detection.RodSwitch.AllExhausted += OnAllRodsExhausted;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_detection == null) return;
        _detection.StateChanged -= OnDetectionStateChanged;
        _detection.FishStateChanged -= OnFishStateChanged;
        _detection.RodSwitch.StateChanged -= OnRodSwitchStateChanged;
        _detection.RodSwitch.AllExhausted -= OnAllRodsExhausted;
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
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
            }
            else
            {
                BtnToggle.Content = "▶ 开始检测";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A));
                StateLabel.Text = "等待启用检测";
            }
        });
    }

    private void OnFishStateChanged(FishingState oldState, FishingState newState)
    {
        Dispatcher.Invoke(() =>
        {
            if (_detection?.RodSwitch.IsActive == true) return; // 让切杆状态接管

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

    private void OnRodSwitchStateChanged(RodSwitchState state)
    {
        Dispatcher.Invoke(() =>
        {
            var slot = _detection?.RodSwitch.CurrentSlot ?? 0;
            StateLabel.Text = state switch
            {
                RodSwitchState.WaitingForKeyPress => $"鱼竿损坏 — 即将切换到 #{slot + 1}...",
                RodSwitchState.WaitingForCast => $"已切换 — 等待抛竿...",
                RodSwitchState.Idle => "未钓鱼 — 请手动抛竿",
                _ => StateLabel.Text
            };
        });
    }

    private void OnAllRodsExhausted()
    {
        Dispatcher.Invoke(() =>
        {
            StateLabel.Text = "鱼竿已全部损坏 — 请更换";
        });
    }

}
