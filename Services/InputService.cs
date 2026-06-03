using System.Runtime.InteropServices;
using AutoFish.Helpers;

namespace AutoFish.Services;

public class InputService
{
    private readonly int _clickDurationMs;

    public InputService(int clickDurationMs = 50)
    {
        _clickDurationMs = clickDurationMs;
    }

    public void SendRightClick()
    {
        SendMouseEvent(Win32.MOUSEEVENTF_RIGHTDOWN);
        Thread.Sleep(_clickDurationMs);
        SendMouseEvent(Win32.MOUSEEVENTF_RIGHTUP);
    }

    private static void SendMouseEvent(uint flags)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_MOUSE,
                mi = new Win32.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>());
    }
}
