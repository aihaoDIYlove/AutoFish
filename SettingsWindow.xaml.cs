using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoFish.Models;
using AutoFish.Services;

namespace AutoFish;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private bool _rodTabLoaded;

    public event Action? SettingsSaved;

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        Loaded += (_, _) => LoadSettings();
    }

    /// <summary>首次切到"更换鱼竿"选项卡时填充控件值</summary>
    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rodTabLoaded) return;
        // 检查当前选中项是否为"更换鱼竿"（索引 1）
        if (SettingsTabs.SelectedIndex != 1) return;

        // 首次选中：控件已由 WPF 创建，现在可以安全填充
        _rodTabLoaded = true;
        LoadRodSettings();
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void LoadSettings()
    {
        TxtCaptureX.Text = _settings.CaptureX.ToString();
        TxtCaptureY.Text = _settings.CaptureY.ToString();
        TxtCaptureW.Text = _settings.CaptureWidth.ToString();
        TxtCaptureH.Text = _settings.CaptureHeight.ToString();
        SetPollingRadio(_settings.PollingIntervalMs);
        ChkAutoFish.IsChecked = _settings.AutoFishEnabled;
        ChkDebugLog.IsChecked = _settings.DebugLogOcr;

        TxtCastPhrases.Text = string.Join(", ", _settings.CastPhrases);
        TxtBitePhrases.Text = string.Join(", ", _settings.BitePhrases);
        TxtReelPhrases.Text = string.Join(", ", _settings.ReelPhrases);

        TxtCastCooldown.Text = _settings.CastCooldownMs.ToString();
        TxtRecastDelay.Text = _settings.RecastDelayMs.ToString();
        TxtClickDuration.Text = _settings.ClickDurationMs.ToString();
        TxtHookCooldown.Text = _settings.HookCooldownMs.ToString();
    }

    /// <summary>填充切杆选项卡控件值（首次切换时调用）</summary>
    private void LoadRodSettings()
    {
        if (ChkAutoSwitchRod == null) return;
        ChkAutoSwitchRod.IsChecked = _settings.AutoSwitchRodEnabled;
        if (ChkDebugLogInput != null) ChkDebugLogInput.IsChecked = _settings.DebugLogInput;
        if (TxtSwitchRodDelay != null) TxtSwitchRodDelay.Text = _settings.SwitchRodDelayMs.ToString();
        if (TxtSwitchRodRecast != null) TxtSwitchRodRecast.Text = _settings.SwitchRodRecastMs.ToString();
        if (TxtBrokenPhrases != null) TxtBrokenPhrases.Text = string.Join(", ", _settings.BrokenPhrases);

        var rods = new[] { ChkRod1, ChkRod2, ChkRod3, ChkRod4, ChkRod5, ChkRod6, ChkRod7, ChkRod8, ChkRod9 };
        for (int i = 0; i < 9; i++)
            if (rods[i] != null)
                rods[i].IsChecked = i < _settings.RodSlots.Count ? _settings.RodSlots[i] : true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.CaptureX = int.Parse(TxtCaptureX.Text);
            _settings.CaptureY = int.Parse(TxtCaptureY.Text);
            _settings.CaptureWidth = int.Parse(TxtCaptureW.Text);
            _settings.CaptureHeight = int.Parse(TxtCaptureH.Text);
            _settings.PollingIntervalMs = GetPollingValue();
            _settings.AutoFishEnabled = ChkAutoFish.IsChecked == true;
            _settings.DebugLogOcr = ChkDebugLog.IsChecked == true;

            _settings.CastPhrases = ParsePhrases(TxtCastPhrases.Text);
            _settings.BitePhrases = ParsePhrases(TxtBitePhrases.Text);
            _settings.ReelPhrases = ParsePhrases(TxtReelPhrases.Text);

            _settings.CastCooldownMs = int.Parse(TxtCastCooldown.Text);
            _settings.RecastDelayMs = int.Parse(TxtRecastDelay.Text);
            _settings.ClickDurationMs = int.Parse(TxtClickDuration.Text);
            _settings.HookCooldownMs = int.Parse(TxtHookCooldown.Text);

            // 切杆设置（仅在用户访问过选项卡后读取，否则保持磁盘值）
            if (_rodTabLoaded)
            {
                _settings.AutoSwitchRodEnabled = ChkAutoSwitchRod.IsChecked == true;
                _settings.DebugLogInput = ChkDebugLogInput.IsChecked == true;
                _settings.SwitchRodDelayMs = int.Parse(TxtSwitchRodDelay.Text);
                _settings.SwitchRodRecastMs = int.Parse(TxtSwitchRodRecast.Text);
                _settings.BrokenPhrases = ParsePhrases(TxtBrokenPhrases.Text);

                var rods = new[] { ChkRod1, ChkRod2, ChkRod3, ChkRod4, ChkRod5, ChkRod6, ChkRod7, ChkRod8, ChkRod9 };
                _settings.RodSlots.Clear();
                for (int i = 0; i < 9; i++)
                    _settings.RodSlots.Add(rods[i].IsChecked == true);
            }

            _settingsService.Save();
            SettingsSaved?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetPollingRadio(int ms)
    {
        (ms switch { 400 => (RadioButton)Rb400, 330 => Rb330, 200 => Rb200, 125 => Rb125, _ => Rb250 }).IsChecked = true;
    }

    private int GetPollingValue()
    {
        if (Rb125.IsChecked == true) return 125;
        if (Rb200.IsChecked == true) return 200;
        if (Rb330.IsChecked == true) return 330;
        if (Rb400.IsChecked == true) return 400;
        return 250;
    }

    private static List<string> ParsePhrases(string text)
    {
        return text
            .Split(new[] { ',', '，', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }
}
