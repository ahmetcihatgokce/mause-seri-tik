using System;
using System.Drawing;
using System.Text;

namespace MauseSeriTik
{
    internal sealed class TargetWindow
    {
        private readonly IntPtr handle;
        private readonly uint processId;

        internal readonly string Title;

        private TargetWindow(IntPtr handle, uint processId, string title)
        {
            this.handle = handle;
            this.processId = processId;
            Title = title;
        }

        internal static TargetWindow Capture(uint ownProcessId)
        {
            IntPtr foreground = Root(NativeMethods.GetForegroundWindow());
            uint targetProcessId;
            if (foreground == IntPtr.Zero ||
                NativeMethods.GetWindowThreadProcessId(foreground, out targetProcessId) == 0 ||
                targetProcessId == ownProcessId)
                return null;

            var title = new StringBuilder(256);
            NativeMethods.GetWindowText(foreground, title, title.Capacity);
            return new TargetWindow(foreground, targetProcessId, title.Length > 0 ? title.ToString() :
                "Başlıksız pencere · " + targetProcessId);
        }

        internal bool Exists()
        {
            uint currentProcessId;
            return NativeMethods.GetWindowThreadProcessId(handle, out currentProcessId) != 0 &&
                currentProcessId == processId;
        }

        internal bool IsForeground()
        {
            return Root(NativeMethods.GetForegroundWindow()) == handle && Exists();
        }

        internal bool AcceptsPoint(NativeMethods.Point point)
        {
            return IsForeground() && Root(NativeMethods.WindowFromPoint(point)) == handle;
        }

        internal bool AcceptsCursor()
        {
            NativeMethods.Point point;
            return NativeMethods.GetCursorPos(out point) && AcceptsPoint(point);
        }

        internal bool TryGetClientBounds(out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            NativeMethods.Rect rect;
            var origin = new NativeMethods.Point();
            if (!IsForeground() || !NativeMethods.GetClientRect(handle, out rect) ||
                !NativeMethods.ClientToScreen(handle, ref origin))
                return false;

            bounds = new Rectangle(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return bounds.Width > 0 && bounds.Height > 0;
        }

        private static IntPtr Root(IntPtr window)
        {
            return NativeMethods.GetAncestor(window, NativeMethods.RootAncestor);
        }
    }
}
