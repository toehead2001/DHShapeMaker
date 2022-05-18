using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace ShapeMaker
{
    internal static class StreamGeometryUtil
    {
        internal static string TryExtractStreamGeometry(string xamlCode)
        {
            if (string.IsNullOrWhiteSpace(xamlCode))
            {
                return null;
            }

            XDocument xDoc = TryParseXDocument(xamlCode);
            if (xDoc == null)
            {
                return null;
            }

            XElement docElement = xDoc.Root;

            if (docElement.Name == XName.Get("svg", "http://www.w3.org/2000/svg"))
            {
                if (!docElement.HasElements)
                {
                    return null;
                }

                IEnumerable<XElement> pathElements = docElement.Descendants(XName.Get("path", "http://www.w3.org/2000/svg"));
                if (!pathElements.Any())
                {
                    return null;
                }

                List<string> dataStrings = new List<string>();

                foreach (XElement pathElement in pathElements)
                {
                    if (!pathElement.HasAttributes)
                    {
                        continue;
                    }

                    XAttribute dAttribute = pathElement.Attribute(XName.Get("d", string.Empty));
                    if (dAttribute == null)
                    {
                        continue;
                    }

                    string geometryCode = dAttribute.Value;
                    if (string.IsNullOrWhiteSpace(geometryCode))
                    {
                        continue;
                    }

                    string streamGeometry = TryGetValidatedStreamGeometry(geometryCode);
                    if (streamGeometry == null)
                    {
                        continue;
                    }

                    dataStrings.Add(streamGeometry);
                }

                return dataStrings.Any()
                    ? string.Join(" ", dataStrings)
                    : null;
            }

            if (docElement.Name == XName.Get("vector", string.Empty))
            {
                if (!docElement.HasElements)
                {
                    return null;
                }

                IEnumerable<XElement> pathElements = docElement.Descendants(XName.Get("path", string.Empty));
                if (!pathElements.Any())
                {
                    return null;
                }

                List<string> dataStrings = new List<string>();

                foreach (XElement pathElement in pathElements)
                {
                    if (!pathElement.HasAttributes)
                    {
                        continue;
                    }

                    XAttribute pathDataAttribute = pathElement.Attribute(XName.Get("pathData", "http://schemas.android.com/apk/res/android"));
                    if (pathDataAttribute == null)
                    {
                        continue;
                    }

                    string geometryCode = pathDataAttribute.Value;
                    if (string.IsNullOrWhiteSpace(geometryCode))
                    {
                        continue;
                    }

                    string streamGeometry = TryGetValidatedStreamGeometry(geometryCode);
                    if (streamGeometry == null)
                    {
                        continue;
                    }

                    dataStrings.Add(streamGeometry);
                }

                return dataStrings.Any()
                    ? string.Join(" ", dataStrings)
                    : null;
            }

            if (docElement.Name == XName.Get("SimpleGeometryShape", "clr-namespace:PaintDotNet.Shapes;assembly=PaintDotNet.Framework"))
            {
                if (docElement.HasElements)
                {
                    XElement firstElement = docElement.Elements().First();

                    string xElementText = firstElement.ToString();
                    int xmlnsStartIndex = xElementText.IndexOf(" xmlns=");
                    int xmlnsEndIndex = xElementText.IndexOf(">");

                    if (firstElement.IsEmpty)
                    {
                        xmlnsEndIndex--;
                    }

                    if (xmlnsStartIndex < 0 || xmlnsEndIndex < 0 || xmlnsEndIndex < xmlnsStartIndex)
                    {
                        return null;
                    }

                    const string replacementNs = " xmlns =\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

                    string geometryText = xElementText
                        .Remove(xmlnsStartIndex, xmlnsEndIndex - xmlnsStartIndex)
                        .Insert(xmlnsStartIndex, replacementNs);

                    Geometry geometry = TryParseXaml<Geometry>(geometryText);

                    return (geometry != null)
                        ? StreamGeometryFromGeometry(geometry)
                        : null;
                }

                if (docElement.HasAttributes)
                {
                    XAttribute geometryAttribute = docElement.Attribute(XName.Get("Geometry", string.Empty));
                    if (geometryAttribute == null)
                    {
                        return null;
                    }

                    string geometryCode = geometryAttribute.Value;
                    if (string.IsNullOrWhiteSpace(geometryCode))
                    {
                        return null;
                    }

                    return TryGetValidatedStreamGeometry(geometryCode);
                }

                return null;
            }

            const string xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

            if (docElement.Name.NamespaceName == xamlNs)
            {
                if (!docElement.HasElements)
                {
                    return null;
                }

                IEnumerable<XElement> pathElements = docElement.Descendants(XName.Get(nameof(Path), xamlNs));
                IEnumerable<XElement> geoDrawElements = docElement.Descendants(XName.Get(nameof(GeometryDrawing), xamlNs));
                IEnumerable<XElement> pathGeoElements = docElement.Descendants(XName.Get(nameof(PathGeometry), xamlNs));

                if (!pathElements.Any() && !geoDrawElements.Any() && !pathGeoElements.Any())
                {
                    pathElements = docElement.Descendants(XName.Get(nameof(Path), string.Empty));
                    geoDrawElements = docElement.Descendants(XName.Get(nameof(GeometryDrawing), string.Empty));
                    pathGeoElements = docElement.Descendants(XName.Get(nameof(PathGeometry), string.Empty));

                    if (!pathElements.Any() && !geoDrawElements.Any() && !pathGeoElements.Any())
                    {
                        return null;
                    }
                }

                List<string> dataStrings = new List<string>();

                foreach (XElement pathElement in pathElements)
                {
                    if (pathElement.Name.NamespaceName.Length == 0)
                    {
                        pathElement.Name = XName.Get(pathElement.Name.LocalName, xamlNs);
                    }

                    Path path = TryParseXaml<Path>(pathElement.ToString());
                    if (path != null && path.Data != null)
                    {
                        string streamGeometry = StreamGeometryFromGeometry(path.Data);
                        dataStrings.Add(streamGeometry);
                    }
                }

                foreach (XElement geoDrawElement in geoDrawElements)
                {
                    if (geoDrawElement.Name.NamespaceName.Length == 0)
                    {
                        geoDrawElement.Name = XName.Get(geoDrawElement.Name.LocalName, xamlNs);
                    }

                    GeometryDrawing geoDraw = TryParseXaml<GeometryDrawing>(geoDrawElement.ToString());
                    if (geoDraw != null && geoDraw.Geometry != null)
                    {
                        string streamGeometry = StreamGeometryFromGeometry(geoDraw.Geometry);
                        dataStrings.Add(streamGeometry);
                    }
                }

                foreach (XElement pathGeoElement in pathGeoElements)
                {
                    if (pathGeoElement.Name.NamespaceName.Length == 0)
                    {
                        pathGeoElement.Name = XName.Get(pathGeoElement.Name.LocalName, xamlNs);
                    }

                    PathGeometry pathGeometry = TryParseXaml<PathGeometry>(pathGeoElement.ToString());
                    if (pathGeometry != null)
                    {
                        string streamGeometry = StreamGeometryFromGeometry(pathGeometry);
                        dataStrings.Add(streamGeometry);
                    }
                }

                return dataStrings.Any()
                    ? string.Join(" ", dataStrings)
                    : null;
            }

            return null;
        }

        private static XDocument TryParseXDocument(string xml)
        {
            XDocument xmlDoc = null;

            try
            {
                xmlDoc = XDocument.Parse(xml);
            }
            catch
            {
                return null;
            }

            return xmlDoc;
        }

        private static T TryParseXaml<T>(string xamlText)
            where T : class
        {
            T path = null;

            try
            {
                path = (T)XamlReader.Parse(xamlText);
            }
            catch
            {
            }

            return path;
        }

        internal static string TryGetValidatedStreamGeometry(string streamGeometry)
        {
            return TryParseStreamGeometry(streamGeometry)?.ToString(CultureInfo.InvariantCulture);
        }

        internal static StreamGeometry TryParseStreamGeometry(string streamGeometry)
        {
            StreamGeometry geometry = null;

            try
            {
                geometry = (StreamGeometry)Geometry.Parse(streamGeometry);
            }
            catch
            {
            }

            return geometry;
        }

        private static string StreamGeometryFromGeometry(Geometry geometry)
        {
            PathGeometry pathGeometry = PathGeometry.CreateFromGeometry(geometry);
            return pathGeometry.Figures.ToString(CultureInfo.InvariantCulture);
        }

        internal static string GenerateStreamGeometry(IReadOnlyList<PathData> paths, bool solidFill, float width, float height)
        {
            StringBuilder sb = new StringBuilder();
            float oldx = 0, oldy = 0;
            bool previousClosed = false;

            if (solidFill)
            {
                sb.Append("F1 ");
            }

            for (int index = 0; index < paths.Count; index++)
            {
                PathData currentPath = paths[index];
                PathType pathType = currentPath.PathType;
                PointF[] line = currentPath.Points;
                bool islarge = currentPath.ArcOptions.HasFlag(ArcOptions.LargeArc);
                bool revsweep = currentPath.ArcOptions.HasFlag(ArcOptions.PositiveSweep);

                if (line.Length < 2)
                {
                    continue;
                }

                float x, y;

                x = width * line[0].X;
                y = height * line[0].Y;

                if (index == 0 || (x != oldx || y != oldy) || currentPath.CloseType == CloseType.Individual || previousClosed)
                {
                    if (index > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append("M ")
                        .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                        .Append(',')
                        .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);
                }

                switch (pathType)
                {
                    case PathType.Straight:
                        sb.Append(" L ");
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;

                            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                                .Append(',')
                                .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);

                            if (i < line.Length - 1)
                            {
                                sb.Append(',');
                            }
                        }
                        oldx = x; oldy = y;
                        break;
                    case PathType.EllipticalArc:
                        sb.Append(" A ");
                        PointF[] pts = new PointF[line.Length];
                        for (int i = 0; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            pts[i] = new PointF(x, y);
                        }
                        PointF mid = PointFUtil.PointAverage(pts[0], pts[4]);
                        float l = PointFUtil.Pythag(mid, pts[1]);
                        float h = PointFUtil.Pythag(mid, pts[2]);
                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                        float b = (islarge) ? 1 : 0;
                        float s = (revsweep) ? 1 : 0;

                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", l)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", h)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", a)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0}", b)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0}", s)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", pts[4].X)
                            .Append(',')
                            .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", pts[4].Y);

                        oldx = pts[4].X;
                        oldy = pts[4].Y;
                        break;
                    case PathType.Cubic:
                        sb.Append(" C ");
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;

                            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                                .Append(',')
                                .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);

                            if (i < line.Length - 1)
                            {
                                sb.Append(',');
                            }

                            oldx = x; oldy = y;
                        }
                        break;
                    case PathType.Quadratic:
                        sb.Append(" Q ");
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (CanvasUtil.GetNubType(i) != NubType.ControlPoint2)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;

                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                                    .Append(',')
                                    .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);

                                if (i < line.Length - 1)
                                {
                                    sb.Append(',');
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case PathType.SmoothCubic:
                        sb.Append(" S ");
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (CanvasUtil.GetNubType(i) != NubType.ControlPoint1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;

                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                                    .Append(',')
                                    .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);

                                if (i < line.Length - 1)
                                {
                                    sb.Append(',');
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case PathType.SmoothQuadratic:
                        sb.Append(" T ");
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (CanvasUtil.GetNubType(i) != NubType.ControlPoint2 && CanvasUtil.GetNubType(i) != NubType.ControlPoint1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", x)
                                    .Append(',')
                                    .AppendFormat(CultureInfo.InvariantCulture, "{0:0.##}", y);

                                if (i < line.Length - 1)
                                {
                                    sb.Append(',');
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                }

                if (currentPath.CloseType != CloseType.None)
                {
                    sb.Append(" Z");
                    if (currentPath.CloseType == CloseType.Individual)
                    {
                        oldx += 10;
                        oldy += 10;
                    }

                    previousClosed = true;
                }
                else
                {
                    previousClosed = false;
                }
            }

            return sb.ToString();
        }
    }
}
