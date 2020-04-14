using PaintDotNet;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShapeMaker
{
    internal static class ShapeBuilder
    {
        internal static System.Drawing.Bitmap ShapeBmp;

        private static Size canvasSize;
        private static Rect selection;
        private static SolidColorBrush strokeBrush;
        private static SolidColorBrush fillBrush;
        private static double strokeThickness;

        internal static void SetEnviromentParams(int canvasWidth, int canvasHeight, int selectionX, int selectionY,
            int selectionWidth, int selctionHeight, ColorBgra strokeColor, ColorBgra fillColor, double strokeWidth)
        {
            canvasSize = new Size(canvasWidth, canvasHeight);
            selection = new Rect(selectionX, selectionY, selectionWidth, selctionHeight);
            strokeBrush = new SolidColorBrush(Color.FromArgb(strokeColor.A, strokeColor.R, strokeColor.G, strokeColor.B));
            fillBrush = new SolidColorBrush(Color.FromArgb(fillColor.A, fillColor.R, fillColor.G, fillColor.B));
            strokeThickness = strokeWidth;
        }

        internal static void RenderShape(string geometryCode, DrawModes drawMode)
        {
            ShapeBmp?.Dispose();
            ShapeBmp = null;

            if (string.IsNullOrWhiteSpace(geometryCode))
            {
                return;
            }

            StreamGeometry geometry = StreamGeometryUtil.TryParseStreamGeometry(geometryCode);
            if (geometryCode == null)
            {
                return;
            }

            RenderGeometry(geometry, drawMode);
        }

        private static void RenderGeometry(Geometry geometry, DrawModes drawMode)
        {
            const int padding = 5;

            bool stroke = drawMode.HasFlag(DrawModes.Stroke);
            bool fill = drawMode.HasFlag(DrawModes.Fill);
            bool fit = drawMode.HasFlag(DrawModes.Fit);

            double maxDim = Math.Max(selection.Width, selection.Height);

            double xOffset = fit ?
                selection.X :
                selection.X - (selection.Height > selection.Width ? (selection.Height - selection.Width) / 2 : 0);
            double yOffset = fit ?
                selection.Y :
                selection.Y - (selection.Width > selection.Height ? (selection.Width - selection.Height) / 2 : 0);

            // Negitive values seem to be treated as half values by Canvas Margin
            if (xOffset < 0) xOffset *= 2;
            if (yOffset < 0) yOffset *= 2;

            double width = fit ?
                 selection.Width - padding * 2 :
                 maxDim;
            double height = fit ?
                 selection.Height - padding * 2 :
                 maxDim;

            if (!fit)
            {
                double newScale = Math.Max(selection.Width, selection.Height) / 500.0;
                geometry = geometry.Clone();
                geometry.Transform = new ScaleTransform(newScale, newScale);
            }

            Path path = new Path
            {
                Data = geometry,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stroke = stroke ? strokeBrush : Brushes.Transparent,
                Fill = !stroke ? strokeBrush : fill ? fillBrush : Brushes.Transparent,
                StrokeThickness = strokeThickness,
                Stretch = fit ? Stretch.Uniform : Stretch.None,
                Width = width,
                Height = height,
                Margin = fit ? new Thickness(padding) : new Thickness(0)
            };

            Canvas canvas = new Canvas
            {
                Width = canvasSize.Width,
                Height = canvasSize.Height,
                Margin = new Thickness(xOffset, yOffset, 0, 0),
                Background = Brushes.Transparent
            };

            canvas.Children.Add(path);

            canvas.Measure(new Size(canvas.Width, canvas.Height));
            canvas.Arrange(new Rect(new Size(canvas.Width, canvas.Height)));

            CreateBitmap(canvas, (int)canvas.Width, (int)canvas.Height);
        }

        private static void CreateBitmap(Visual visual, int width, int height)
        {
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            BmpBitmapEncoder image = new BmpBitmapEncoder();
            image.Frames.Add(BitmapFrame.Create(bitmap));
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                image.Save(ms);
                ms.Seek(0, System.IO.SeekOrigin.Begin);
                ShapeBmp = new System.Drawing.Bitmap(ms);
            }
        }
    }
}
