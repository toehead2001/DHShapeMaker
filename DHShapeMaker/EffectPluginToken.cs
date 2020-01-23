#if PDNPLUGIN
using PaintDotNet;
using PaintDotNet.Effects;
using System.Collections.Generic;

namespace ShapeMaker
{
    internal class EffectPluginConfigToken : EffectConfigToken
    {
        public string GeometryCode { get; set; }
        internal List<PData> PathData { get; set; }
        internal bool Draw { get; set; }
        internal decimal Scale { get; set; }
        internal bool SnapTo { get; set; }
        internal string ShapeName { get; set; }
        internal bool SolidFill { get; set; }
        internal ColorBgra StrokeColor { get; set; }
        internal ColorBgra FillColor { get; set; }
        internal float StrokeThickness { get; set; }
        internal DrawMode DrawMode { get; set; }

        internal EffectPluginConfigToken(string geometryCode, List<PData> pathdata, bool draw, decimal scale, bool snap,
            string shapename, bool solidfill, ColorBgra strokeColor, ColorBgra fillColor, float strokeThickness, DrawMode drawMode)
        {
            this.GeometryCode = geometryCode;
            this.PathData = pathdata;
            this.Draw = draw;
            this.Scale = scale;
            this.SnapTo = snap;
            this.ShapeName = shapename;
            this.SolidFill = solidfill;
            this.StrokeColor = strokeColor;
            this.FillColor = fillColor;
            this.StrokeThickness = strokeThickness;
            this.DrawMode = drawMode;
        }

        private EffectPluginConfigToken(EffectPluginConfigToken copyMe) : base(copyMe)
        {
            this.GeometryCode = copyMe.GeometryCode;
            this.PathData = copyMe.PathData;
            this.Draw = copyMe.Draw;
            this.Scale = copyMe.Scale;
            this.SnapTo = copyMe.SnapTo;
            this.ShapeName = copyMe.ShapeName;
            this.SolidFill = copyMe.SolidFill;
            this.StrokeColor = copyMe.StrokeColor;
            this.FillColor = copyMe.FillColor;
            this.StrokeThickness = copyMe.StrokeThickness;
            this.DrawMode = copyMe.DrawMode;
        }

        public override object Clone()
        {
            return new EffectPluginConfigToken(this);
        }
    }
}
#endif
