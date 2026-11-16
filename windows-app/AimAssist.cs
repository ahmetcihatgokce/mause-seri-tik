using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MauseSeriTik
{
    internal sealed class AimSettings
    {
        internal readonly Color TargetColor;
        internal readonly int Radius;
        internal readonly int Tolerance;
        internal readonly double Sensitivity;

        internal AimSettings(Color targetColor, int radius, int tolerance, double sensitivity)
        {
            TargetColor = targetColor;
            Radius = Math.Max(20, Math.Min(radius, 160));
            Tolerance = Math.Max(0, Math.Min(tolerance, 90));
            Sensitivity = double.IsNaN(sensitivity) ? 1 : Math.Max(0.05, Math.Min(sensitivity, 2));
        }
    }

    internal sealed class AimAssist : IDisposable
    {
        private Bitmap capture;
        private byte[] colors;
        private byte[] matching;
        private int[] queue;

        internal static bool TryReadColor(Point screenPoint, out Color color)
        {
            color = Color.Empty;
            if (!SystemInformation.VirtualScreen.Contains(screenPoint))
                return false;

            using (var sample = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                if (!CopyScreen(sample, screenPoint))
                    return false;
                color = sample.GetPixel(0, 0);
                return true;
            }
        }

        internal bool TryFindCorrection(Rectangle clientBounds, Point aimCenter, AimSettings settings,
            out Point movement)
        {
            movement = Point.Empty;
            if (settings == null || settings.TargetColor.IsEmpty || !clientBounds.Contains(aimCenter))
                return false;

            int radius = settings.Radius;
            var square = new Rectangle(aimCenter.X - radius, aimCenter.Y - radius, radius * 2 + 1, radius * 2 + 1);
            Rectangle region = Rectangle.Intersect(Rectangle.Intersect(clientBounds, square),
                SystemInformation.VirtualScreen);
            if (region.Width < 2 || region.Height < 2 || !region.Contains(aimCenter))
                return false;

            if (capture == null || capture.Size != region.Size)
            {
                Dispose();
                capture = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            }
            if (!CopyScreen(capture, region.Location))
                return false;

            var localCenter = new Point(aimCenter.X - region.X, aimCenter.Y - region.Y);
            Point candidate;
            if (!TryFindTarget(capture, localCenter, settings, out candidate))
                return false;

            double x = (candidate.X - localCenter.X) * settings.Sensitivity;
            double y = (candidate.Y - localCenter.Y) * settings.Sensitivity;
            double distance = Math.Sqrt(x * x + y * y);
            const double maxCorrection = 120;
            if (distance > maxCorrection)
            {
                x *= maxCorrection / distance;
                y *= maxCorrection / distance;
            }

            movement = new Point((int)Math.Round(x, MidpointRounding.AwayFromZero),
                (int)Math.Round(y, MidpointRounding.AwayFromZero));
            return true;
        }

        public void Dispose()
        {
            if (capture != null)
            {
                capture.Dispose();
                capture = null;
            }
        }

        private static bool CopyScreen(Bitmap destination, Point screenLocation)
        {
            try
            {
                using (Graphics graphics = Graphics.FromImage(destination))
                    graphics.CopyFromScreen(screenLocation, Point.Empty, destination.Size, CopyPixelOperation.SourceCopy);
                return true;
            }
            catch (ExternalException)
            {
                return false;
            }
        }

        private bool TryFindTarget(Bitmap bitmap, Point center, AimSettings settings, out Point target)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int pixelCount = width * height;
            if (matching == null || matching.Length < pixelCount)
            {
                matching = new byte[pixelCount];
                queue = new int[pixelCount];
            }
            MarkMatchingPixels(bitmap, center, settings);

            target = Point.Empty;
            int bestDistance = int.MaxValue;
            int nextDistance = int.MaxValue;
            for (int index = 0; index < pixelCount; index++)
            {
                if (matching[index] == 0)
                    continue;

                Point candidate;
                if (!TryReadComponent(index, width, height, settings.Radius, out candidate))
                    continue;

                int dx = candidate.X - center.X;
                int dy = candidate.Y - center.Y;
                int distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    nextDistance = bestDistance;
                    bestDistance = distance;
                    target = candidate;
                }
                else if (distance < nextDistance)
                {
                    nextDistance = distance;
                }
            }

            // Similar distances make color-only selection ambiguous.
            return bestDistance != int.MaxValue &&
                nextDistance - bestDistance > Math.Max(4, bestDistance / 10);
        }

        private void MarkMatchingPixels(Bitmap bitmap, Point center, AimSettings settings)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var area = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = Math.Abs(data.Stride);
            try
            {
                int length = stride * height;
                if (colors == null || colors.Length < length)
                    colors = new byte[length];
                if (data.Stride > 0)
                    Marshal.Copy(data.Scan0, colors, 0, length);
                else
                    for (int y = 0; y < height; y++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), colors, y * stride, stride);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            int radiusSquared = settings.Radius * settings.Radius;
            int toleranceSquared = settings.Tolerance * settings.Tolerance;
            Color target = settings.TargetColor;
            for (int y = 0; y < height; y++)
            {
                int dy = y - center.Y;
                int offset = y * stride;
                for (int x = 0; x < width; x++, offset += 4)
                {
                    int dx = x - center.X;
                    int blue = colors[offset] - target.B;
                    int green = colors[offset + 1] - target.G;
                    int red = colors[offset + 2] - target.R;
                    matching[y * width + x] = dx * dx + dy * dy <= radiusSquared &&
                        blue * blue + green * green + red * red <= toleranceSquared ? (byte)1 : (byte)0;
                }
            }
        }

        private bool TryReadComponent(int start, int width, int height, int radius, out Point center)
        {
            int count = 0;
            int left = width, top = height, right = 0, bottom = 0;
            int totalX = 0, totalY = 0;
            Enqueue(start, ref count);
            for (int i = 0; i < count; i++)
            {
                int index = queue[i];
                int y = index / width;
                int x = index - y * width;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
                totalX += x;
                totalY += y;

                if (x > 0) Enqueue(index - 1, ref count);
                if (x + 1 < width) Enqueue(index + 1, ref count);
                if (y > 0) Enqueue(index - width, ref count);
                if (y + 1 < height) Enqueue(index + width, ref count);
            }

            int componentWidth = right - left + 1;
            int componentHeight = bottom - top + 1;
            center = Point.Empty;
            if (count < 4 || componentWidth < 2 || componentHeight < 2 ||
                componentWidth > radius || componentHeight > radius ||
                Math.Max(componentWidth, componentHeight) > Math.Min(componentWidth, componentHeight) * 6 ||
                count * 5 < componentWidth * componentHeight)
                return false;

            var centroid = new Point((totalX + count / 2) / count, (totalY + count / 2) / count);
            int bestDistance = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int y = queue[i] / width;
                int x = queue[i] - y * width;
                int dx = x - centroid.X;
                int dy = y - centroid.Y;
                int distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    center = new Point(x, y);
                    if (distance == 0)
                        break;
                }
            }
            return true;
        }

        private void Enqueue(int index, ref int count)
        {
            if (matching[index] == 0)
                return;
            matching[index] = 0;
            queue[count++] = index;
        }
    }
}
