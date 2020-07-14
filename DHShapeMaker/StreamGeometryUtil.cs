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

            if (docElement.Name.LocalName == "SimpleGeometryShape" && !docElement.HasElements && docElement.HasAttributes)
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
                if (streamGeometry != null)
                {
                    return streamGeometry.ToString();
                }
            }
            else if (docElement.HasElements)
            {
                IEnumerable<XElement> pathElements = docElement.Descendants(XName.Get("Path", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"));
                if (!pathElements.Any())
                {
                    return null;
                }

                List<string> dataStrings = new List<string>();
                foreach (XElement pathElement in pathElements)
                {
                    Path path = TryParsePath(pathElement.ToString());
                    if (path != null && path.Data is StreamGeometry streamGeometry)
                    {
                        dataStrings.Add(streamGeometry.ToString());
                    }
                }

                return string.Join(" ", dataStrings);
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

        private static Path TryParsePath(string shape)
        {
            Path path = null;

            try
            {
                path = (Path)XamlReader.Parse(shape);
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
    }
}
