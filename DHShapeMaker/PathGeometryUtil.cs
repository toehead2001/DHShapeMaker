using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace ShapeMaker
{
    internal static class PathGeometryUtil
    {
        internal static string GeneratePathGeometry(IReadOnlyList<PathData> paths, IReadOnlyList<EffectPluginConfigDialog.LinkFlags> linkFlags, float width, float height)
        {
            StringBuilder sb = new StringBuilder();

            for (int index = 0; index < paths.Count; index++)
            {
                PathData currentPath = paths[index];
                PathType pathType = currentPath.PathType;
                PointF[] points = currentPath.Points;
                bool islarge = currentPath.ArcOptions.HasFlag(ArcOptions.LargeArc);
                bool revsweep = currentPath.ArcOptions.HasFlag(ArcOptions.PositiveSweep);

                if (points.Length < 2)
                {
                    continue;
                }

                float x, y;

                x = width * points[0].X;
                y = height * points[0].Y;

                EffectPluginConfigDialog.LinkFlags pathLinks = linkFlags[index];

                if (index == 0)
                {
                    string isClosed = pathLinks.HasFlag(EffectPluginConfigDialog.LinkFlags.Closed) ? "True" : "False";
                    string movePoint = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);

                    sb.Append("\t\t\t")
                        .AppendFormat(ExportConsts.PGMove, isClosed, movePoint)
                        .AppendLine();
                }
                else if (!pathLinks.HasFlag(EffectPluginConfigDialog.LinkFlags.Up))
                {
                    string isClosed = pathLinks.HasFlag(EffectPluginConfigDialog.LinkFlags.Closed) ? "True" : "False";
                    string movePoint = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);

                    sb.Append("\t\t\t</PathFigure>\r\n\t\t\t")
                        .AppendFormat(ExportConsts.PGMove, isClosed, movePoint)
                        .AppendLine();
                }

                switch (pathType)
                {
                    case PathType.Straight:
                        for (int i = 1; i < points.Length; i++)
                        {
                            x = width * points[i].X;
                            y = height * points[i].Y;

                            string straightPoint = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);

                            sb.Append("\t\t\t\t")
                                .AppendFormat(ExportConsts.PGLine, straightPoint)
                                .AppendLine();
                        }

                        break;
                    case PathType.EllipticalArc:
                        PointF[] pts = new PointF[points.Length];
                        for (int i = 0; i < points.Length; i++)
                        {
                            x = width * points[i].X;
                            y = height * points[i].Y;
                            pts[i] = new PointF(x, y);
                        }

                        PointF mid = PointFUtil.PointAverage(pts[0], pts[4]);
                        float l = PointFUtil.Hypot(mid, pts[1]);
                        float h = PointFUtil.Hypot(mid, pts[2]);
                        float a = (float)double.RadiansToDegrees(double.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X));

                        string arcSize = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", l, h);
                        string arcAngle = string.Format(CultureInfo.InvariantCulture, "{0:0.##}", a);
                        string arcPoint = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", pts[4].X, pts[4].Y);

                        sb.Append("\t\t\t\t")
                            .AppendFormat(
                                ExportConsts.PGEllipse,
                                arcSize,
                                arcAngle,
                                (islarge) ? "True" : "False",
                                (revsweep) ? "Clockwise" : "CounterClockwise",
                                arcPoint)
                            .AppendLine();

                        break;
                    case PathType.SmoothCubic:
                    case PathType.Cubic:
                        for (int i = 1; i < points.Length - 1; i += 3)
                        {
                            string[] cubicPoints = new string[3];

                            for (int j = 0; j < 3; j++)
                            {
                                x = width * points[j + i].X;
                                y = height * points[j + i].Y;
                                cubicPoints[j] = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);
                            }

                            sb.Append("\t\t\t\t")
                                .AppendFormat(ExportConsts.PGBezier, cubicPoints)
                                .AppendLine();
                        }

                        break;
                    case PathType.SmoothQuadratic:
                    case PathType.Quadratic:
                        for (int i = 1; i < points.Length - 1; i += 3)
                        {
                            x = width * points[i].X;
                            y = height * points[i].Y;
                            string p1 = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);
                            x = width * points[i + 2].X;
                            y = height * points[i + 2].Y;
                            string p2 = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);

                            sb.Append("\t\t\t\t")
                                .AppendFormat(ExportConsts.PGQuad, p1, p2)
                                .AppendLine();
                        }

                        break;
                }
            }

            sb.Append("\t\t\t</PathFigure>");

            return sb.ToString();
        }
    }
}
