#if !FASTDEBUG
using PaintDotNet;
using PaintDotNet.Direct2D1;
using PaintDotNet.Effects;
using PaintDotNet.Effects.Gpu;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Windows.Media;
using IDeviceContext = PaintDotNet.Direct2D1.IDeviceContext;

namespace ShapeMaker
{
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "ShapeMaker")]
    public class EffectPlugin : GpuDrawingEffect<EffectPluginConfigToken>
    {
        private bool draw;
        private DrawModes drawMode;
        private ColorBgra32 primaryColor;
        private ColorBgra32 secondaryColor;
        private float strokeThickness;
        private string geometryCode;

        public EffectPlugin()
            : base("ShapeMaker - Test", Properties.Resources.icon, "Advanced", GpuDrawingEffectOptions.Create() with { IsConfigurable = true })
        {
        }

        protected override IEffectConfigForm OnCreateConfigForm()
        {
            return new EffectPluginConfigDialog();
        }

        protected override void OnSetToken(EffectPluginConfigToken newToken)
        {
            this.draw = newToken.Draw;
            this.primaryColor = newToken.StrokeColor;
            this.secondaryColor = newToken.FillColor;
            this.strokeThickness = newToken.StrokeThickness;
            this.geometryCode = newToken.GeometryCode;
            this.drawMode = newToken.DrawMode;

            base.OnSetToken(newToken);
        }

        protected override void OnDraw(IDeviceContext dc)
        {
            dc.DrawImage(Environment.SourceImage);

            if (!this.draw || geometryCode == null)
            {
                return;
            }

            StreamGeometry wpfGeometry = StreamGeometryUtil.TryParseStreamGeometry(geometryCode);
            if (wpfGeometry == null)
            {
                return;
            }

            bool stroke = drawMode.HasFlag(DrawModes.Stroke);
            bool fill = drawMode.HasFlag(DrawModes.Fill);
            bool fit = drawMode.HasFlag(DrawModes.Fit);

            IDirect2DFactory d2dFactory = this.Services.GetService<IDirect2DFactory>();
            using IGeometry d2dGeometry = d2dFactory.CreateGeometryFromWpfGeometry(wpfGeometry);

            RectFloat geoBounds = fit ? d2dGeometry.GetWidenedBounds(strokeThickness) : new RectFloat(0, 0, 500, 500);
            RectInt32 selBounds = this.Environment.Selection.RenderBounds;

            float scale;
            if (fit)
            {
                const int padding = 5;
                scale = (selBounds.Width - geoBounds.Width) < (selBounds.Height - geoBounds.Height)
                    ? (selBounds.Width - padding) / geoBounds.Width
                    : (selBounds.Height - padding) / geoBounds.Height;
            }
            else
            {
                scale = Math.Max(selBounds.Width, selBounds.Height) / 500.0f;
            }

            float selCenterX = (selBounds.Right - selBounds.Left) / 2f + selBounds.Left;
            float selCenterY = (selBounds.Bottom - selBounds.Top) / 2f + selBounds.Top;

            Matrix3x2Float matrix = Matrix3x2Float.Translation(
                (selBounds.Width - geoBounds.Width) / 2f - geoBounds.Left + selBounds.Left,
                (selBounds.Height - geoBounds.Height) / 2f - geoBounds.Top + selBounds.Top);

            matrix.ScaleAt(scale, scale, selCenterX, selCenterY);

            using ITransformedGeometry transformedGeometry = d2dFactory.CreateTransformedGeometry(d2dGeometry, matrix);

            using ISolidColorBrush strokeBrush = dc.CreateSolidColorBrush(stroke ? primaryColor : LinearColors.Transparent);
            using ISolidColorBrush fillBrush = dc.CreateSolidColorBrush(!stroke ? primaryColor : fill ? secondaryColor : LinearColors.Transparent);

            using (dc.UseTranslateTransform(0.5f, 0.5f))
            {
                dc.FillGeometry(transformedGeometry, fillBrush);
                dc.DrawGeometry(transformedGeometry, strokeBrush, strokeThickness);
            }
        }
    }
}
#endif
