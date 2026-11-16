using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MauseSeriTik
{
    internal sealed class SprintAssist : IDisposable
    {
        private const int LowLevelKeyboard = 13;
        private const int KeyDownMessage = 0x0100;
        private const int KeyUpMessage = 0x0101;
        private const int SystemKeyDownMessage = 0x0104;
        private const int SystemKeyUpMessage = 0x0105;
        private const uint ForwardKey = 0x57;
        private const uint ShiftKey = 0x10;
        private const uint LeftShiftKey = 0xA0;
        private const uint RightShiftKey = 0xA1;
        private const ushort LeftShiftScan = 0x2A;
        private const ushort RightShiftScan = 0x36;
        private const uint KeyboardInjected = 0x12;
        private const uint UseScanCode = 0x0008;
        private const uint KeyUp = 0x0002;
        private static readonly UIntPtr InputMarker = new UIntPtr(0x4D53524E);

        private readonly Control dispatcher;
        private readonly Timer focusTimer = new Timer { Interval = 20 };
        private readonly KeyboardHook keyboardCallback;
        private readonly IntPtr keyboardHook;
        private readonly Input[] shiftInput = new Input[1];
        private readonly int inputSize = Marshal.SizeOf(typeof(Input));
        private TargetWindow target;
        private bool enabled;
        private bool forwardDown;
        private bool forwardInTarget;
        private bool leftShiftDown;
        private bool rightShiftDown;
        private bool ownsShift;
        private bool updateQueued;
        private bool disposed;

        // Construct and configure on the form's message-loop thread.
        internal SprintAssist(Control dispatcher)
        {
            this.dispatcher = dispatcher;
            forwardDown = GetAsyncKeyState((int)ForwardKey) < 0;
            leftShiftDown = GetAsyncKeyState((int)LeftShiftKey) < 0;
            rightShiftDown = GetAsyncKeyState((int)RightShiftKey) < 0;
            keyboardCallback = OnKeyboard;
            keyboardHook = SetKeyboardHook(LowLevelKeyboard, keyboardCallback, NativeMethods.GetModuleHandle(null), 0);
            HookError = keyboardHook == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
            focusTimer.Tick += delegate { Reconcile(); };
            shiftInput[0].Type = 1;
            shiftInput[0].Data.Keyboard.ScanCode = LeftShiftScan;
            shiftInput[0].Data.Keyboard.ExtraInfo = InputMarker;
        }

        internal int HookError { get; private set; }
        internal bool HookInstalled { get { return keyboardHook != IntPtr.Zero; } }
        internal string Failure { get; private set; }

        internal void Configure(TargetWindow selectedWindow, bool active)
        {
            if (disposed)
                return;

            bool nextEnabled = active && selectedWindow != null && HookInstalled;
            if (target != selectedWindow || enabled != nextEnabled)
            {
                forwardInTarget = false;
                Failure = null;
            }
            target = selectedWindow;
            enabled = nextEnabled;
            Reconcile();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Configure(null, false);
            if (ownsShift && !leftShiftDown)
                SendShift(false);
            disposed = true;
            focusTimer.Dispose();
            if (HookInstalled)
                NativeMethods.UnhookWindowsHookEx(keyboardHook);
        }

        private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code < 0 || disposed)
                return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);

            int kind = message.ToInt32();
            bool down = kind == KeyDownMessage || kind == SystemKeyDownMessage;
            if (!down && kind != KeyUpMessage && kind != SystemKeyUpMessage)
                return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);

            var key = (KeyboardData)Marshal.PtrToStructure(data, typeof(KeyboardData));
            uint virtualKey = key.VirtualKey;
            if (virtualKey == ShiftKey)
                virtualKey = key.ScanCode == RightShiftScan ? RightShiftKey : LeftShiftKey;
            if ((key.Flags & KeyboardInjected) != 0)
            {
                // A physical Shift press can overtake a queued release. Keep that press in effect.
                if (key.ExtraInfo == InputMarker && virtualKey == LeftShiftKey &&
                    (down ? !CanOwnShift() : leftShiftDown))
                    return new IntPtr(1);
                return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);
            }

            switch (virtualKey)
            {
                case ForwardKey:
                    if (!down)
                        forwardInTarget = false;
                    else if (!forwardDown)
                        forwardInTarget = IsTargetActive();
                    forwardDown = down;
                    break;
                case LeftShiftKey:
                    leftShiftDown = down;
                    // Leave the next physical release in charge of the left Shift key.
                    if (down)
                        ownsShift = false;
                    break;
                case RightShiftKey:
                    rightShiftDown = down;
                    break;
                default:
                    return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);
            }

            if (!updateQueued && (enabled || ownsShift) && dispatcher.IsHandleCreated)
            {
                updateQueued = true;
                dispatcher.BeginInvoke((MethodInvoker)delegate
                {
                    updateQueued = false;
                    Reconcile();
                });
            }
            return NativeMethods.CallNextHookEx(keyboardHook, code, message, data);
        }

        private bool IsTargetActive()
        {
            return enabled && target != null && target.IsForeground();
        }

        private bool CanOwnShift()
        {
            return forwardInTarget && !leftShiftDown && !rightShiftDown && IsTargetActive();
        }

        private void Reconcile()
        {
            if (disposed)
                return;

            if (!IsTargetActive())
                forwardInTarget = false;
            bool holdShift = CanOwnShift();
            if (holdShift != ownsShift)
            {
                if (SendShift(holdShift))
                {
                    ownsShift = holdShift;
                    Failure = null;
                }
                else
                {
                    Failure = "Windows koşma tuşunu gönderemedi.";
                    if (holdShift)
                        forwardInTarget = false;
                }
            }
            focusTimer.Enabled = enabled || ownsShift;
        }

        private bool SendShift(bool down)
        {
            shiftInput[0].Data.Keyboard.Flags = down ? UseScanCode : UseScanCode | KeyUp;
            return SendKeyboardInput(1, shiftInput, inputSize) == 1;
        }

        private delegate IntPtr KeyboardHook(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardData
        {
            internal uint VirtualKey;
            internal uint ScanCode;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            internal ushort VirtualKey;
            internal ushort ScanCode;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputData
        {
            [FieldOffset(0)] internal KeyboardInput Keyboard;
            [FieldOffset(0)] internal NativeMethods.MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            internal uint Type;
            internal InputData Data;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
        private static extern IntPtr SetKeyboardHook(int type, KeyboardHook callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        private static extern uint SendKeyboardInput(uint count, [In] Input[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
