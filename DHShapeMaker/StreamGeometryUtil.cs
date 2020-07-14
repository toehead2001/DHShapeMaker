using System.Collections.Generic;
using System.Linq;
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

            if (docElement.Name.LocalName == "SimpleGeometryShape")
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

                    const string xamlNs = " xmlns =\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

                    string geometryText = xElementText
                        .Remove(xmlnsStartIndex, xmlnsEndIndex - xmlnsStartIndex)
                        .Insert(xmlnsStartIndex, xamlNs);

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

                    StreamGeometry streamGeometry = TryParseStreamGeometry(geometryCode);

                    return streamGeometry?.ToString();
                }

                return null;
            }

            if (docElement.HasElements)
            {
                const string xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

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
            return pathGeometry.Figures.ToString();
        }
    }
}
