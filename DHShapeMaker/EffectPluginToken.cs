using System.Collections;
using PaintDotNet.Effects;
using System.Drawing.Drawing2D;

namespace ShapeMaker
{
    internal class EffectPluginConfigToken : EffectConfigToken
    {
        internal GraphicsPath[] GP { get; set; }
        internal ArrayList PathData { get; set; }
        internal bool Draw { get; set; }
        internal decimal Scale { get; set; }
        internal bool SnapTo { get; set; }
        internal string ShapeName { get; set; }
        internal bool SolidFill { get; set; }

        internal EffectPluginConfigToken(GraphicsPath[] gp, ArrayList pathdata, bool draw, decimal scale, bool snap, string shapename, bool solidfill)
        {
            GP = gp;
            PathData = pathdata;
            Draw = draw;
            Scale = scale;
            SnapTo = snap;
            ShapeName = shapename;
            SolidFill = solidfill;
        }

        private EffectPluginConfigToken(EffectPluginConfigToken copyMe) : base(copyMe)
        {
            GP = copyMe.GP;
            PathData = copyMe.PathData;
            Draw = copyMe.Draw;
            Scale = copyMe.Scale;
            SnapTo = copyMe.SnapTo;
            ShapeName = copyMe.ShapeName;
            SolidFill = copyMe.SolidFill;
        }

        public override object Clone()
        {
            return new EffectPluginConfigToken(this);
        }
    }
}