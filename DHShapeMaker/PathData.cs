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
            get => PathType == PathType.Ellipse ? arcOptions : ArcOptions.None;
            set => arcOptions = value;
        }
        public string Alias { get; set; }

        public PathData(PathType pathType, IEnumerable<PointF> points, CloseType closeType, ArcOptions arcOptions, string alias)
        {
            this.PathType = pathType;
            this.Points = points.ToArray();
            this.CloseType = closeType;
            this.ArcOptions = arcOptions;
            this.Alias = alias;
        }

        public PathData()
        {
        }
    }

    public enum PathType
    {
        Straight,
        Ellipse,
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
