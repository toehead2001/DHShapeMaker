using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShapeMaker
{
    internal static class Extensions
    {
        internal static int ConstrainToInterval(this float amount, int interval)
        {
            return (int)(Math.Round(amount / interval) * interval);
        }

        internal static Point Round(this PointF pointF)
        {
            return Point.Round(pointF);
        }

        internal static PointF Average(this PointF[] p)
        {
            if (p.Length == 0)
            {
                return Point.Empty;
            }

            float x = 0, y = 0;
            foreach (PointF pt in p)
            {
                x += pt.X;
                y += pt.Y;
            }
            return new PointF(x / p.Length, y / p.Length);
        }

        internal static void Add(this GraphicsPath graphicsPath, PointF start, float radiusX, float radiusY, float angle, int size, int sweep, PointF end)
        {
            if (start == end)
            {
                return;
            }

            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);

            if (radiusX == 0.0f && radiusY == 0.0f)
            {
                graphicsPath.AddLine(start, end);
                return;
            }

            double sinPhi = Math.Sin(angle * RadPerDeg);
            double cosPhi = Math.Cos(angle * RadPerDeg);

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
                root = ((size == 1 && sweep == 1) || (size == 0 && sweep == 0) ? -1.0 : 1.0) * Math.Sqrt(numerator / (radiusX * radiusX * y1dash * y1dash + radiusY * radiusY * x1dash * x1dash));
            }

            double cxdash = root * rx * y1dash / ry;
            double cydash = -root * ry * x1dash / rx;

            double cx = cosPhi * cxdash - sinPhi * cydash + (start.X + end.X) / 2.0;
            double cy = sinPhi * cxdash + cosPhi * cydash + (start.Y + end.Y) / 2.0;

            double theta1 = VectorAngle(1.0, 0.0, (x1dash - cxdash) / rx, (y1dash - cydash) / ry);
            double dtheta = VectorAngle((x1dash - cxdash) / rx, (y1dash - cydash) / ry, (-x1dash - cxdash) / rx, (-y1dash - cydash) / ry);

            if (sweep == 0 && dtheta > 0)
            {
                dtheta -= TwoPI;
            }
            else if (sweep == 1 && dtheta < 0)
            {
                dtheta += TwoPI;
            }

            int segments = (int)Math.Ceiling(Math.Abs(dtheta / (Math.PI / 2.0)));
            double delta = dtheta / segments;
            double t = 8.0 / 3.0 * Math.Sin(delta / 4.0) * Math.Sin(delta / 4.0) / Math.Sin(delta / 2.0);

            double startX = start.X;
            double startY = start.Y;

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

                graphicsPath.AddBezier((float)startX, (float)startY, (float)(startX + dx1), (float)(startY + dy1),
                    (float)(endpointX + dxe), (float)(endpointY + dye), (float)endpointX, (float)endpointY);

                theta1 = theta2;
                startX = (float)endpointX;
                startY = (float)endpointY;
            }
        }

#if !PDNPLUGIN
        internal static int Clamp(this int value, int min, int max)
        {
            if (min > max)
            {
                throw new Exception(nameof(min) + "is larger than" + nameof(max));
            }

            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }
            else
            {
                return value;
            }
        }
#endif

        private const double RadPerDeg = Math.PI / 180.0;
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
    }
}
