using System.Runtime.InteropServices;
using AutoFish.Helpers;

namespace AutoFish.Services;

public class InputService
{
    private readonly int _clickDurationMs;

    /// <summary>由 DetectionLoop 在启动/设置变更时同步</summary>
    public bool DebugLogInput { get; set; }

    public InputService(int clickDurationMs = 50)
    {
        _clickDurationMs = clickDurationMs;
    }

    public void SendRightClick()
    {
        var r1 = SendMouseEvent(Win32.MOUSEEVENTF_RIGHTDOWN);
        Thread.Sleep(_clickDurationMs);
        var r2 = SendMouseEvent(Win32.MOUSEEVENTF_RIGHTUP);

        if (DebugLogInput)
            Logger.Info($"SendRightClick: sizeof={Marshal.SizeOf<Win32.INPUT>()}, down={r1}, up={r2}");
    }

    public void SendKey(int vkCode, int durationMs = 50)
    {
        var r1 = SendKeyEvent(vkCode, Win32.KEYEVENTF_KEYDOWN);
        Thread.Sleep(durationMs);
        var r2 = SendKeyEvent(vkCode, Win32.KEYEVENTF_KEYUP);

        if (DebugLogInput)
            Logger.Info($"SendKey(0x{vkCode:X}): sizeof={Marshal.SizeOf<Win32.INPUT>()}, down={r1}, up={r2}");
    }

    private static uint SendMouseEvent(uint flags)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_MOUSE,
                u = new Win32.INPUTUNION
                {
                    mi = new Win32.MOUSEINPUT
                    {
                        dx = 0, dy = 0, mouseData = 0,
                        dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };
        return Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static uint SendKeyEvent(int vkCode, uint flags)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };
        return Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }
}
