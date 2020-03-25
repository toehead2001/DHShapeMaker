using System.Collections.Generic;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;

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

            XmlDocument xDoc = TryLoadXml(xamlCode);
            if (xDoc == null)
            {
                return null;
            }

            XmlElement docElement = xDoc.DocumentElement;

            if (docElement.Name == "ps:SimpleGeometryShape" && !docElement.HasChildNodes && docElement.HasAttributes && docElement.HasAttribute("Geometry"))
            {
                string geometryCode = docElement.Attributes.GetNamedItem("Geometry").InnerText;
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
            else
            {
                XmlNodeList pathNodes = docElement.GetElementsByTagName("Path");
                if (pathNodes.Count == 0)
                {
                    return null;
                }

                List<string> dataStrings = new List<string>(pathNodes.Count);
                foreach (XmlNode pathNode in pathNodes)
                {
                    Path path = TryParsePath(pathNode.OuterXml);
                    if (path != null && path.Data is StreamGeometry streamGeometry)
                    {
                        dataStrings.Add(streamGeometry.ToString());
                    }
                }

                return string.Join(" ", dataStrings);
            }

            return null;
        }

        private static XmlDocument TryLoadXml(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(xml);
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
