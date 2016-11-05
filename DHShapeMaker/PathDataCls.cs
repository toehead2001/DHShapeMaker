using System;
using System.Drawing;

namespace ShapeMaker
{
    [Serializable]
    public class PData
    {
        public PointF[] Points
        {
            get;
            set;
        }
        public int PathType
        {
            get;
            set;
        }
        public bool ClosedPath
        {
            get;
            set;
        }
        public bool IsLarge
        {
            get;
            set;
        }
        public bool RevSweep
        {
            get;
            set;
        }
        public string Alias
        {
            get;
            set;
        }
        public string Meta
        {
            get;
            set;
        }
        public bool SolidFill
        {
            get;
            set;
        }
        public bool LoopBack
        {
            get;
            set;
        }
        public PData(PointF[] points, bool closed, int lineType, bool isLarge, bool revSweep, string alias, bool loopBack)
        {
            Points = points;
            PathType = lineType;
            ClosedPath = closed;
            IsLarge = isLarge;
            RevSweep = revSweep;
            Alias = alias;
            LoopBack = loopBack;
        }
        public PData()
        {

        }
    }
}
