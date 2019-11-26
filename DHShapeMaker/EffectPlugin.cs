#if PDNPLUGIN
using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShapeMaker
{
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "ShapeMaker")]
    public class EffectPlugin : Effect
    {
        internal const string StaticName = "ShapeMaker - Test";
        private static readonly Bitmap StaticImage = Properties.Resources.icon;
        private const string StaticSubMenuName = "Advanced";

        public EffectPlugin() : base(StaticName, StaticImage, StaticSubMenuName, EffectFlags.Configurable)
        {
        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            return new EffectPluginConfigDialog();
        }

        protected override void OnSetRenderInfo(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)parameters;
            this.PGP = token.GP;
            this.Draw = token.Draw;
            if (this.PGP != null)
            {
                MyRender(dstArgs.Surface, srcArgs.Surface);
            }

            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        private GraphicsPath[] PGP = new GraphicsPath[0];
        private bool Draw = false;

        private void MyRender(Surface dst, Surface src)
        {
            PdnRegion selectionRegion = this.EnvironmentParameters.GetSelection(src.Bounds);
            ColorBgra PrimaryColor = this.EnvironmentParameters.PrimaryColor;
            float BrushWidth = this.EnvironmentParameters.BrushWidth;

            dst.CopySurface(src, selectionRegion);

            if (this.PGP.Length > 0 && this.Draw)
            {
                using (Graphics g = new RenderArgs(dst).Graphics)
                {
                    using (Region reg = new Region(selectionRegion.GetRegionData()))
                    {
                        g.SetClip(reg, CombineMode.Replace);
                    }
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    using (Pen p = new Pen(PrimaryColor))
                    {
                        p.Width = BrushWidth;
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        for (int i = 0; i < this.PGP.Length; i++)
                        {
                            if (this.PGP[i].PointCount > 0)
                            {
                                g.DrawPath(p, this.PGP[i]);
                            }
                        }
                    }
                }
            }
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
        }
    }
}
#endif
