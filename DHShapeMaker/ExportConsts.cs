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
            "    DisplayName=\"~1\"\r\n" +
            "    Geometry=\"~2\r\n" +
            "\"/>\r\n";

        internal const string PdnPathGeometryFile =
            "<ps:SimpleGeometryShape\r\n" +
            "        xmlns=\"clr-namespace:PaintDotNet.UI.Media;assembly=PaintDotNet.Framework\"\r\n" +
            "        xmlns:ps=\"clr-namespace:PaintDotNet.Shapes;assembly=PaintDotNet.Framework\"\r\n" +
            "        DisplayName=\"~1\">\r\n" +
            "    <GeometryGroup FillRule=\"~3\">\r\n" +
            "        <PathGeometry>\r\n" +
            "~2\r\n" +
            "        </PathGeometry>\r\n" +
            "    </GeometryGroup>\r\n" +
            "</ps:SimpleGeometryShape>\r\n";

        internal const string PGBezier = "<BezierSegment Point1=\"~1\" Point2=\"~2\" Point3=\"~3\" />";

        internal const string PGEllipse = "<ArcSegment Size=\"~1\" RotationAngle=\"~2\" IsLargeArc=\"~3\" SweepDirection=\"~4\" Point=\"~5\" />";

        internal const string PGLine = "<LineSegment Point=\"~1\" />";

        internal const string PGMove = "<PathFigure IsClosed=\"~0\" IsFilled=\"True\" StartPoint=\"~1\">";

        internal const string PGQuad = "<QuadraticBezierSegment Point1=\"~1\" Point2=\"~2\" />";
    }
}
