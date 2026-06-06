namespace AutoFish.Models;

public class AppSettings
{
    public int CaptureX { get; set; } = 100;
    public int CaptureY { get; set; } = 100;
    public int CaptureWidth { get; set; } = 320;
    public int CaptureHeight { get; set; } = 60;
    public int PollingIntervalMs { get; set; } = 250;

    public List<string> CastPhrases { get; set; } = new() { "甩出" };
    public List<string> BitePhrases { get; set; } = new() { "漂溅起水花" };
    public List<string> ReelPhrases { get; set; } = new() { "收回" };

    public int CastCooldownMs { get; set; } = 1200;
    public int RecastDelayMs { get; set; } = 1200;
    public int ClickDurationMs { get; set; } = 50;
    public int HookCooldownMs { get; set; } = 2000;

    public bool AutoFishEnabled { get; set; } = true;
    public bool DebugLogOcr { get; set; } = false;

    // --- 自动更换鱼竿 ---
    public bool AutoSwitchRodEnabled { get; set; } = false;
    public bool DebugLogInput { get; set; } = false;
    public int SwitchRodDelayMs { get; set; } = 500;
    public int SwitchRodRecastMs { get; set; } = 1200;
    public List<string> BrokenPhrases { get; set; } = new() { "品坏" };
    public List<bool> RodSlots { get; set; } = new()
    {
        true, true, true, true, true, true, true, true, true
    };
}
