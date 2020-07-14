#if !FASTDEBUG
using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing;
using PaintDotNet.Threading;
using System.Threading;

namespace ShapeMaker
{
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "ShapeMaker")]
    public class EffectPlugin : Effect
    {
        private Surface shapeSurface;
        private readonly BinaryPixelOp normalOp = LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Normal);
        private bool draw;

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
            this.draw = token.Draw;

            if (this.draw)
            {
                Size srcSize = EnvironmentParameters.SourceSurface.Size;
                Rectangle selection = EnvironmentParameters.SelectionBounds;
                ColorBgra strokeColor = token.StrokeColor;
                ColorBgra fillColor = token.FillColor;
                double strokeThickness = token.StrokeThickness;
                string geometryCode = token.GeometryCode;
                DrawModes drawMode = token.DrawMode;

                PdnSynchronizationContext.Instance.Send(new SendOrPostCallback((object _) =>
                {
                    ShapeBuilder.SetEnviromentParams(srcSize.Width, srcSize.Height, selection.X, selection.Y, selection.Width, selection.Height, strokeColor, fillColor, strokeThickness);

                    ShapeBuilder.RenderShape(geometryCode, drawMode);
                }), null);

                this.shapeSurface?.Dispose();

                if (ShapeBuilder.ShapeBmp != null)
                {
                    this.shapeSurface = Surface.CopyFromBitmap(ShapeBuilder.ShapeBmp);
                }
                else
                {
                    this.draw = false;
                }
            }

            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
            dstArgs.Surface.CopySurface(srcArgs.Surface, rois, startIndex, length);
            if (this.draw && this.shapeSurface != null)
            {
                this.normalOp.Apply(dstArgs.Surface, this.shapeSurface, rois, startIndex, length);
            }
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                this.shapeSurface?.Dispose();
                this.shapeSurface = null;
            }

            base.OnDispose(disposing);
        }
    }
}
#endif
