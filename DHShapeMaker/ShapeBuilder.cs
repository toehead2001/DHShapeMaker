using PaintDotNet;
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

        internal static void RenderShape(string geometryCode, DrawMode drawMode)
        {
            ShapeBmp?.Dispose();
            ShapeBmp = null;

            if (string.IsNullOrWhiteSpace(geometryCode))
            {
                return;
            }

            StreamGeometry geometry = TryParseStreamGeometry(geometryCode);
            if (geometryCode == null)
            {
                return;
            }

            RenderGeometry(geometry, drawMode);
        }

        private static StreamGeometry TryParseStreamGeometry(string streamGeometry)
        {
            StreamGeometry geometry = null;

            try
            {
                geometry = (StreamGeometry)Geometry.Parse(streamGeometry);
            }
            catch
            {
            }

            return geometry;
        }

        private static void RenderGeometry(Geometry geometry, DrawMode drawMode)
        {
            const int padding = 5;
            bool stroke = drawMode.HasFlag(DrawMode.Stroke);
            bool fill = drawMode.HasFlag(DrawMode.Fill);

            Path path = new Path
            {
                Data = geometry,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stroke = stroke ? strokeBrush : Brushes.Transparent,
                Fill = !stroke ? strokeBrush : fill ? fillBrush : Brushes.Transparent,
                StrokeThickness = strokeThickness,
                Stretch = Stretch.Uniform,
                Width = selection.Width - padding * 2,
                Height = selection.Height - padding * 2,
                Margin = new Thickness(padding)
            };

            Canvas canvas = new Canvas
            {
                Width = canvasSize.Width,
                Height = canvasSize.Height,
                Margin = new Thickness(selection.X, selection.Y, 0, 0),
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
