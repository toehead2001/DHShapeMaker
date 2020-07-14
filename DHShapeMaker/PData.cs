﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace ShapeMaker
{
    [Serializable]
    public class PData
    {
        public PointF[] Lines { get; set; }
        public int LineType { get; set; }
        public bool ClosedType { get; set; }
        public bool IsLarge { get; set; }
        public bool RevSweep { get; set; }
        public string Alias { get; set; }
        public string Meta { get; set; }
        public bool SolidFill { get; set; }
        public bool LoopBack { get; set; }

        public PData(PointF[] points, bool closed, int lineType, bool isLarge, bool revSweep, string alias, bool loopBack)
        {
            this.Lines = points;
            this.LineType = lineType;
            this.ClosedType = closed;
            this.IsLarge = isLarge;
            this.RevSweep = revSweep;
            this.Alias = alias;
            this.LoopBack = loopBack;
        }

        public PData()
        {
        }

        internal static IReadOnlyCollection<PData> FromStreamGeometry(string streamGeometry)
        {
            return PDataFactory.StreamGeometryToPData(streamGeometry);
        }

        private static class PDataFactory
        {
            internal static IReadOnlyCollection<PData> StreamGeometryToPData(string streamGeometry)
            {
                streamGeometry = streamGeometry.Trim();
                if (streamGeometry.Length == 0)
                {
                    return Array.Empty<PData>();
                }

                List<PointF> pts = new List<PointF>();
                PathType pathType = PathType.None;
                bool closedIndividual = false;
                bool closedContiguous = false;
                bool isLarge = true;
                bool revSweep = false;

                bool solidFill = false;

                PointF LastPos = new PointF();
                PointF HomePos = new PointF();

                StreamGeometryCommand currentCommand = StreamGeometryCommand.None;
                int drawCommandsSinceMove = 0;
                bool errorFlagX = false;
                bool errorFlagY = false;
                float x = 0, y = 0;

                List<PData> paths = new List<PData>();

                string[] str = scrubNums(streamGeometry).Split(',');
                for (int i = 0; i < str.Length; i++)
                {
                    errorFlagX = true;
                    errorFlagY = true;

                    StreamGeometryCommand command = GetStreamGeometryCommand(str[i]);

                    if (command != StreamGeometryCommand.None)
                    {
                        currentCommand = command;

                        if (currentCommand == StreamGeometryCommand.Move)
                        {
                            drawCommandsSinceMove = 0;
                        }
                        else if (currentCommand != StreamGeometryCommand.Close && currentCommand != StreamGeometryCommand.FillRule)
                        {
                            drawCommandsSinceMove++;
                        }

                        closedContiguous = currentCommand == StreamGeometryCommand.Close && drawCommandsSinceMove > 1;
                        closedIndividual = !closedContiguous && currentCommand == StreamGeometryCommand.Close;

                        if (pts.Count > 1)
                        {
                            PData path = new PData(pts.ToArray(), closedIndividual, (int)pathType, isLarge, revSweep, string.Empty, closedContiguous);
                            paths.Add(path);
                        }

                        pts.Clear();
                        pts.Add(LastPos);

                        PathType type = CommandToPathType(currentCommand);
                        if (type != PathType.None)
                        {
                            pathType = type;
                        }

                        continue;
                    }

                    int len = 0;

                    switch (currentCommand)
                    {
                        case StreamGeometryCommand.Close:
                            pts.Add(HomePos);
                            break;
                        case StreamGeometryCommand.FillRule:
                            errorFlagX = int.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out int fillRule);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            solidFill = Convert.ToBoolean(fillRule);
                            break;
                        case StreamGeometryCommand.Move:
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            HomePos = LastPos;
                            break;

                        case StreamGeometryCommand.CubicBezierCurve:
                        case StreamGeometryCommand.Line:
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            pts.Add(LastPos);
                            break;
                        case StreamGeometryCommand.SmoothCubicBezierCurve:
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            len = pts.Count;

                            if (len > 1)
                            {
                                NubType nubType = CanvasUtil.GetNubType(len);

                                if (nubType == NubType.ControlPoint1)
                                {
                                    pts.Add(PointFUtil.ReverseAverage(pts[len - 2], pts[len - 1]));
                                    pts.Add(LastPos);
                                }
                                else if (nubType == NubType.EndPoint)
                                {
                                    pts.Add(LastPos);
                                }
                            }
                            else
                            {
                                pts.Add(pts[0]);
                                pts.Add(LastPos);
                            }

                            break;
                        case StreamGeometryCommand.SmoothQuadraticBezierCurve:
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            len = pts.Count;

                            if (len > 1)
                            {
                                pts.Add(PointFUtil.ReverseAverage(pts[len - 2], pts[len - 1]));
                                pts.Add(pts[len]);
                            }
                            else
                            {
                                pts[1] = pts[0];
                                pts[2] = pts[0];
                            }
                            pts.Add(LastPos);
                            break;
                        case StreamGeometryCommand.QuadraticBezierCurve:
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            pts.Add(LastPos);

                            if (CanvasUtil.GetNubType(pts.Count - 1) == NubType.ControlPoint1)
                            {
                                pts.Add(LastPos);
                            }

                            break;
                        case StreamGeometryCommand.HorizontalLine:
                            y = LastPos.Y;
                            errorFlagX = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            x = x / 500;
                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            pts.Add(LastPos);
                            break;
                        case StreamGeometryCommand.VerticalLine:
                            x = LastPos.X;
                            errorFlagY = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            y = y / 500;
                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);
                            pts.Add(LastPos);
                            break;
                        case StreamGeometryCommand.EllipticalArc:
                            errorFlagX = float.TryParse(str[i + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            errorFlagY = float.TryParse(str[i + 6], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            LastPos = CanvasUtil.PointToCanvasCoord1x(x, y);

                            float dist;
                            errorFlagX = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //W
                            if (!errorFlagX)
                            {
                                break;
                            }

                            PointF From = CanvasUtil.CanvasCoordToPoint1x(pts[0]);
                            PointF To = new PointF(x, y);

                            PointF mid = PointFUtil.PointAverage(From, To);
                            PointF mid2 = PointFUtil.ThirdPoint(From, mid, true, 1f);
                            float far = PointFUtil.Pythag(From, mid);
                            float atan = (float)Math.Atan2(mid2.Y - mid.Y, mid2.X - mid.X);

                            pts.Add(pointOrbit(mid, atan - (float)Math.PI / 4f, dist));

                            errorFlagX = float.TryParse(str[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //H
                            if (!errorFlagX)
                            {
                                break;
                            }

                            pts.Add(pointOrbit(mid, atan + (float)Math.PI / 4f, dist));
                            errorFlagX = float.TryParse(str[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                            float rot = dist * (float)Math.PI / 180f; //ROT
                            pts.Add(pointOrbit(mid, rot, far));

                            pts.Add(LastPos); //ENDPOINT

                            errorFlagX = float.TryParse(str[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                            if (!errorFlagX)
                            {
                                break;
                            }

                            float dist2;
                            errorFlagY = float.TryParse(str[i + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out dist2);
                            if (!errorFlagY)
                            {
                                break;
                            }

                            isLarge = Convert.ToBoolean(dist);
                            revSweep = Convert.ToBoolean(dist2);

                            i += 6;
                            //currentCommand = StreamGeometryCommand.Close;
                            break;
                    }

                    if (!errorFlagX || !errorFlagY)
                    {
                        break;
                    }
                }

                if (!errorFlagX || !errorFlagY || pathType == PathType.None)
                {
                    return Array.Empty<PData>();
                }

                if (pts.Count > 1)
                {
                    PData path = new PData(pts.ToArray(), closedIndividual, (int)pathType, isLarge, revSweep, string.Empty, closedContiguous);
                    path.SolidFill = solidFill;
                    paths.Add(path);
                }

                return paths;
            }

            private static StreamGeometryCommand GetStreamGeometryCommand(string commandChar)
            {
                // https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/path-markup-syntax
                const string commands = "fmlacsqthvz";

                return (StreamGeometryCommand)commands.IndexOf(commandChar);
            }

            private static PathType CommandToPathType(StreamGeometryCommand streamGeometryCommand)
            {
                switch (streamGeometryCommand)
                {
                    case StreamGeometryCommand.Line:
                    case StreamGeometryCommand.HorizontalLine:
                    case StreamGeometryCommand.VerticalLine:
                        return PathType.Straight;
                    case StreamGeometryCommand.EllipticalArc:
                        return PathType.Ellipse;
                    case StreamGeometryCommand.CubicBezierCurve:
                        return PathType.Cubic;
                    case StreamGeometryCommand.SmoothCubicBezierCurve:
                        return PathType.SmoothCubic;
                    case StreamGeometryCommand.QuadraticBezierCurve:
                        return PathType.Quadratic;
                    case StreamGeometryCommand.SmoothQuadraticBezierCurve:
                        return PathType.SmoothQuadratic;
                    case StreamGeometryCommand.FillRule:
                    case StreamGeometryCommand.Move:
                    case StreamGeometryCommand.Close:
                    case StreamGeometryCommand.None:
                    default:
                        return PathType.None;
                }
            }

            private enum StreamGeometryCommand
            {
                FillRule,
                Move,
                Line,
                EllipticalArc,
                CubicBezierCurve,
                SmoothCubicBezierCurve,
                QuadraticBezierCurve,
                SmoothQuadraticBezierCurve,
                HorizontalLine,
                VerticalLine,
                Close,
                None = -1
            }

            private static PointF pointOrbit(PointF center, float rotation, float distance)
            {
                float x = (float)Math.Cos(rotation) * distance;
                float y = (float)Math.Sin(rotation) * distance;
                return CanvasUtil.PointToCanvasCoord1x(center.X + x, center.Y + y);
            }

            private static string scrubNums(string strPath)
            {
                const string commands = "fmlacsqthvz";
                const string numbers = "e.-0123456789";
                string TMP = string.Empty;
                bool alpha = false;
                bool blank = false;

                foreach (char mychar in strPath.ToLowerInvariant().Replace(',', ' '))
                {
                    bool isNumber = numbers.Contains(mychar);
                    bool isCommand = commands.Contains(mychar);

                    if (TMP.Length == 0)
                    {
                        TMP += mychar;
                        alpha = true;
                        blank = false;
                    }
                    else if (mychar.Equals(' '))
                    {
                        alpha = true;
                        blank = true;
                    }
                    else if (isCommand && (!alpha || blank))
                    {
                        TMP += "," + mychar;
                        alpha = true;
                        blank = false;
                    }
                    else if (isCommand)
                    {
                        TMP += mychar;
                        alpha = true;
                        blank = false;
                    }
                    else if (isNumber && (alpha || blank))
                    {
                        TMP += "," + mychar;
                        alpha = false;
                        blank = false;
                    }
                    else if (isNumber)
                    {
                        TMP += mychar;
                        alpha = false;
                        blank = false;
                    }
                }
                return TMP;
            }
        }
    }
}