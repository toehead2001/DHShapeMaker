using PaintDotNet;
using PaintDotNet.Direct2D1;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

namespace ShapeMaker
{
    internal static class Extensions
    {
        internal static int ConstrainToInterval(this int amount, int interval)
        {
            return ConstrainToInterval((float)amount, interval);
        }

        internal static int ConstrainToInterval(this double amount, int interval)
        {
            return (int)(Math.Round(amount / interval) * interval);
        }

        internal static Point ConstrainToInterval(this Point point, int interval)
        {
            return new Point(
                ConstrainToInterval((float)point.X, interval),
                ConstrainToInterval((float)point.Y, interval));
        }

        internal static Point ConstrainToInterval(this PointF point, int interval)
        {
            return new Point(
                ConstrainToInterval(point.X, interval),
                ConstrainToInterval(point.Y, interval));
        }

        internal static Rectangle Round(this RectangleF rectangleF)
        {
            return Rectangle.Round(rectangleF);
        }

        internal static PointF Average(this IEnumerable<PathData> paths)
        {
            return paths.SelectMany(path => path.Points).ToArray().Average();
        }

        internal static PointF Average(this IReadOnlyCollection<PointF> points)
        {
            if (points.Count == 0)
            {
                return Point.Empty;
            }

            float x = 0, y = 0;
            foreach (PointF pt in points)
            {
                x += pt.X;
                y += pt.Y;
            }
            return new PointF(x / points.Count, y / points.Count);
        }

        internal static RectangleF Bounds(this IEnumerable<PathData> paths)
        {
            return paths.SelectMany(path => path.Points).ToArray().Bounds();
        }

        internal static RectangleF Bounds(this IReadOnlyCollection<PointF> points)
        {
            if (points.Count == 0)
            {
                return RectangleF.Empty;
            }

            float left = int.MaxValue;
            float top = int.MaxValue;
            float right = int.MinValue;
            float bottom = int.MinValue;

            foreach (PointF pt in points)
            {
                if (pt.X < left)
                {
                    left = pt.X;
                }

                if (pt.Y < top)
                {
                    top = pt.Y;
                }

                if (pt.X > right)
                {
                    right = pt.X;
                }

                if (pt.Y > bottom)
                {
                    bottom = pt.Y;
                }
            }

            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        internal static void Rotate(this PointF[] points, IReadOnlyList<PointF> originalPoints, double radians, PointF center)
        {
            for (int i = 0; i < points.Length; i++)
            {
                double x = originalPoints[i].X - center.X;
                double y = originalPoints[i].Y - center.Y;
                double nx = Math.Cos(radians) * x - Math.Sin(radians) * y + center.X;
                double ny = Math.Cos(radians) * y + Math.Sin(radians) * x + center.Y;

                points[i].X = (float)nx;
                points[i].Y = (float)ny;
            }
        }

        internal static void Scale(this PointF[] points, IReadOnlyList<PointF> originalPoints, float scale, PointF center)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i].X = (originalPoints[i].X - center.X) * scale + center.X;
                points[i].Y = (originalPoints[i].Y - center.Y) * scale + center.Y;
            }
        }

        internal static ReadOnlySpan<Point2Float> ToPoints(this RectFloat rectFloat)
        {
            return new Point2Float[] { rectFloat.TopLeft, rectFloat.TopRight, rectFloat.BottomRight, rectFloat.BottomLeft };
        }

        internal static IDeviceImage CreateImageFromGdiBitmap(this IDeviceContext deviceContext, Bitmap gdiBitmap)
        {
            using Surface surface = Surface.CopyFromBitmap(gdiBitmap);
            using IBitmapSource bitmapSource = surface.CreateSharedBitmap();
            return deviceContext.CreateImageFromBitmap(bitmapSource);
        }

        public static void DrawSquare(this IDeviceContext deviceContext, Point2Float center, float radius, IDeviceBrush brush, float strokeWidth = 1f, IStrokeStyle? strokeStyle = null)
        {
            RectFloat squareRect = new RectFloat(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            deviceContext.DrawRectangle(squareRect, brush, strokeWidth, strokeStyle);
        }

        public static void FillSquare(this IDeviceContext deviceContext, Point2Float center, float radius, IDeviceBrush brush)
        {
            RectFloat squareRect = new RectFloat(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            deviceContext.FillRectangle(squareRect, brush);
        }
    }
}
