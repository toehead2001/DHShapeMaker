using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShapeMaker
{
    internal static class CanvasUtil
    {
        internal static PointF PointToCanvasCoord1x(float x, float y)
        {
            return PointToCanvasCoord(x, y, 500, 500);
        }

        internal static PointF PointToCanvasCoord(float x, float y, int width, int height)
        {
            return new PointF(x / width, y / height);
        }

        internal static PointF CanvasCoordToPoint1x(PointF coord)
        {
            return CanvasCoordToPoint(coord.X, coord.Y, 500, 500);
        }

        internal static PointF CanvasCoordToPoint(float x, float y, int width, int height)
        {
            return new PointF(x * width, y * height);
        }

        internal static bool IsControlNub(int nubIndex, PathType pathType)
        {
            switch (pathType)
            {
                case PathType.EllipticalArc:
                    if (nubIndex % 4 != 0)
                    {
                        return true;
                    }
                    break;
                case PathType.Cubic:
                case PathType.SmoothCubic:
                case PathType.Quadratic:
                    if (nubIndex % 3 != 0)
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        internal static NubType GetNubType(int nubIndex)
        {
            if (nubIndex == 0)
            {
                return NubType.StartPoint;
            }

            int nubType = ((nubIndex - 1) % 3) + 1;
            return (NubType)nubType;
        }

        internal static void AutoScaleAndCenter(IReadOnlyCollection<PathData> paths)
        {
            if (paths.Count == 0)
            {
                return;
            }

            RectangleF bounds = paths.Bounds();
            if (bounds.IsEmpty)
            {
                return;
            }

            PointF center = new PointF(0.5f, 0.5f);
            PointF origin = bounds.Location;
            PointF destination = new PointF(center.X - bounds.Width / 2f, center.Y - bounds.Height / 2f);
            float scale = 0.98f / Math.Max(bounds.Width, bounds.Height);
            foreach (PathData path in paths)
            {
                PointF[] pathPoints = path.Points;
                for (int j = 0; j < pathPoints.Length; j++)
                {
                    pathPoints[j] = PointFUtil.MovePoint(origin, destination, pathPoints[j]);
                }

                pathPoints.Scale(pathPoints, scale, center);
            }
        }
    }
}
