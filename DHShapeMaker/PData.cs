using System;
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

        public PData()
        {
        }

        internal PathData ToPathData()
        {
            PathType pathType = (PathType)this.LineType;
            CloseType closeType = this.ClosedType ? CloseType.Individual : this.LoopBack ? CloseType.Contiguous : CloseType.None;
            ArcOptions arcOptions = ArcOptions.None;

            if (pathType == PathType.EllipticalArc)
            {
                if (this.IsLarge)
                {
                    arcOptions |= ArcOptions.LargeArc;
                }
                if (this.RevSweep)
                {
                    arcOptions |= ArcOptions.PositiveSweep;
                }
            }

            return new PathData(pathType, this.Lines, closeType, arcOptions, this.Alias);
        }
    }
}
