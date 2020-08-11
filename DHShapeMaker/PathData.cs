using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShapeMaker
{
    [Serializable]
    public class PathData
    {
        private ArcOptions arcOptions;

        public PathType PathType { get; set; }
        public PointF[] Points { get; set; }
        public CloseType CloseType { get; set; }
        public ArcOptions ArcOptions
        {
            get => PathType == PathType.EllipticalArc ? arcOptions : ArcOptions.None;
            set => arcOptions = value;
        }
        public string Alias { get; set; }

        internal PathData(PathType pathType, IEnumerable<PointF> points, CloseType closeType, ArcOptions arcOptions, string alias)
        {
            this.PathType = pathType;
            this.Points = points.ToArray();
            this.CloseType = closeType;
            this.ArcOptions = arcOptions;
            this.Alias = alias;
        }

        internal PathData(PathType pathType, IEnumerable<PointF> points, CloseType closeType, ArcOptions arcOptions)
            : this(pathType, points, closeType, arcOptions, string.Empty)
        {
        }

        public PathData()
        {
        }
    }

    public enum PathType
    {
        Straight,
        EllipticalArc,
        Cubic,
        SmoothCubic,
        Quadratic,
        SmoothQuadratic,
        None = -1
    }

    public enum CloseType
    {
        None,
        Individual,
        Contiguous
    }

    [Flags]
    public enum ArcOptions
    {
        None,
        LargeArc,
        PositiveSweep
    }
}
