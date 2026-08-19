using System.Runtime.InteropServices;

namespace DataControlRemoto;

internal static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Mouse(double x, double y, int action)
    {
        uint flags = 0x8000 | 0x0001;
        flags |= action switch { 1 => 0x0002, 2 => 0x0004, 3 => 0x0008, 4 => 0x0010, _ => 0 };
        var i = new INPUT { type = 0, U = new InputUnion { mi = new MOUSEINPUT { dx = (int)(Math.Clamp(x, 0, 1) * 65535), dy = (int)(Math.Clamp(y, 0, 1) * 65535), dwFlags = flags } } };
        SendInput(1, [i], Marshal.SizeOf<INPUT>());
    }

    public static void Key(int key, bool down)
    {
        var i = new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = down ? 0u : 0x0002u } } };
        SendInput(1, [i], Marshal.SizeOf<INPUT>());
    }
}
