using PaintDotNet.Controls;
using PaintDotNet.Direct2D1;
using PaintDotNet.Rendering;
using System;

namespace ShapeMaker
{
    public class ShapeCanvas
        : Direct2DPictureBox
    {
        public ShapeCanvas()
        {
            this.EnableAlphaCheckerboard = true;
        }

        public event EventHandler<RenderEventArgs> RenderForeground;
        public event EventHandler<RenderEventArgs> RenderBackground;

        protected override void OnRenderForeground(IDeviceContext deviceContext, RectFloat clipRect, RectFloat bitmapRect)
        {
            base.OnRenderForeground(deviceContext, clipRect, bitmapRect);

            this.RenderForeground?.Invoke(this, new RenderEventArgs(deviceContext, clipRect, bitmapRect));
        }

        protected override void OnRenderBackground(IDeviceContext deviceContext, RectFloat clipRect, RectFloat bitmapRect)
        {
            base.OnRenderBackground(deviceContext, clipRect, bitmapRect);

            this.RenderBackground?.Invoke(this, new RenderEventArgs(deviceContext, clipRect, bitmapRect));
        }
    }

    public class RenderEventArgs
        : EventArgs
    {
        public IDeviceContext DeviceContext { get; }
        public RectFloat ClipRect { get; }
        public RectFloat BitmapRect { get; }

        public RenderEventArgs(IDeviceContext deviceContext, RectFloat clipRect, RectFloat bitmapRect)
        {
            DeviceContext = deviceContext;
            ClipRect = clipRect;
            BitmapRect = bitmapRect;
        }
    }
}
