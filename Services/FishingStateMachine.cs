using AutoFish.Models;

namespace AutoFish.Services;

public enum FishingState
{
    Idle,
    Fishing,
    ReelingIn,
    ReeledIn
}

public class FishingStateMachine
{
    private readonly AppSettings _settings;
    private DateTime _stateEnteredAt;
    private DateTime _lastHookTime = DateTime.MinValue;
    private bool _cooldownActive;

    public FishingState CurrentState { get; private set; } = FishingState.Idle;

    public event Action<string>? RightClickRequested;
    public event Action<FishingState, FishingState>? StateChanged;
    public event Action<string>? DebugInfo;

    public FishingStateMachine(AppSettings settings)
    {
        _settings = settings;
        _stateEnteredAt = DateTime.Now;
    }

    public void Process(IReadOnlyList<string> textLines)
    {
        var now = DateTime.Now;

        switch (CurrentState)
        {
            case FishingState.Idle:
                ProcessIdle(textLines);
                break;

            case FishingState.Fishing:
                ProcessFishing(textLines, now);
                break;

            case FishingState.ReelingIn:
                ProcessReelingIn(textLines);
                break;

            case FishingState.ReeledIn:
                ProcessReeledIn(now);
                break;
        }
    }

    public void Reset()
    {
        TransitionTo(FishingState.Idle, "手动重置");
    }

    private int _idleDebugCounter;

    private void ProcessIdle(IReadOnlyList<string> textLines)
    {
        if (!_settings.AutoFishEnabled) return;

        if (MatchesAny(textLines, _settings.CastPhrases))
        {
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "检测到抛竿");
        }
        else if (textLines.Count > 0 && ++_idleDebugCounter % 10 == 0)
        {
            var sample = string.Join(" | ", textLines.Take(3));
            DebugInfo?.Invoke($"未匹配: [{sample}]");
        }
    }

    private void ProcessFishing(IReadOnlyList<string> textLines, DateTime now)
    {
        if (!_settings.AutoFishEnabled) return;

        if (_cooldownActive)
        {
            var elapsed = (now - _stateEnteredAt).TotalMilliseconds;
            if (elapsed < _settings.CastCooldownMs)
                return;
            _cooldownActive = false;
        }

        if ((now - _lastHookTime).TotalMilliseconds < _settings.HookCooldownMs)
            return;

        if (MatchesAny(textLines, _settings.BitePhrases))
        {
            _lastHookTime = now;
            RightClickRequested?.Invoke(string.Join(", ", textLines));
            TransitionTo(FishingState.ReelingIn, "检测到咬钩");
        }
    }

    private void ProcessReelingIn(IReadOnlyList<string> textLines)
    {
        if (!_settings.AutoFishEnabled) return;

        if (MatchesAny(textLines, _settings.ReelPhrases))
        {
            TransitionTo(FishingState.ReeledIn, "检测到收回");
        }
    }

    private void ProcessReeledIn(DateTime now)
    {
        if (!_settings.AutoFishEnabled) return;

        var elapsed = (now - _stateEnteredAt).TotalMilliseconds;
        if (elapsed >= _settings.RecastDelayMs)
        {
            RightClickRequested?.Invoke("自动重抛");
            _cooldownActive = true;
            TransitionTo(FishingState.Fishing, "自动重抛");
        }
    }

    private void TransitionTo(FishingState newState, string reason)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        _stateEnteredAt = DateTime.Now;
        DebugInfo?.Invoke($"[状态机] {oldState} → {newState} ({reason})");
        StateChanged?.Invoke(oldState, newState);
    }

    private static bool MatchesAny(IReadOnlyList<string> textLines, List<string> phrases)
    {
        foreach (var line in textLines)
        {
            var normalized = Normalize(line);
            foreach (var phrase in phrases)
            {
                var normalizedPhrase = Normalize(phrase);
                if (normalized.Contains(normalizedPhrase))
                    return true;
            }
        }
        return false;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var result = text.ToLowerInvariant()
            .Replace('：', ':')
            .Replace('，', ',')
            .Replace('（', '(')
            .Replace('）', ')');

        result = result.Replace(" ", "").Replace("\u00A0", "");

        var chars = new char[result.Length];
        int pos = 0;
        foreach (var c in result)
        {
            if (char.IsLetterOrDigit(c))
                chars[pos++] = c;
        }

        return new string(chars, 0, pos);
    }
}
