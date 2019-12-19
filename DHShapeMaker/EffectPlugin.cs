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
        public EffectPlugin()
            : base("ShapeMaker - Test", Properties.Resources.icon, "Advanced", new EffectOptions { Flags = EffectFlags.Configurable })
        {
        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            return new EffectPluginConfigDialog();
        }

        protected override void OnSetRenderInfo(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)parameters;
            GraphicsPath[] paths = token.GP;
            bool draw = token.Draw;

            PdnRegion selectionRegion = this.EnvironmentParameters.GetSelection(srcArgs.Bounds);

            dstArgs.Surface.CopySurface(srcArgs.Surface, selectionRegion);

            if (draw && paths?.Length > 0)
            {
                using (Graphics g = new RenderArgs(dstArgs.Surface).Graphics)
                {
                    using (Region reg = new Region(selectionRegion.GetRegionData()))
                    {
                        g.SetClip(reg, CombineMode.Replace);
                    }
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    using (Pen p = new Pen(this.EnvironmentParameters.PrimaryColor))
                    {
                        p.Width = this.EnvironmentParameters.BrushWidth;
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        for (int i = 0; i < paths.Length; i++)
                        {
                            if (paths[i].PointCount > 0)
                            {
                                g.DrawPath(p, paths[i]);
                            }
                        }
                    }
                }
            }

            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
        }
    }
}
#endif
