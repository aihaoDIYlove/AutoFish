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

    public int CastCooldownMs { get; set; } = 800;
    public int RecastDelayMs { get; set; } = 1200;
    public int ClickDurationMs { get; set; } = 50;
    public int HookCooldownMs { get; set; } = 2200;

    public bool AutoFishEnabled { get; set; } = true;
    public bool DebugLogOcr { get; set; } = false;
}
