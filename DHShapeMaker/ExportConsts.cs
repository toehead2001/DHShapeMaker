using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShapeMaker
{
    internal static class ExportConsts
    {
        internal const string SvgFile =
            "<svg height=\"500\" viewBox=\"0 0 500 500\" width=\"500\" xmlns=\"http://www.w3.org/2000/svg\">\r\n" +
            "    <path d=\"{0}\" fill=\"none\" stroke=\"#000\" stroke-width=\"1.0\"/>\r\n" +
            "</svg>";

        internal const string PdnStreamGeometryFile =
            "<ps:SimpleGeometryShape\r\n" +
            "    xmlns=\"clr-namespace:PaintDotNet.UI.Media;assembly=PaintDotNet.Framework\"\r\n" +
            "    xmlns:ps=\"clr-namespace:PaintDotNet.Shapes;assembly=PaintDotNet.Framework\"\r\n" +
            "    DisplayName=\"{0}\"\r\n" +
            "    Geometry=\"{1}\r\n" +
            "\"/>\r\n";

        internal const string PdnPathGeometryFile =
            "<ps:SimpleGeometryShape\r\n" +
            "        xmlns=\"clr-namespace:PaintDotNet.UI.Media;assembly=PaintDotNet.Framework\"\r\n" +
            "        xmlns:ps=\"clr-namespace:PaintDotNet.Shapes;assembly=PaintDotNet.Framework\"\r\n" +
            "        DisplayName=\"{0}\">\r\n" +
            "    <GeometryGroup FillRule=\"{1}\">\r\n" +
            "        <PathGeometry>\r\n" +
            "{2}\r\n" +
            "        </PathGeometry>\r\n" +
            "    </GeometryGroup>\r\n" +
            "</ps:SimpleGeometryShape>\r\n";

        internal const string PGBezier = "<BezierSegment Point1=\"{0}\" Point2=\"{1}\" Point3=\"{2}\" />";

        internal const string PGEllipse = "<ArcSegment Size=\"{0}\" RotationAngle=\"{1}\" IsLargeArc=\"{2}\" SweepDirection=\"{3}\" Point=\"{4}\" />";

        internal const string PGLine = "<LineSegment Point=\"{0}\" />";

        internal const string PGMove = "<PathFigure IsClosed=\"{0}\" IsFilled=\"True\" StartPoint=\"{1}\">";

        internal const string PGQuad = "<QuadraticBezierSegment Point1=\"{0}\" Point2=\"{1}\" />";
    }
}
