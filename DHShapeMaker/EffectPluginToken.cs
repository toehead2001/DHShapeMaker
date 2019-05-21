using PaintDotNet.Effects;
using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace ShapeMaker
{
    internal class EffectPluginConfigToken : EffectConfigToken
    {
        internal GraphicsPath[] GP { get; set; }
        internal List<PData> PathData { get; set; }
        internal bool Draw { get; set; }
        internal decimal Scale { get; set; }
        internal bool SnapTo { get; set; }
        internal string ShapeName { get; set; }
        internal bool SolidFill { get; set; }

        internal EffectPluginConfigToken(GraphicsPath[] gp, List<PData> pathdata, bool draw, decimal scale, bool snap, string shapename, bool solidfill)
        {
            this.GP = gp;
            this.PathData = pathdata;
            this.Draw = draw;
            this.Scale = scale;
            this.SnapTo = snap;
            this.ShapeName = shapename;
            this.SolidFill = solidfill;
        }

        private EffectPluginConfigToken(EffectPluginConfigToken copyMe) : base(copyMe)
        {
            this.GP = copyMe.GP;
            this.PathData = copyMe.PathData;
            this.Draw = copyMe.Draw;
            this.Scale = copyMe.Scale;
            this.SnapTo = copyMe.SnapTo;
            this.ShapeName = copyMe.ShapeName;
            this.SolidFill = copyMe.SolidFill;
        }

        public override object Clone()
        {
            return new EffectPluginConfigToken(this);
        }
    }
}
