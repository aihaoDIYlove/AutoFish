using System.Windows.Threading;
using AutoFish.Helpers;
using AutoFish.Models;

namespace AutoFish.Services;

public class DetectionLoop : IDisposable
{
    private readonly AppSettings _settings;
    private readonly OcrService _ocr;
    private readonly InputService _input;
    private readonly FishingStateMachine _fsm;
    private readonly DispatcherTimer _timer;
    private int _tickCount;

    public event Action<bool>? StateChanged;
    public event Action<string>? TextRecognized;
    public event Action<FishingState, FishingState>? FishStateChanged;
    public event Action<string>? DebugInfo;

    private bool _running;
    public bool IsRunning
    {
        get => _running;
        private set
        {
            if (_running != value)
            {
                _running = value;
                StateChanged?.Invoke(value);
            }
        }
    }

    public DetectionLoop(AppSettings settings, OcrService ocr, InputService input)
    {
        _settings = settings;
        _ocr = ocr;
        _input = input;

        _fsm = new FishingStateMachine(settings);
        _fsm.RightClickRequested += OnRightClickRequested;
        _fsm.StateChanged += (old, @new) => FishStateChanged?.Invoke(old, @new);
        _fsm.DebugInfo += msg => DebugInfo?.Invoke(msg);

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(settings.PollingIntervalMs),
            DispatcherPriority.Background,
            OnTick,
            Dispatcher.CurrentDispatcher);
        _timer.Stop(); // 四参数构造自动启动，显式停止
    }

    public void Start()
    {
        if (IsRunning) return;
        if (!_ocr.IsAvailable)
        {
            var msg = "OCR 引擎不可用。\n请安装中文语言包的光学字符识别组件：\n设置 → 时间和语言 → 语言 → 添加语言 → 中文(简体) → 可选功能 → 光学字符识别";
            DebugInfo?.Invoke(msg);
            System.Windows.MessageBox.Show(msg, "AutoFish", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        _tickCount = 0;
        _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs);
        _timer.Start();
        IsRunning = true;
        Logger.Info($"检测已启动: 区域=({_settings.CaptureX},{_settings.CaptureY}) {_settings.CaptureWidth}x{_settings.CaptureHeight} 间隔={_settings.PollingIntervalMs}ms");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();
        IsRunning = false;
        Logger.Info("检测已停止");
        TextRecognized?.Invoke("");
        DebugInfo?.Invoke("已停止");
    }

    public void ResetStateMachine()
    {
        _fsm.Reset();
        DebugInfo?.Invoke("状态机已重置");
    }

    private void OnRightClickRequested(string reason)
    {
        Logger.Info($"右键触发: {reason}");
        DebugInfo?.Invoke($"右键: {reason}");
        _input.SendRightClick();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;

        try
        {
            using var frame = ScreenCapture.CaptureRegion(
                _settings.CaptureX,
                _settings.CaptureY,
                _settings.CaptureWidth,
                _settings.CaptureHeight);

            IReadOnlyList<string> lines;
            try
            {
                lines = _ocr.RecognizeLines(frame);
            }
            catch (Exception ex)
            {
                Logger.Error("OCR 异常", ex);
                DebugInfo?.Invoke($"OCR 异常: {ex.Message}");
                return;
            }

            var joined = string.Join(" | ", lines);
            TextRecognized?.Invoke(joined);

            if (_settings.DebugLogOcr && lines.Count > 0)
                Logger.Info($"OCR: [{joined}]");

            if (_tickCount % 10 == 0 && lines.Count > 0)
                DebugInfo?.Invoke($"OCR({lines.Count}行): {joined}");

            if (lines.Count > 0)
                _fsm.Process(lines);
        }
        catch (Exception ex)
        {
            Logger.Error("检测循环异常", ex);
            DebugInfo?.Invoke($"循环异常: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
