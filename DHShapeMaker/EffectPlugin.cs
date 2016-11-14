using System.Drawing;
using System.Drawing.Drawing2D;
using PaintDotNet;
using PaintDotNet.Effects;

namespace ShapeMaker
{
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "ShapeMaker")]
    public class EffectPlugin : Effect
    {
        public static string StaticName
        {
            get
            {
                return "ShapeMaker";
            }
        }

        public static Bitmap StaticImage
        {
            get { return ShapeMaker.Properties.Resources.icon; }
        }

        public static string StaticSubMenuName
        {
            get
            {
                return "Advanced";
            }
        }

        public EffectPlugin()
            : base(EffectPlugin.StaticName, EffectPlugin.StaticImage, EffectPlugin.StaticSubMenuName, EffectFlags.Configurable)
        {

        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            return new EffectPluginConfigDialog();
        }
        protected override void OnSetRenderInfo(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)parameters;
            PGP = token.GP;
            Draw = token.Draw;
            if (PGP != null) MyRender(dstArgs.Surface, srcArgs.Surface);
            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        GraphicsPath[] PGP = new GraphicsPath[0];
        bool Draw = false;

        private void MyRender(Surface dst, Surface src)
        {
            PdnRegion selectionRegion = EnvironmentParameters.GetSelection(src.Bounds);
            ColorBgra PrimaryColor = EnvironmentParameters.PrimaryColor;
            float BrushWidth = EnvironmentParameters.BrushWidth;

            dst.CopySurface(src, selectionRegion);

            if (PGP.Length > 0 && Draw)
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
                        for (int i = 0; i < PGP.Length; i++)
                        {
                            if (PGP[i].PointCount > 0)
                            {
                                g.DrawPath(p, PGP[i]);
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