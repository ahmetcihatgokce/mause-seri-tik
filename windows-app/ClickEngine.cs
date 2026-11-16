using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MauseSeriTik
{
    internal sealed class ClickEngine : IDisposable
    {
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;
        private readonly NativeMethods.MouseHook callback;
        private readonly IntPtr hook;
        private readonly uint markerPrefix = (uint)new Random().Next(1, 65536) << 16;
        private TargetWindow target;
        private ClickSequence sequence;
        private TargetWindow sequenceWindow;
        private Injection pending;
        private ClickMode mode;
        private int rate;
        private int count;
        private bool armed;
        private bool shutdown;
        private string failure;
        private uint markerCounter;

        internal ClickEngine()
        {
            callback = OnMouse;
            hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, callback,
                NativeMethods.GetModuleHandle(null), 0);
            HookError = hook == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
            worker = new Thread(Run) { IsBackground = true, Name = "Mause Seri Tık" };
            worker.Start();
        }

        internal int HookError { get; private set; }
        internal bool HookInstalled { get { return hook != IntPtr.Zero; } }
        internal bool HasStopped { get { return !worker.IsAlive; } }
        internal bool Armed { get { lock (gate) return armed; } }
        internal string Failure { get { lock (gate) return failure; } }

        internal void Configure(ClickMode mode, int rate, int count)
        {
            lock (gate)
            {
                this.mode = mode;
                this.rate = rate;
                this.count = count;
                CancelSequence();
            }
            wake.Set();
        }

        internal void SetTarget(TargetWindow window)
        {
            lock (gate)
            {
                target = window;
                armed = false;
                CancelSequence();
                failure = null;
            }
            wake.Set();
        }

        internal bool Arm()
        {
            lock (gate)
            {
                if (shutdown || !HookInstalled || target == null || !target.Exists())
                    return false;
                failure = null;
                armed = true;
                return true;
            }
        }

        internal void Disarm()
        {
            lock (gate)
            {
                if (shutdown)
                    return;
                armed = false;
                CancelSequence();
                wake.Set();
            }
        }

        internal void RequestShutdown()
        {
            lock (gate)
            {
                shutdown = true;
                armed = false;
                CancelSequence();
            }
            wake.Set();
        }

        // The worker finishes any outstanding release before its WaitHandle is disposed.
        public void Dispose()
        {
            if (hook != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(hook);
            wake.Dispose();
        }

        private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
        {
            bool down = message == new IntPtr(NativeMethods.WmLButtonDown);
            if (code >= 0 && (down || message == new IntPtr(NativeMethods.WmLButtonUp)))
            {
                var info = (NativeMethods.HookData)Marshal.PtrToStructure(data, typeof(NativeMethods.HookData));
                ulong marker = info.ExtraInfo.ToUInt64();
                if ((info.Flags & NativeMethods.MouseInjected) == 0)
                    HandlePhysicalClick(down, info.Position);
                else if (marker >> 16 == markerPrefix >> 16 && HandleInjectedClick(down, (uint)marker, info.Position))
                    return new IntPtr(1);
            }
            return NativeMethods.CallNextHookEx(hook, code, message, data);
        }

        private void HandlePhysicalClick(bool down, NativeMethods.Point point)
        {
            lock (gate)
            {
                if (down)
                {
                    if (sequence != null || !armed || target == null || !target.AcceptsPoint(point))
                        return;
                    sequence = new ClickSequence(mode, rate, count, Stopwatch.GetTimestamp());
                    sequenceWindow = target;
                    wake.Set();
                    return;
                }

                if (sequence == null)
                    return;
                // Windows receives the real up, which also closes any synthetic down already delivered.
                sequence = null;
                sequenceWindow = null;
                wake.Set();
            }
        }

        private bool HandleInjectedClick(bool down, uint marker, NativeMethods.Point point)
        {
            lock (gate)
            {
                if (pending == null || pending.Marker != marker || pending.Down != down)
                    return true;
                pending.Observed = true;
                wake.Set();
                if (pending.Sequence != sequence)
                    return true;

                if (!sequenceWindow.AcceptsPoint(point))
                    sequence.Cancel();
                return !sequence.ApplyOutput(down, Stopwatch.GetTimestamp());
            }
        }

        private void Run()
        {
            int inputSize = Marshal.SizeOf(typeof(NativeMethods.Input));
            var input = new NativeMethods.Input[1];
            using (var timer = new ClickTimer(wake))
            {
                while (true)
                {
                    ClickSequence current;
                    TargetWindow window;
                    long due;
                    lock (gate)
                    {
                        current = sequence;
                        window = sequenceWindow;
                        due = current == null ? ClickSequence.Finished : current.Due;
                        if (shutdown && due == ClickSequence.Finished)
                            return;
                    }

                    if (due == ClickSequence.Finished)
                    {
                        wake.WaitOne();
                        continue;
                    }

                    long now = Stopwatch.GetTimestamp();
                    if (due != 0 && (now - due > Stopwatch.Frequency || !window.AcceptsCursor()))
                    {
                        lock (gate)
                            current.Cancel();
                        continue;
                    }
                    if (now < due)
                    {
                        timer.WaitUntil(Math.Min(due, now + ClickSequence.Milliseconds(25)));
                        continue;
                    }

                    Injection request = Prepare(current);
                    if (request == null)
                        continue;
                    input[0].Mouse.Flags = request.Down ? NativeMethods.MouseLeftDown : NativeMethods.MouseLeftUp;
                    input[0].Mouse.ExtraInfo = new UIntPtr(request.Marker);

                    // The hook validates queued input after release/stop and acknowledges each packet.
                    uint sent = NativeMethods.SendInput(1, input, inputSize);
                    if (sent == 0 && !request.Down)
                        sent = NativeMethods.SendInput(1, input, inputSize);
                    WaitForHook(request, sent, timer, input, inputSize);
                }
            }
        }

        private Injection Prepare(ClickSequence current)
        {
            lock (gate)
            {
                if (sequence != current || current.Due == ClickSequence.Finished)
                    return null;
                uint marker = markerPrefix | (++markerCounter & 0xFFFF);
                pending = new Injection(current, marker, current.WantsDown);
                return pending;
            }
        }

        private void WaitForHook(Injection request, uint sent, ClickTimer timer, NativeMethods.Input[] input, int inputSize)
        {
            long deadline = Stopwatch.GetTimestamp() + ClickSequence.Milliseconds(500);
            while (true)
            {
                lock (gate)
                {
                    if (request.Observed || sequence != request.Sequence)
                    {
                        pending = null;
                        return;
                    }
                    if (sent == 0 || Stopwatch.GetTimestamp() >= deadline)
                    {
                        request.Sequence.Abandon();
                        pending = null;
                        armed = false;
                        failure = "Windows fare olayını doğrulayamadı. Sol tuşu bırakın ve uygulamayı yeniden açın.";
                        break;
                    }
                }
                timer.WaitUntil(deadline);
            }

            // If Windows removed the low-level hook, a sent down may require a balancing up.
            if (sent != 0 && request.Down)
            {
                input[0].Mouse.Flags = NativeMethods.MouseLeftUp;
                input[0].Mouse.ExtraInfo = UIntPtr.Zero;
                NativeMethods.SendInput(1, input, inputSize);
            }
        }

        private void CancelSequence()
        {
            if (sequence != null)
                sequence.Cancel();
        }

        private sealed class Injection
        {
            internal readonly ClickSequence Sequence;
            internal readonly uint Marker;
            internal readonly bool Down;
            internal bool Observed;

            internal Injection(ClickSequence sequence, uint marker, bool down)
            {
                Sequence = sequence;
                Marker = marker;
                Down = down;
            }
        }

        private sealed class ClickTimer : WaitHandle
        {
            private readonly AutoResetEvent wake;
            private readonly WaitHandle[] waits;

            internal ClickTimer(AutoResetEvent wake)
            {
                this.wake = wake;
                IntPtr timer = NativeMethods.CreateWaitableTimerEx(IntPtr.Zero, null,
                    NativeMethods.HighResolutionTimer, NativeMethods.TimerAccess);
                if (timer == IntPtr.Zero)
                    timer = NativeMethods.CreateWaitableTimerEx(IntPtr.Zero, null, 0, NativeMethods.TimerAccess);
                if (timer != IntPtr.Zero)
                {
                    SafeWaitHandle = new SafeWaitHandle(timer, true);
                    waits = new WaitHandle[] { wake, this };
                }
            }

            internal void WaitUntil(long deadline)
            {
                long remaining = deadline - Stopwatch.GetTimestamp();
                if (remaining <= 0)
                    return;

                long interval = -Math.Max(1, remaining * 10000000 / Stopwatch.Frequency);
                if (waits != null && NativeMethods.SetWaitableTimer(SafeWaitHandle.DangerousGetHandle(),
                    ref interval, 0, IntPtr.Zero, IntPtr.Zero, false))
                    WaitAny(waits);
                else
                    wake.WaitOne((int)Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency));
            }
        }
    }
}
