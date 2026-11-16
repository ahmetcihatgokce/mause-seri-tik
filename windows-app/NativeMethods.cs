using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MauseSeriTik
{
    internal static class NativeMethods
    {
        internal const int WhMouseLl = 14;
        internal const int WmLButtonDown = 0x0201;
        internal const int WmLButtonUp = 0x0202;
        internal const int WmHotkey = 0x0312;
        internal const uint MouseInjected = 0x03;
        internal const uint MouseLeftDown = 0x02;
        internal const uint MouseLeftUp = 0x04;
        internal const uint RootAncestor = 2;
        internal const uint ModNoRepeat = 0x4000;
        internal const uint HighResolutionTimer = 0x02;
        internal const uint TimerAccess = 0x00100002;

        internal delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HookData
        {
            internal Point Position;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Input
        {
            internal uint Type;
            internal MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            internal int X;
            internal int Y;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr SetWindowsHookEx(int type, MouseHook callback, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(Point point);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, StringBuilder title, int maxLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint access);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr callback,
            IntPtr argument, bool resume);
    }
}
