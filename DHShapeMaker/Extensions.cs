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

        internal static Point Round(this PointF pointF)
        {
            return Point.Round(pointF);
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

        internal static IPathGeometry CreateArcPathGeometry(this IDirect2DFactory factory, PointF start, float radiusX, float radiusY, float angle, bool largeArc, bool positiveSweep, PointF end)
        {
            if (start == end)
            {
                throw new ArgumentException("The end point can't be equal to the start point.", nameof(end));
            }

            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);

            if (radiusX == 0.0f && radiusY == 0.0f)
            {
                return factory.CreateLinePathGeometry(start, end);
            }

            double sinPhi = Math.Sin(angle);
            double cosPhi = Math.Cos(angle);

            double x1dash = cosPhi * (start.X - end.X) / 2.0 + sinPhi * (start.Y - end.Y) / 2.0;
            double y1dash = -sinPhi * (start.X - end.X) / 2.0 + cosPhi * (start.Y - end.Y) / 2.0;

            double root;
            double numerator = radiusX * radiusX * radiusY * radiusY - radiusX * radiusX * y1dash * y1dash - radiusY * radiusY * x1dash * x1dash;

            float rx = radiusX;
            float ry = radiusY;

            if (numerator < 0.0)
            {
                float s = (float)Math.Sqrt(1.0 - numerator / (radiusX * radiusX * radiusY * radiusY));

                rx *= s;
                ry *= s;
                root = 0.0;
            }
            else
            {
                root = ((largeArc && positiveSweep) || (!largeArc && !positiveSweep) ? -1.0 : 1.0) * Math.Sqrt(numerator / (radiusX * radiusX * y1dash * y1dash + radiusY * radiusY * x1dash * x1dash));
            }

            double cxdash = root * rx * y1dash / ry;
            double cydash = -root * ry * x1dash / rx;

            double cx = cosPhi * cxdash - sinPhi * cydash + (start.X + end.X) / 2.0;
            double cy = sinPhi * cxdash + cosPhi * cydash + (start.Y + end.Y) / 2.0;

            double theta1 = VectorAngle(1.0, 0.0, (x1dash - cxdash) / rx, (y1dash - cydash) / ry);
            double dtheta = VectorAngle((x1dash - cxdash) / rx, (y1dash - cydash) / ry, (-x1dash - cxdash) / rx, (-y1dash - cydash) / ry);

            if (!positiveSweep && dtheta > 0)
            {
                dtheta -= TwoPI;
            }
            else if (positiveSweep && dtheta < 0)
            {
                dtheta += TwoPI;
            }

            int segments = (int)Math.Ceiling(Math.Abs(dtheta / (Math.PI / 2.0)));
            double delta = dtheta / segments;
            double t = 8.0 / 3.0 * Math.Sin(delta / 4.0) * Math.Sin(delta / 4.0) / Math.Sin(delta / 2.0);

            double startX = start.X;
            double startY = start.Y;

            List<Point2Float> bezierPoints = new List<Point2Float>();

            for (int i = 0; i < segments; ++i)
            {
                double cosTheta1 = Math.Cos(theta1);
                double sinTheta1 = Math.Sin(theta1);
                double theta2 = theta1 + delta;
                double cosTheta2 = Math.Cos(theta2);
                double sinTheta2 = Math.Sin(theta2);

                double endpointX = cosPhi * rx * cosTheta2 - sinPhi * ry * sinTheta2 + cx;
                double endpointY = sinPhi * rx * cosTheta2 + cosPhi * ry * sinTheta2 + cy;

                double dx1 = t * (-cosPhi * rx * sinTheta1 - sinPhi * ry * cosTheta1);
                double dy1 = t * (-sinPhi * rx * sinTheta1 + cosPhi * ry * cosTheta1);

                double dxe = t * (cosPhi * rx * sinTheta2 + sinPhi * ry * cosTheta2);
                double dye = t * (sinPhi * rx * sinTheta2 - cosPhi * ry * cosTheta2);

                bezierPoints.AddRange([
                    new Point2Float((float)startX, (float)startY),
                    new Point2Float((float)(startX + dx1), (float)(startY + dy1)),
                    new Point2Float((float)(endpointX + dxe), (float)(endpointY + dye))
                ]);

                theta1 = theta2;
                startX = (float)endpointX;
                startY = (float)endpointY;
            }

            bezierPoints.Add(new Point2Float((float)startX, (float)startY));

            return factory.CreateBeziersPathGeometry(bezierPoints.ToArray());
        }

        private const double TwoPI = Math.PI * 2.0;

        private static double VectorAngle(double ux, double uy, double vx, double vy)
        {
            double ta = Math.Atan2(uy, ux);
            double tb = Math.Atan2(vy, vx);

            if (tb >= ta)
            {
                return tb - ta;
            }

            return TwoPI - (ta - tb);
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
