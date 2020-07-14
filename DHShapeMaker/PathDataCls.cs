using System;
using System.Collections.Generic;
using System.Drawing;

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
    }
}
