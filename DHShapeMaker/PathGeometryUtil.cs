using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShapeMaker
{
    internal static class PathGeometryUtil
    {
        internal static string GeneratePathGeometry(IReadOnlyList<PathData> paths, float width, float height)
        {
            string strPath = string.Empty;
            float oldx = 0, oldy = 0;
            string[] repstr = { "~1", "~2", "~3" };
            string tmpstr = string.Empty;

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

                if (index == 0)
                {
                    strPath += $"\t\t\t\t{ExportConsts.PGMove}\r\n";
                    strPath = strPath.Replace("~1", $"{x:0.##},{y:0.##}");
                }
                else if (currentPath.CloseType == CloseType.Individual || (x != oldx || y != oldy))//mod 091515
                {
                    strPath = strPath.Replace("~0", "False");
                    strPath += "\t\t\t\t</PathFigure>\r\n";
                    strPath += $"\t\t\t\t{ExportConsts.PGMove}\r\n";
                    strPath = strPath.Replace("~1", $"{x:0.##},{y:0.##}");
                }

                switch (pathType)
                {
                    case PathType.Straight:
                        tmpstr = string.Empty;
                        for (int i = 1; i < points.Length; i++)
                        {
                            strPath += $"\t\t\t\t\t{ExportConsts.PGLine}\r\n";
                            x = width * points[i].X;
                            y = height * points[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";

                            strPath = strPath.Replace("~1", tmpstr);
                        }

                        oldx = x; oldy = y;
                        break;
                    case PathType.Ellipse:
                        strPath += $"\t\t\t\t\t{ExportConsts.PGEllipse}\r\n";
                        PointF[] pts = new PointF[points.Length];
                        for (int i = 0; i < points.Length; i++)
                        {
                            x = width * points[i].X;
                            y = height * points[i].Y;
                            pts[i] = new PointF(x, y);
                        }
                        PointF mid = PointFUtil.PointAverage(pts[0], pts[4]);
                        float l = PointFUtil.Pythag(mid, pts[1]);
                        float h = PointFUtil.Pythag(mid, pts[2]);
                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);

                        tmpstr = $"{l:0.##}";
                        tmpstr += ",";
                        tmpstr += $"{h:0.##}";
                        strPath = strPath.Replace("~1", tmpstr);
                        strPath = strPath.Replace("~2", $"{a:0.##}");
                        strPath = strPath.Replace("~3", (islarge) ? "True" : "False");
                        strPath = strPath.Replace("~4", (revsweep) ? "Clockwise" : "CounterClockwise");

                        tmpstr = $"{pts[4].X:0.##},{pts[4].Y:0.##}";
                        strPath = strPath.Replace("~5", tmpstr);
                        oldx = pts[4].X; oldy = pts[4].Y;
                        break;
                    case PathType.SmoothCubic:
                    case PathType.Cubic:

                        for (int i = 1; i < points.Length - 1; i += 3)
                        {
                            strPath += $"\t\t\t\t\t{ExportConsts.PGBezier}\r\n";
                            for (int j = 0; j < 3; j++)
                            {
                                x = width * points[j + i].X;
                                y = height * points[j + i].Y;
                                tmpstr = $"{x:0.##},{y:0.##}";
                                strPath = strPath.Replace(repstr[j], tmpstr);
                            }
                        }

                        oldx = x; oldy = y;
                        break;
                    case PathType.SmoothQuadratic:
                    case PathType.Quadratic:

                        for (int i = 1; i < points.Length - 1; i += 3)
                        {
                            strPath += $"\t\t\t\t\t{ExportConsts.PGQuad}\r\n";

                            x = width * points[i].X;
                            y = height * points[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~1", tmpstr);
                            x = width * points[i + 2].X;
                            y = height * points[i + 2].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~2", tmpstr);
                        }

                        oldx = x; oldy = y;
                        break;
                }

                if (currentPath.CloseType != CloseType.None)
                {
                    strPath = strPath.Replace("~0", "True");
                    oldx += 10;
                    oldy += 10;
                }
            }

            strPath += "\t\t\t\t</PathFigure>\r\n";
            strPath = strPath.Replace("~0", "False");
            strPath += "\r\n";

            return strPath;
        }
    }
}
