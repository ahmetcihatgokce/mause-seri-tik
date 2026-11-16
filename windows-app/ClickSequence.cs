using System;
using System.Diagnostics;

namespace MauseSeriTik
{
    internal enum ClickMode
    {
        Hold,
        Limited
    }

    internal sealed class ClickSequence
    {
        internal const long Finished = -1;

        private readonly long interval;
        private readonly long pressDuration;
        private readonly int limit;
        private long lastDown;
        private long nextEvent;
        private int clicks = 1;
        private bool repeating = true;
        private bool syntheticDown;

        internal ClickSequence(ClickMode mode, int clicksPerSecond, int burstCount, long start)
        {
            lastDown = start;
            interval = Stopwatch.Frequency / clicksPerSecond;
            pressDuration = Math.Min(interval / 2, Milliseconds(25));
            limit = mode == ClickMode.Hold ? 0 : burstCount;
            nextEvent = start + pressDuration;
            OutputDown = true;
        }

        private bool OutputDown { get; set; }
        internal bool WantsDown { get { return repeating && !OutputDown && (limit == 0 || clicks < limit); } }

        internal long Due
        {
            get
            {
                if (syntheticDown)
                    return repeating ? nextEvent : 0;
                return repeating && (limit == 0 || clicks < limit) ? nextEvent : Finished;
            }
        }

        internal void Cancel()
        {
            repeating = false;
        }

        internal void Abandon()
        {
            repeating = false;
            syntheticDown = false;
        }

        internal bool ApplyOutput(bool down, long now)
        {
            if (down)
            {
                if (!WantsDown)
                    return false;
                if (limit > 0)
                    clicks++;
                lastDown = now;
                nextEvent = now + pressDuration;
            }
            else
            {
                // Once cancelled, only a synthetic down needs a synthetic cleanup release.
                if (!OutputDown || (!syntheticDown && !repeating))
                    return false;
                nextEvent = Math.Max(lastDown + interval, now + pressDuration);
            }

            OutputDown = down;
            syntheticDown = down;
            return true;
        }

        internal static long Milliseconds(int value)
        {
            return value * Stopwatch.Frequency / 1000;
        }
    }
}
