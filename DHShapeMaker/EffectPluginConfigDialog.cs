#if !FASTDEBUG
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
#endif
using PaintDotNet.Controls;
using PaintDotNet.Direct2D1;
using PaintDotNet.Direct2D1.Effects;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace ShapeMaker
{
#if !FASTDEBUG
    internal partial class EffectPluginConfigDialog : EffectConfigForm<EffectPlugin, EffectPluginConfigToken>
#else
    internal partial class EffectPluginConfigDialog : Form
#endif
    {
        private PathType activeType;
        private readonly IEnumerable<ToolStripButtonWithKeys> typeButtons;

        private const int maxPoints = byte.MaxValue;
        private const int InvalidPath = -1;
        private const int InvalidNub = -1;
        private int clickedNub = InvalidNub;
        private PointF moveStart;
        private readonly List<PointF> canvasPoints = new List<PointF>(maxPoints);

        private const int historyMax = 16;
        private readonly List<PathData>[] undoPaths = new List<PathData>[historyMax];
        private readonly PathData[] undoCanvas = new PathData[historyMax];
        private readonly int[] undoSelected = new int[historyMax];
        private int undoCount = 0;
        private int redoCount = 0;
        private int historyIndex = 0;

        private bool keyTrak = false;
        private readonly List<PathData> paths = new List<PathData>();
        private bool panFlag = false;
        private bool canScrollZoom = false;
        private static float dpiScale = 1;
        private Control hadFocus;
        private bool isNewPath = true;
        private int canvasBaseSize;
#if !FASTDEBUG
        private bool drawClippingArea = false;
        private string geometryForPdnCanvas = null;
#else
        private Bitmap clipboardImage = null;
#endif
        private bool moveFlag = false;
        private bool drawAverage = false;
        private bool magneticallyLinked = false;
        private PointF averagePoint = new PointF(0.5f, 0.5f);
        private float initialDist;
        private SizeF initialDistSize;
        private double initialRads;
        private Size clickOffset;
        private Operation operation;
        private Rectangle operationBox = Rectangle.Empty;
        private Tuple<int, int> operationRange = new Tuple<int, int>(-1, -1);
        private readonly List<LinkFlags> linkFlagsList = new List<LinkFlags>();
        private int oldPathListBoxIndex = -1;
        private static readonly IList<int> zoomFactors = [1, 2, 5, 10, 15, 20];

        private readonly Dictionary<Keys, ToolStripButtonWithKeys> hotKeys = new Dictionary<Keys, ToolStripButtonWithKeys>();

        private PathType PathTypeFromUI
        {
            get => this.activeType;
        }

        private CloseType CloseTypeFromUI
        {
            get => this.ClosePath.Checked ? CloseType.Individual : this.CloseContPaths.Checked ? CloseType.Contiguous : CloseType.None;
        }

        private ArcOptions ArcOptionsFromUI
        {
            get
            {
                if (this.activeType != PathType.EllipticalArc)
                {
                    return ArcOptions.None;
                }

                ArcOptions arcOptions = ArcOptions.None;

                if (this.Arc.CheckState == CheckState.Checked)
                {
                    arcOptions |= ArcOptions.LargeArc;
                }

                if (this.Sweep.CheckState == CheckState.Checked)
                {
                    arcOptions |= ArcOptions.PositiveSweep;
                }

                return arcOptions;
            }
        }

        internal EffectPluginConfigDialog()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentUICulture;
            InitializeComponent();

#if FASTDEBUG
            this.buttonOK.Visible = false;
            this.DrawOnCanvas.Visible = false;
            this.strokeColorPanel.Visible = false;
            this.fillColorPanel.Visible = false;
            this.strokeThicknessBox.Visible = false;
            this.drawModeBox.Visible = false;
            this.fitCanvasBox.Visible = false;
            this.ShowInTaskbar = true;
#endif
            this.toolStripUndo.Renderer = new ThemeRenderer(Color.White, Color.Silver);
            this.toolStripBlack.Renderer = new ThemeRenderer(PathType.Straight);
            this.toolStripBlue.Renderer = new ThemeRenderer(PathType.Cubic);
            this.toolStripGreen.Renderer = new ThemeRenderer(PathType.SmoothCubic);
            this.toolStripYellow.Renderer = new ThemeRenderer(PathType.Quadratic);
            this.toolStripPurple.Renderer = new ThemeRenderer(PathType.SmoothQuadratic);
            this.toolStripRed.Renderer = new ThemeRenderer(PathType.EllipticalArc);
            this.toolStripOptions.Renderer = new ThemeRenderer(Color.White, Color.Silver);

            this.typeButtons = new ToolStripButtonWithKeys[]
            {
                this.StraightLine,
                this.Elliptical,
                this.CubicBezier,
                this.SCubicBezier,
                this.QuadBezier,
                this.SQuadBezier
            };

            // Zoom drop down
            ZoomMenuItem[] zoomMenuItems = zoomFactors
                .Reverse()
                .Select(zf => new ZoomMenuItem($"{zf}x", zf, ZoomDropDownItem_Click))
                .ToArray();

            splitButtonZoom.DropDownItems.AddRange(zoomMenuItems);

            if (splitButtonZoom.DropDown is ToolStripDropDownMenu menu)
            {
                menu.ShowImageMargin = false;
            }
        }

#if !FASTDEBUG
        protected override bool UseAppThemeColorsDefault => false;

        #region Effect Token functions
        protected override EffectConfigToken OnCreateInitialToken()
        {
            return new EffectPluginConfigToken(this.geometryForPdnCanvas, this.paths, false, 100, true, "Untitled", false, ColorBgra.Zero, ColorBgra.Zero, 0, DrawModes.Stroke);
        }

        protected override void OnUpdateTokenFromDialog(EffectPluginConfigToken dstToken)
        {
            dstToken.GeometryCode = this.geometryForPdnCanvas;
            dstToken.PathData = this.paths;
            dstToken.Draw = this.DrawOnCanvas.Checked;
            dstToken.ShapeName = this.FigureName.Text;
            dstToken.SnapTo = this.Snap.Checked;
            dstToken.SolidFill = this.solidFillCheckBox.Checked;
            dstToken.StrokeColor = this.strokeColorPanel.BackColor;
            dstToken.FillColor = this.fillColorPanel.BackColor;
            dstToken.StrokeThickness = (float)this.strokeThicknessBox.Value;

            dstToken.DrawMode = this.drawModeBox.SelectedIndex switch
            {
                0 => DrawModes.Stroke,
                1 => DrawModes.Fill,
                2 => DrawModes.Stroke | DrawModes.Fill,
                _ => DrawModes.Stroke,
            };

            if (this.fitCanvasBox.Checked)
            {
                dstToken.DrawMode |= DrawModes.Fit;
            }
        }

        protected override void OnUpdateDialogFromToken(EffectPluginConfigToken token)
        {
            this.DrawOnCanvas.Checked = token.Draw;
            this.FigureName.Text = token.ShapeName;
            this.Snap.Checked = token.SnapTo;
            this.solidFillCheckBox.Checked = token.SolidFill;
            this.strokeColorPanel.BackColor = (token.StrokeColor == ColorBgra.Zero) ? this.Environment.PrimaryColor.GetSrgb() : token.StrokeColor;
            this.fillColorPanel.BackColor = (token.FillColor == ColorBgra.Zero) ? this.Environment.SecondaryColor.GetSrgb() : token.FillColor;
            this.strokeThicknessBox.Value = Math.Clamp(
                (token.StrokeThickness == 0) ? (decimal)this.Environment.BrushSize : (decimal)token.StrokeThickness,
                this.strokeThicknessBox.Minimum,
                this.strokeThicknessBox.Maximum);

            DrawModes drawMode = token.DrawMode;
            if (drawMode.HasFlag(DrawModes.Stroke) &&
                drawMode.HasFlag(DrawModes.Fill))
            {
                this.drawModeBox.SelectedIndex = 2;
            }
            else if (drawMode.HasFlag(DrawModes.Fill))
            {
                this.drawModeBox.SelectedIndex = 1;
            }
            else
            {
                this.drawModeBox.SelectedIndex = 0;
            }

            this.fitCanvasBox.Checked = drawMode.HasFlag(DrawModes.Fit);

            IEnumerable<PathData> tmp = new List<PathData>(token.PathData);
            this.paths.Clear();
            this.PathListBox.Items.Clear();
            foreach (PathData p in tmp)
            {
                this.paths.Add(p);
                this.PathListBox.Items.Add(p.PathType.GetName());
            }

            this.drawClippingArea = this.DrawOnCanvas.Checked && !this.fitCanvasBox.Checked;

            RefreshPdnCanvas();
        }
        #endregion
#endif

        #region Form functions
        private void EffectPluginConfigDialog_Load(object sender, EventArgs e)
        {
            dpiScale = this.DeviceDpi / 96f;
            this.canvasBaseSize = this.canvas.Width;

            setTraceImage();

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = "ShapeMaker - v" + version + " - Test";

            this.Arc.Enabled = false;
            this.Sweep.Enabled = false;

            this.toolTip1.ReshowDelay = 0;
            this.toolTip1.AutomaticDelay = 0;
            this.toolTip1.AutoPopDelay = 0;
            this.toolTip1.InitialDelay = 0;
            this.toolTip1.UseFading = false;
            this.toolTip1.UseAnimation = false;

            this.timer1.Enabled = true;

            #region DPI fixes
            this.MinimumSize = this.Size;
            this.PathListBox.ItemHeight = getDpiSize(this.PathListBox.ItemHeight);
            this.PathListBox.Height = this.upList.Top - this.PathListBox.Top;
            this.statusLabelNubsUsed.Width = getDpiSize(this.statusLabelNubsUsed.Width);
            this.statusLabelPathsUsed.Width = getDpiSize(this.statusLabelPathsUsed.Width);
            this.statusLabelNubPos.Width = getDpiSize(this.statusLabelNubPos.Width);
            this.statusLabelMousePos.Width = getDpiSize(this.statusLabelMousePos.Width);
            this.horScrollBar.Height = getDpiSize(this.horScrollBar.Height);
            this.verScrollBar.Width = getDpiSize(this.verScrollBar.Width);
            this.traceLayer.Left = this.traceClipboard.Left;

            this.toolStripUndo.AutoSize = this.toolStripBlack.AutoSize = this.toolStripBlue.AutoSize = this.toolStripGreen.AutoSize =
                this.toolStripYellow.AutoSize = this.toolStripPurple.AutoSize = this.toolStripRed.AutoSize = this.toolStripOptions.AutoSize = false;
            this.toolStripUndo.ImageScalingSize = this.toolStripBlack.ImageScalingSize = this.toolStripBlue.ImageScalingSize =
                this.toolStripGreen.ImageScalingSize = this.toolStripYellow.ImageScalingSize = this.toolStripPurple.ImageScalingSize =
                this.toolStripRed.ImageScalingSize = this.toolStripOptions.ImageScalingSize = getDpiSize(this.toolStripOptions.ImageScalingSize);
            this.toolStripUndo.AutoSize = this.toolStripBlack.AutoSize = this.toolStripBlue.AutoSize = this.toolStripGreen.AutoSize =
                this.toolStripYellow.AutoSize = this.toolStripPurple.AutoSize = this.toolStripRed.AutoSize = this.toolStripOptions.AutoSize = true;

            this.toolStripBlack.Left = this.toolStripUndo.Right;
            this.toolStripBlue.Left = this.toolStripBlack.Right;
            this.toolStripGreen.Left = this.toolStripBlue.Right;
            this.toolStripYellow.Left = this.toolStripGreen.Right;
            this.toolStripPurple.Left = this.toolStripYellow.Right;
            this.toolStripRed.Left = this.toolStripPurple.Right;
            this.toolStripOptions.Left = this.toolStripRed.Right;
            #endregion

            adjustForWindowSize();

            this.statusLabelPathsUsed.Text = $"{this.PathListBox.Items.Count} Paths";

            // Store hotkeys in a Dictionary
            foreach (ToolStripButtonWithKeys button in this.Controls.OfType<ToolStrip>().SelectMany(ts => ts.Items.OfType<ToolStripButtonWithKeys>()))
            {
                Keys keys = button.ShortcutKeys;
                if (keys != Keys.None && !this.hotKeys.ContainsKey(keys))
                {
                    this.hotKeys.Add(keys, button);
                }
            }
        }

        private static Size getDpiSize(Size size)
        {
            return new Size
            {
                Width = (int)Math.Round(size.Width * dpiScale),
                Height = (int)Math.Round(size.Height * dpiScale)
            };
        }

        private static int getDpiSize(int dimension)
        {
            return (int)Math.Round(dimension * dpiScale);
        }

        private void EffectPluginConfigDialog_Resize(object sender, EventArgs e)
        {
            adjustForWindowSize();
        }

        private void adjustForWindowSize()
        {
            this.viewport.Width = this.PathListBox.Left - this.viewport.Left - getDpiSize(32);
            this.viewport.Height = this.statusStrip1.Top - this.viewport.Top - getDpiSize(20);

            this.horScrollBar.Top = this.viewport.Bottom;
            this.horScrollBar.Width = this.viewport.Width;

            this.verScrollBar.Left = this.viewport.Right;
            this.verScrollBar.Height = this.viewport.Height;

            Point newCanvasPos = this.canvas.Location;
            if (this.canvas.Width < this.viewport.ClientSize.Width || this.canvas.Location.X > 0)
            {
                newCanvasPos.X = (this.viewport.ClientSize.Width - this.canvas.Width) / 2;
            }

            if (this.canvas.Height < this.viewport.ClientSize.Height || this.canvas.Location.Y > 0)
            {
                newCanvasPos.Y = (this.viewport.ClientSize.Height - this.canvas.Height) / 2;
            }

            this.canvas.Location = newCanvasPos;

            UpdateScrollBars();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (this.canvasPoints.Count > 1 && this.PathListBox.SelectedIndex == InvalidPath)
                {
                    AddNewPath();
                }

                return true;
            }

            if (keyData == Keys.Escape)
            {
                Deselect();
                return true;
            }

            if (this.hotKeys.ContainsKey(keyData))
            {
                ToolStripButtonWithKeys button = this.hotKeys[keyData];

                if (button.Enabled)
                {
                    button.PerformClick();
                    return true;
                }

                return false;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        #endregion

        #region Undo History functions
        private void setUndo(bool deSelected = false)
        {
            this.Undo.Enabled = true;
            this.Redo.Enabled = false;

            this.redoCount = 0;
            this.undoCount++;
            this.undoCount = (this.undoCount > historyMax) ? historyMax : this.undoCount;
            this.undoSelected[this.historyIndex] = deSelected ? InvalidPath : this.PathListBox.SelectedIndex;
            this.undoCanvas[this.historyIndex] = new PathData(this.PathTypeFromUI, this.canvasPoints, this.CloseTypeFromUI, this.ArcOptionsFromUI);

            if (this.undoPaths[this.historyIndex] == null)
            {
                this.undoPaths[this.historyIndex] = new List<PathData>();
            }
            else
            {
                this.undoPaths[this.historyIndex].Clear();
            }

            foreach (PathData pd in this.paths)
            {
                PathData clonedPath = new PathData(pd.PathType, pd.Points, pd.CloseType, pd.ArcOptions, pd.Alias);
                this.undoPaths[this.historyIndex].Add(clonedPath);
            }

            this.historyIndex++;
            this.historyIndex %= historyMax;
        }

        private void Undo_Click(object sender, EventArgs e)
        {
            if (this.undoCount == 0)
            {
                return;
            }

            if (this.redoCount == 0)
            {
                setUndo();
                this.undoCount--;
                this.historyIndex--;
            }

            this.historyIndex--;
            this.historyIndex += historyMax;
            this.historyIndex %= historyMax;

            PerformUndoOrRedo();

            this.undoCount--;
            this.undoCount = (this.undoCount < 0) ? 0 : this.undoCount;
            this.redoCount++;

            this.Undo.Enabled = (this.undoCount > 0);
            this.Redo.Enabled = true;
        }

        private void Redo_Click(object sender, EventArgs e)
        {
            if (this.redoCount == 0)
            {
                return;
            }

            this.historyIndex++;
            this.historyIndex += historyMax;
            this.historyIndex %= historyMax;

            PerformUndoOrRedo();

            this.undoCount++;
            this.redoCount--;

            this.Redo.Enabled = (this.redoCount > 0);
            this.Undo.Enabled = true;
        }

        private void PerformUndoOrRedo()
        {
            this.PathListBox.Items.Clear();
            this.paths.Clear();

            if (this.undoPaths[this.historyIndex].Count != 0)
            {
                this.PathListBox.SelectedIndexChanged -= PathListBox_SelectedIndexChanged;
                foreach (PathData pd in this.undoPaths[this.historyIndex])
                {
                    PathData clonedPath = new PathData(pd.PathType, pd.Points, pd.CloseType, pd.ArcOptions, pd.Alias);
                    this.paths.Add(clonedPath);
                    this.PathListBox.Items.Add(pd.PathType.GetName());
                }

                if (this.undoSelected[this.historyIndex] < this.PathListBox.Items.Count)
                {
                    this.PathListBox.SelectedIndex = this.undoSelected[this.historyIndex];
                }

                this.PathListBox.SelectedIndexChanged += PathListBox_SelectedIndexChanged;
            }

            PathData path = (this.PathListBox.SelectedIndex != InvalidPath)
                ? this.paths[this.PathListBox.SelectedIndex]
                : this.undoCanvas[this.historyIndex];

            SetUiForPath(path);

            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();
        }

        private void resetHistory()
        {
            this.undoCount = 0;
            this.redoCount = 0;
            this.historyIndex = 0;
            this.Undo.Enabled = false;
            this.Redo.Enabled = false;
        }
        #endregion

        #region Canvas functions
        private void Canvas_RenderForeground(object sender, RenderEventArgs e)
        {
            #region Grid
            using ICommandList gridCmdList = e.DeviceContext.CreateCommandList();

            using (gridCmdList.UseBeginDraw(e.DeviceContext))
            using (e.DeviceContext.UseTranslateTransform(0.5f, 0.5f))
            using (e.DeviceContext.UsePrimitiveBlend(PrimitiveBlend.Copy))
            {
                int left = (int)Math.Floor(e.ClipRect.Left);
                int top = (int)Math.Floor(e.ClipRect.Top);
                int right = (int)Math.Ceiling(e.ClipRect.Right);
                int bottom = (int)Math.Ceiling(e.ClipRect.Bottom);

                RectInt32 clipRectInt32 = RectInt32.FromEdges(left - (left % 50) - 1, top - (top % 50) - 1, right, bottom);
                float opacity = (100 - this.opacitySlider.Value) / 100f;

                using IDeviceBrush bgBrush = e.DeviceContext.CreateSolidColorBrush(new ColorRgb24(255, 255, 255), opacity);
                e.DeviceContext.FillRectangle(clipRectInt32, bgBrush);

                using IDeviceBrush tenBrush = e.DeviceContext.CreateSolidColorBrush(new ColorRgb24(224, 224, 224), opacity);
                using IDeviceBrush fiftyBrush = e.DeviceContext.CreateSolidColorBrush(new ColorRgb24(181, 181, 181), opacity);

                for (int x = clipRectInt32.Left; x < clipRectInt32.Right; x += 10)
                {
                    e.DeviceContext.DrawLine(x, clipRectInt32.Top, x, clipRectInt32.Bottom, tenBrush);
                }

                for (int y = clipRectInt32.Top; y < clipRectInt32.Bottom; y += 10)
                {
                    e.DeviceContext.DrawLine(clipRectInt32.Left, y, clipRectInt32.Right, y, tenBrush);
                }

                for (int x = clipRectInt32.Left; x < clipRectInt32.Right; x += 50)
                {
                    e.DeviceContext.DrawLine(x, clipRectInt32.Top, x, clipRectInt32.Bottom, fiftyBrush);
                }

                for (int y = clipRectInt32.Top; y < clipRectInt32.Bottom; y += 50)
                {
                    e.DeviceContext.DrawLine(clipRectInt32.Left, y, clipRectInt32.Right, y, fiftyBrush);
                }
            }

            e.DeviceContext.DrawImage(gridCmdList);
            #endregion

#if !FASTDEBUG
            if (this.drawClippingArea)
            {
                RectInt32 hatchRect = new RectInt32(0, 0, 8, 8);

                using ICommandList hatchCmdList = e.DeviceContext.CreateCommandList();

                using (hatchCmdList.UseBeginDraw(e.DeviceContext))
                {
                    using IDeviceBrush whiteBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.White);
                    e.DeviceContext.FillRectangle(hatchRect, whiteBrush);

                    using IDeviceBrush grayBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.Gray);

                    e.DeviceContext.DrawLine(hatchRect.TopLeft, hatchRect.BottomRight, grayBrush);
                    e.DeviceContext.DrawLine(hatchRect.TopRight, hatchRect.BottomLeft, grayBrush);
                }

                using IImageBrush hatchBrush = e.DeviceContext.CreateImageBrush(hatchCmdList, hatchRect, ExtendMode.Wrap, ExtendMode.Wrap);

                using IPathGeometry geometry = e.DeviceContext.Factory.CreatePathGeometry();
                using IGeometrySink sink = geometry.Open();

                ReadOnlySpan<Point2Float> clipPoints = e.ClipRect.ToPoints();
                ReadOnlySpan<Point2Float> bmpPoints = e.BitmapRect.ToPoints();

                sink.BeginFigure(clipPoints[0], FigureBegin.Filled);
                sink.AddLines(clipPoints[1..]);
                sink.EndFigure(FigureEnd.Closed);

                sink.BeginFigure(bmpPoints[0], FigureBegin.Filled);
                sink.AddLines(bmpPoints[1..]);
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();

                e.DeviceContext.FillGeometry(geometry, hatchBrush);

                using var bmpOutlineBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.Gray);
                e.DeviceContext.DrawRectangle(e.BitmapRect, bmpOutlineBrush);
            }
#endif
            int selectedIndex = this.PathListBox.SelectedIndex;
            int canvasPointCount = this.canvasPoints.Count;
            Size canvasSize = this.canvas.ClientSize;

            #region Draw Paths
            const int normalStrokeWidth = 1;
            const int activeStrokeWidth = 5;
            const int operationStrokeWidth = 15;

            using ISolidColorBrush operationBrush = e.DeviceContext.CreateSolidColorBrush(Color.FromArgb(85, Color.Yellow));

            using IStrokeStyle strokeStyle = e.DeviceContext.Factory.CreateStrokeStyle(
                StrokeStyleProperties.Default with
                {
                    LineJoin = PaintDotNet.Direct2D1.LineJoin.Round
                });

            using IStrokeStyle dashedStrokeStyle = e.DeviceContext.Factory.CreateStrokeStyle(
                StrokeStyleProperties.Default with
                {
                    LineJoin = PaintDotNet.Direct2D1.LineJoin.Round,
                    DashStyle = PaintDotNet.Direct2D1.DashStyle.Dash
                });

            PointF loopBack = new PointF(-9999, -9999);
            PointF previousEndPoint = new PointF(-9999, -9999);
            bool previousClosed = false;

            bool isNewPath = selectedIndex == InvalidPath;

            PathType pathType = 0;
            bool closedIndividual = false;
            bool closedContiguous = false;
            ArcSize arcSize = ArcSize.Small;
            SweepDirection sweepDirection = SweepDirection.Counterclockwise;
            IReadOnlyList<PointF> pPoints;

            using (e.DeviceContext.UseTranslateTransform(0.5f, 0.5f))
            {
                int j;
                for (int jj = -1; jj < this.paths.Count; jj++)
                {
                    j = jj + 1;
                    if (j == this.paths.Count && isNewPath)
                    {
                        j = -1;
                    }

                    if (j >= this.paths.Count)
                    {
                        continue;
                    }

                    bool isActive = j == selectedIndex;

                    if (isActive)
                    {
                        pPoints = this.canvasPoints;
                        pathType = this.PathTypeFromUI;
                        closedIndividual = this.ClosePath.Checked;
                        closedContiguous = this.CloseContPaths.Checked;
                        arcSize = (this.Arc.CheckState == CheckState.Checked) ? ArcSize.Large : ArcSize.Small;
                        sweepDirection = (this.Sweep.CheckState == CheckState.Checked) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
                    }
                    else
                    {
                        PathData itemPath = this.paths[j];
                        pPoints = itemPath.Points;
                        pathType = itemPath.PathType;
                        closedIndividual = itemPath.CloseType == CloseType.Individual;
                        closedContiguous = itemPath.CloseType == CloseType.Contiguous;
                        arcSize = itemPath.ArcOptions.HasFlag(ArcOptions.LargeArc) ? ArcSize.Large : ArcSize.Small;
                        sweepDirection = itemPath.ArcOptions.HasFlag(ArcOptions.PositiveSweep) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
                    }

                    if (pPoints.Count == 0)
                    {
                        continue;
                    }

                    bool partOfOperation = false;
                    if (!this.operationBox.IsEmpty)
                    {
                        if (isActive)
                        {
                            partOfOperation = true;
                        }
                        else if (selectedIndex == InvalidPath)
                        {
                            if (canvasPointCount > 1)
                            {
                                if (j >= this.operationRange.Item1 && j <= this.operationRange.Item2)
                                {
                                    partOfOperation = true;
                                }
                            }
                            else
                            {
                                partOfOperation = true;
                            }
                        }
                        else if (j >= this.operationRange.Item1 && j <= this.operationRange.Item2)
                        {
                            partOfOperation = true;
                        }
                    }

                    Point2Float[] pts = new Point2Float[pPoints.Count];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        pts[i].X = canvasSize.Width * pPoints[i].X;
                        pts[i].Y = canvasSize.Height * pPoints[i].Y;
                    }

                    int lastIndex = pts.Length - 1;

                    if (previousClosed || !previousEndPoint.Equals(pts[0]) || (isActive && this.ClosePath.Checked))
                    {
                        loopBack = new PointF(pts[0].X, pts[0].Y);
                    }

                    Color pathColor = pathType.GetColor();
                    Color pathLightColor = pathType.GetLightColor();
                    Color activeColor = partOfOperation ? pathLightColor : Color.FromArgb(51, pathColor);

                    using ISolidColorBrush baseBrush = e.DeviceContext.CreateSolidColorBrush(pathColor);
                    using ISolidColorBrush activeBrush = e.DeviceContext.CreateSolidColorBrush(activeColor);

                    switch (pathType)
                    {
                        case PathType.Straight:
                            if (pts.Length > 1)
                            {
                                if (this.MacroRect.Checked && j == -1 && isNewPath)
                                {
                                    for (int i = 1; i < pts.Length; i++)
                                    {
                                        Point2Float[] rectPts =
                                        {
                                            new PointF(pts[i - 1].X, pts[i - 1].Y),
                                            new PointF(pts[i].X, pts[i - 1].Y),
                                            new PointF(pts[i].X, pts[i].Y),
                                            new PointF(pts[i - 1].X, pts[i].Y),
                                            new PointF(pts[i - 1].X, pts[i - 1].Y)
                                        };

                                        using IPathGeometry rectGeometry = e.DeviceContext.Factory.CreateLinesPathGeometry(rectPts);

                                        if (partOfOperation)
                                        {
                                            e.DeviceContext.DrawGeometry(rectGeometry, operationBrush, operationStrokeWidth);
                                        }

                                        e.DeviceContext.DrawGeometry(rectGeometry, activeBrush, activeStrokeWidth);
                                        e.DeviceContext.DrawGeometry(rectGeometry, baseBrush, normalStrokeWidth);
                                    }
                                }
                                else
                                {
                                    using IPathGeometry linesGeometry = e.DeviceContext.Factory.CreateLinesPathGeometry(pts);

                                    if (partOfOperation)
                                    {
                                        e.DeviceContext.DrawGeometry(linesGeometry, operationBrush, operationStrokeWidth, strokeStyle);
                                    }

                                    if (isActive)
                                    {
                                        e.DeviceContext.DrawGeometry(linesGeometry, activeBrush, activeStrokeWidth, strokeStyle);
                                    }

                                    e.DeviceContext.DrawGeometry(linesGeometry, baseBrush, normalStrokeWidth, strokeStyle);
                                }
                            }
                            break;
                        case PathType.EllipticalArc:
                            if (pts.Length == 5)
                            {
                                Point2Float start = pts[0];
                                Point2Float end = pts[4];
                                PointF center = PointFUtil.PointAverage(start, end);

                                if (this.MacroCircle.Checked && j == -1 && isNewPath)
                                {
                                    float radius = PointFUtil.Hypot(start, end) / 2f;
                                    Ellipse ellipse = new Ellipse(center, radius);

                                    if (partOfOperation)
                                    {
                                        e.DeviceContext.DrawEllipse(ellipse, operationBrush, operationStrokeWidth);
                                    }

                                    e.DeviceContext.DrawEllipse(ellipse, activeBrush, activeStrokeWidth);
                                    e.DeviceContext.DrawEllipse(ellipse, baseBrush, normalStrokeWidth);
                                }
                                else
                                {
                                    SizeFloat radius = new SizeFloat(
                                        PointFUtil.Hypot(center, pts[1]),
                                        PointFUtil.Hypot(center, pts[2]));

                                    if ((int)radius.Width == 0 || (int)radius.Height == 0)
                                    {
                                        if (partOfOperation)
                                        {
                                            e.DeviceContext.DrawLine(start, end, operationBrush, operationStrokeWidth);
                                        }

                                        if (isActive)
                                        {
                                            e.DeviceContext.DrawLine(start, end, activeBrush, activeStrokeWidth);
                                        }

                                        e.DeviceContext.DrawLine(start, end, baseBrush, normalStrokeWidth);
                                    }
                                    else if (start != end)
                                    {
                                        float angleRadians = float.Atan2(pts[3].Y - center.Y, pts[3].X - center.X);
                                        float angleDegrees = float.RadiansToDegrees(angleRadians);

                                        using IPathGeometry arcGeometry = e.DeviceContext.Factory.CreateArcPathGeometry(
                                            start, end, radius, angleDegrees, sweepDirection, arcSize);

                                        if (partOfOperation)
                                        {
                                            e.DeviceContext.DrawGeometry(arcGeometry, operationBrush, operationStrokeWidth);
                                        }

                                        if (isActive)
                                        {
                                            e.DeviceContext.DrawGeometry(arcGeometry, activeBrush, activeStrokeWidth);
                                        }

                                        e.DeviceContext.DrawGeometry(arcGeometry, baseBrush, normalStrokeWidth);

                                        if (isActive)
                                        {
                                            SweepDirection activeDirection = (sweepDirection == SweepDirection.Counterclockwise) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
                                            ArcSize activeSize = (arcSize == ArcSize.Small) ? ArcSize.Large : ArcSize.Small;

                                            using IPathGeometry activeArcGeometry = e.DeviceContext.Factory.CreateArcPathGeometry(
                                                start, end, radius, angleDegrees, activeDirection, activeSize);

                                            using ISolidColorBrush activeArcBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.Silver);
                                            e.DeviceContext.DrawGeometry(activeArcGeometry, activeArcBrush, normalStrokeWidth, dashedStrokeStyle);
                                        }
                                    }
                                }
                            }
                            break;
                        case PathType.Cubic:
                        case PathType.SmoothCubic:
                            if (pts.Length > 3)
                            {
                                using IPathGeometry cubicGeometry = e.DeviceContext.Factory.CreateBeziersPathGeometry(pts);

                                if (partOfOperation)
                                {
                                    e.DeviceContext.DrawGeometry(cubicGeometry, operationBrush, operationStrokeWidth);
                                }

                                if (isActive)
                                {
                                    e.DeviceContext.DrawGeometry(cubicGeometry, activeBrush, activeStrokeWidth);
                                }

                                e.DeviceContext.DrawGeometry(cubicGeometry, baseBrush, normalStrokeWidth);
                            }
                            break;
                        case PathType.Quadratic:
                        case PathType.SmoothQuadratic:
                            if (pts.Length > 3)
                            {
                                #region cube to quad
                                Point2Float[] Qpts = new Point2Float[pts.Length];
                                for (int i = 0; i < pts.Length; i++)
                                {
                                    switch (CanvasUtil.GetNubType(i))
                                    {
                                        case NubType.StartPoint:
                                        case NubType.EndPoint:
                                            Qpts[i] = pts[i];
                                            break;
                                        case NubType.ControlPoint1:
                                            Qpts[i] = new PointF(pts[i].X * 2f / 3f + pts[i - 1].X * 1f / 3f,
                                                    pts[i].Y * 2f / 3f + pts[i - 1].Y * 1f / 3f);
                                            break;
                                        case NubType.ControlPoint2:
                                            Qpts[i] = new PointF(pts[i - 1].X * 2f / 3f + pts[i + 1].X * 1f / 3f,
                                                    pts[i - 1].Y * 2f / 3f + pts[i + 1].Y * 1f / 3f);
                                            break;
                                    }
                                }
                                #endregion

                                using IPathGeometry quadGeometry = e.DeviceContext.Factory.CreateBeziersPathGeometry(Qpts);

                                if (partOfOperation)
                                {
                                    e.DeviceContext.DrawGeometry(quadGeometry, operationBrush, operationStrokeWidth);
                                }

                                if (isActive)
                                {
                                    e.DeviceContext.DrawGeometry(quadGeometry, activeBrush, activeStrokeWidth);
                                }

                                e.DeviceContext.DrawGeometry(quadGeometry, baseBrush, normalStrokeWidth);
                            }
                            break;
                    }

                    // Close Path(s)
                    if ((closedContiguous || closedIndividual) && pts.Length > 1 &&
                        !(j == -1 && ((this.MacroCircle.Checked && this.Elliptical.Checked) || (this.MacroRect.Checked && this.StraightLine.Checked))))
                    {
                        PointF pointA = closedIndividual ? pts[0] : pts[lastIndex];
                        PointF pointB = closedIndividual ? pts[lastIndex] : loopBack;

                        using ISolidColorBrush closedBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.DimGray);
                        e.DeviceContext.DrawLine(pointA, pointB, closedBrush, normalStrokeWidth, dashedStrokeStyle);

                        loopBack = pts[lastIndex];
                    }

                    previousEndPoint = pts[lastIndex];
                    previousClosed = closedIndividual || closedContiguous;
                }
            }
            #endregion

            #region Draw Nubs
            if (canvasPointCount > 0 && !ModifierKeys.HasFlag(Keys.Control))
            {
                const int radius = 3;

                pathType = this.PathTypeFromUI;

                PointF[] pts = new PointF[canvasPointCount];
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i].X = canvasSize.Width * this.canvasPoints[i].X;
                    pts[i].Y = canvasSize.Height * this.canvasPoints[i].Y;
                }

                int lastIndex = pts.Length - 1;

                using (ISolidColorBrush nubBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.Black))
                using (e.DeviceContext.UseTranslateTransform(0.5f, 0.5f))
                {
                    for (int i = 1; i < lastIndex; i++)
                    {
                        switch (pathType)
                        {
                            case PathType.Straight:
                                e.DeviceContext.DrawEllipse(pts[i], radius, nubBrush);
                                break;
                            case PathType.EllipticalArc:
                                if (i == 3)
                                {
                                    bool isMacro = this.MacroCircle.Checked;

                                    if (!isMacro || !isNewPath)
                                    {
                                        e.DeviceContext.DrawSquare(pts[1], radius, nubBrush);
                                        e.DeviceContext.FillEllipse(pts[3], radius, nubBrush);
                                        e.DeviceContext.FillSquare(pts[2], radius, nubBrush);

                                        PointF mid = PointFUtil.PointAverage(pts[0], pts[4]);
                                        e.DeviceContext.DrawLine(mid, pts[1], nubBrush);
                                        e.DeviceContext.DrawLine(mid, pts[2], nubBrush);
                                        e.DeviceContext.DrawLine(mid, pts[3], nubBrush);
                                    }

                                    if (isMacro || this.CloseTypeFromUI == CloseType.None)
                                    {
                                        e.DeviceContext.DrawLine(pts[0], pts[4], nubBrush);
                                    }
                                }
                                break;
                            case PathType.Quadratic:
                                if (CanvasUtil.GetNubType(i) == NubType.ControlPoint1)
                                {
                                    e.DeviceContext.DrawEllipse(pts[i], radius, nubBrush);
                                    e.DeviceContext.DrawLine(pts[i - 1], pts[i], nubBrush);
                                    if (i + 2 != lastIndex)
                                    {
                                        e.DeviceContext.DrawEllipse(pts[i + 2], radius, nubBrush);
                                    }
                                    e.DeviceContext.DrawLine(pts[i], pts[i + 2], nubBrush);
                                }
                                break;
                            case PathType.SmoothQuadratic:
                                if (CanvasUtil.GetNubType(i) == NubType.EndPoint)
                                {
                                    e.DeviceContext.DrawEllipse(pts[i], radius, nubBrush);
                                }
                                break;
                            case PathType.Cubic:
                            case PathType.SmoothCubic:
                                if (CanvasUtil.GetNubType(i) == NubType.ControlPoint1 && !this.MacroCubic.Checked)
                                {
                                    if (i != 1 || pathType == PathType.Cubic)
                                    {
                                        e.DeviceContext.DrawEllipse(pts[i], radius, nubBrush);
                                    }

                                    e.DeviceContext.DrawLine(pts[i - 1], pts[i], nubBrush);
                                    if (i + 2 != lastIndex)
                                    {
                                        e.DeviceContext.DrawEllipse(pts[i + 2], radius, nubBrush);
                                    }
                                    e.DeviceContext.DrawEllipse(pts[i + 1], radius, nubBrush);
                                    e.DeviceContext.DrawLine(pts[i + 1], pts[i + 2], nubBrush);
                                }
                                else if (CanvasUtil.GetNubType(i) == NubType.EndPoint && this.MacroCubic.Checked)
                                {
                                    e.DeviceContext.DrawEllipse(pts[i], radius, nubBrush);
                                }
                                break;
                        }
                    }
                }

                // Terminating Nubs
                const float terminatorStrokeWidth = 1.5f;
                Color pathColor = this.PathTypeFromUI.GetColor();
                using ISolidColorBrush terminatorNubBrush = e.DeviceContext.CreateSolidColorBrush(pathColor);

                LinkFlags linkFlags = (selectedIndex != InvalidPath) ? this.linkFlagsList[selectedIndex]
                    : IsNewPathLinked() ? LinkFlags.Up
                    : LinkFlags.None;

                Point2Float[] startTriangle =
                {
                    new PointF(pts[0].X + 4, pts[0].Y),
                    new PointF(pts[0].X - 4f, pts[0].Y - 5f),
                    new PointF(pts[0].X - 4f, pts[0].Y + 4f)
                };

                if (linkFlags.HasFlag(LinkFlags.Up))
                {
                    e.DeviceContext.FillPolygon(startTriangle, terminatorNubBrush);
                }
                else
                {
                    e.DeviceContext.DrawPolygon(startTriangle, terminatorNubBrush, terminatorStrokeWidth);
                }

                if (lastIndex != 0)
                {
                    const int terminatorRadius = 4;

                    if (linkFlags.HasFlag(LinkFlags.Down))
                    {
                        e.DeviceContext.FillSquare(pts[lastIndex], terminatorRadius, terminatorNubBrush);
                    }
                    else
                    {
                        e.DeviceContext.DrawSquare(pts[lastIndex], terminatorRadius, terminatorNubBrush, terminatorStrokeWidth);
                    }
                }
            }
            #endregion

            // render average point for when Scaling and Rotation
            if (this.drawAverage)
            {
                Point tmpPoint = CanvasCoordToPoint(this.averagePoint);

                using ISolidColorBrush averageBrush = e.DeviceContext.CreateSolidColorBrush(SrgbColors.Red);
                using (e.DeviceContext.UseTranslateTransform(0.5f, 0.5f))
                {
                    e.DeviceContext.DrawLine(tmpPoint.X - 3, tmpPoint.Y, tmpPoint.X + 3, tmpPoint.Y, averageBrush);
                    e.DeviceContext.DrawLine(tmpPoint.X, tmpPoint.Y - 3, tmpPoint.X, tmpPoint.Y + 3, averageBrush);
                }
            }

            if (!this.operationBox.IsEmpty)
            {
                const int gripWidth = 8;
                int opWidth = (this.operationBox.Width - gripWidth) / 3;
                Rectangle gripRect = new Rectangle(this.operationBox.Left, this.operationBox.Top, gripWidth, this.operationBox.Height);
                Rectangle scaleRect = new Rectangle(this.operationBox.Left + gripWidth, this.operationBox.Top, opWidth, this.operationBox.Height);
                Rectangle rotateRect = new Rectangle(this.operationBox.Left + gripWidth + opWidth, this.operationBox.Top, opWidth, this.operationBox.Height);
                Rectangle moveRect = new Rectangle(this.operationBox.Left + gripWidth + opWidth * 2, this.operationBox.Top, opWidth, this.operationBox.Height);

                const float activeOpacity = 1.0f;
                const float inactiveOpacity = 0.25f;
                float gripOpacity = (this.operation == Operation.None || this.operation == Operation.NoneRelocate) ? activeOpacity : inactiveOpacity;
                float scaleOpacity = (this.operation == Operation.None || this.operation == Operation.Scale) ? activeOpacity : inactiveOpacity;
                float rotateOpacity = (this.operation == Operation.None || this.operation == Operation.Rotate) ? activeOpacity : inactiveOpacity;
                float moveOpacity = (this.operation == Operation.None || this.operation == Operation.Move) ? activeOpacity : inactiveOpacity;

                using IDeviceImage gripImage = e.DeviceContext.CreateImageFromGdiBitmap(Properties.Resources.Grip);
                using OpacityEffect gripOpacityEffect = new OpacityEffect(e.DeviceContext, gripImage, gripOpacity);
                e.DeviceContext.DrawImage(gripOpacityEffect, gripRect);

                using IDeviceImage scaleImage = e.DeviceContext.CreateImageFromGdiBitmap(Properties.Resources.Resize);
                using OpacityEffect scaleOpacityEffect = new OpacityEffect(e.DeviceContext, scaleImage, scaleOpacity);
                e.DeviceContext.DrawImage(scaleOpacityEffect, scaleRect);

                using IDeviceImage rotateImage = e.DeviceContext.CreateImageFromGdiBitmap(Properties.Resources.Rotate);
                using OpacityEffect rotateOpacityEffect = new OpacityEffect(e.DeviceContext, rotateImage, rotateOpacity);
                e.DeviceContext.DrawImage(rotateOpacityEffect, rotateRect);

                using IDeviceImage moveImage = e.DeviceContext.CreateImageFromGdiBitmap(Properties.Resources.Move);
                using OpacityEffect moveOpacityEffect = new OpacityEffect(e.DeviceContext, moveImage, moveOpacity);
                e.DeviceContext.DrawImage(moveOpacityEffect, moveRect);
            }
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            if (selectedIndex != InvalidPath)
            {
                int bottomIndex = Math.Min(this.PathListBox.TopIndex + (this.PathListBox.Height / this.PathListBox.ItemHeight) - 1, this.PathListBox.Items.Count - 1);
                if (selectedIndex < this.PathListBox.TopIndex || selectedIndex > bottomIndex)
                {
                    this.PathListBox.TopIndex = selectedIndex;
                }
            }

            this.moveStart = PointToCanvasCoord(e.X, e.Y);

            this.clickedNub = InvalidNub;
            Rectangle hit = new Rectangle(e.X - 4, e.Y - 4, 9, 9);
            for (int i = 0; i < this.canvasPoints.Count; i++)
            {
                Point p = CanvasCoordToPoint(this.canvasPoints[i]);
                if (hit.Contains(p))
                {
                    this.clickedNub = i;
                    break;
                }
            }

            bool opBoxInit = false;

            switch (e.Button)
            {
                case MouseButtons.Left:
                    if (Control.ModifierKeys == Keys.Shift)
                    {
                        if (this.canvasPoints.Count != 0)
                        {
                            if (this.clickedNub != InvalidNub)
                            {
                                setUndo();
                                this.moveFlag = true;
                                this.canvas.Cursor = Cursors.SizeAll;
                            }
                        }
                        else if (this.PathListBox.Items.Count > 0)
                        {
                            setUndo();
                            this.moveFlag = true;
                            this.canvas.Cursor = Cursors.SizeAll;
                        }
                    }
                    else if (Control.ModifierKeys == Keys.Control)
                    {
                        if (this.clickedNub != InvalidNub && this.canvasPoints.Count > 1)
                        {
                            ShowOpBox(this.canvasPoints[this.clickedNub]);
                            opBoxInit = true;
                        }
                    }
                    else
                    {
                        if (this.operationBox.Contains(e.Location))
                        {
                            this.clickOffset = new Size(e.X - this.operationBox.X, e.Y - this.operationBox.Y);
                            this.averagePoint = (this.canvasPoints.Count > 1) ? this.canvasPoints.Average() : this.paths.Average();

                            const int gripWidth = 8;
                            int opWidth = (this.operationBox.Width - gripWidth) / 3;
                            Rectangle gripRect = new Rectangle(this.operationBox.Left, this.operationBox.Top, gripWidth, this.operationBox.Height);
                            Rectangle scaleRect = new Rectangle(this.operationBox.Left + gripWidth, this.operationBox.Top, opWidth, this.operationBox.Height);
                            Rectangle rotateRect = new Rectangle(this.operationBox.Left + gripWidth + opWidth, this.operationBox.Top, opWidth, this.operationBox.Height);
                            Rectangle moveRect = new Rectangle(this.operationBox.Left + gripWidth + opWidth * 2, this.operationBox.Top, opWidth, this.operationBox.Height);

                            if (gripRect.Contains(e.Location))
                            {
                                this.operation = Operation.NoneRelocate;
                            }
                            else if (scaleRect.Contains(e.Location))
                            {
                                this.initialDist = PointFUtil.Hypot(PointToCanvasCoord(e.X, e.Y), this.averagePoint);
                                this.operation = Operation.Scale;
                            }
                            else if (rotateRect.Contains(e.Location))
                            {
                                PointF clickCoord = PointToCanvasCoord(e.X, e.Y);
                                this.initialRads = PointFUtil.XYToRadians(clickCoord, this.averagePoint);
                                this.operation = Operation.Rotate;
                            }
                            else if (moveRect.Contains(e.Location))
                            {
                                PointF clickCoord = PointToCanvasCoord(e.X, e.Y);
                                PointF originCoord = this.moveStart;
                                this.initialDistSize = new SizeF(clickCoord.X - originCoord.X, clickCoord.Y - originCoord.Y);
                                this.operation = Operation.Move;
                            }

                            this.drawAverage = (this.operation == Operation.Scale || this.operation == Operation.Rotate);

                            if (this.operation != Operation.None && this.operation != Operation.NoneRelocate)
                            {
                                setUndo();
                            }
                        }
                        else if (this.clickedNub == InvalidNub)
                        {
                            Rectangle bhit = new Rectangle(e.X - 10, e.Y - 10, 20, 20);
                            int clickedPath = getNearestPath(bhit);
                            if (clickedPath != InvalidPath)
                            {
                                this.PathListBox.SelectedIndex = clickedPath;

                                for (int i = 0; i < this.canvasPoints.Count; i++)
                                {
                                    Point nub = CanvasCoordToPoint(this.canvasPoints[i]);
                                    if (bhit.Contains(nub))
                                    {
                                        StatusBarNubLocation(nub.X, nub.Y);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            setUndo();
                            Point nub = CanvasCoordToPoint(this.canvasPoints[this.clickedNub]);
                            StatusBarNubLocation(nub.X, nub.Y);
                        }
                    }

                    break;
                case MouseButtons.Right:  // process add or delete
                    PathType pathType = this.PathTypeFromUI;

                    if (this.clickedNub > InvalidNub)
                    {
                        #region delete
                        if (this.clickedNub == 0)
                        {
                            return; // don't delete first nub 
                        }

                        setUndo();

                        switch (pathType)
                        {
                            case PathType.Straight:
                                this.canvasPoints.RemoveAt(this.clickedNub);
                                break;
                            case PathType.EllipticalArc:
                                if (this.clickedNub != 4)
                                {
                                    return;
                                }

                                PointF hold = this.canvasPoints[this.canvasPoints.Count - 1];
                                this.canvasPoints.Clear();
                                this.canvasPoints.Add(hold);
                                break;
                            case PathType.Cubic:
                                if (CanvasUtil.GetNubType(this.clickedNub) != NubType.EndPoint)
                                {
                                    return;
                                }

                                this.canvasPoints.RemoveAt(this.clickedNub);
                                // remove control points
                                this.canvasPoints.RemoveAt(this.clickedNub - 1);
                                this.canvasPoints.RemoveAt(this.clickedNub - 2);
                                if (this.MacroCubic.Checked)
                                {
                                    CubicAdjust();
                                }

                                break;
                            case PathType.Quadratic:
                                if (CanvasUtil.GetNubType(this.clickedNub) != NubType.EndPoint)
                                {
                                    return;
                                }

                                this.canvasPoints.RemoveAt(this.clickedNub);
                                // remove control points
                                this.canvasPoints.RemoveAt(this.clickedNub - 1);
                                this.canvasPoints.RemoveAt(this.clickedNub - 2);
                                break;
                            case PathType.SmoothCubic:
                                if (CanvasUtil.GetNubType(this.clickedNub) != NubType.EndPoint)
                                {
                                    return;
                                }

                                this.canvasPoints.RemoveAt(this.clickedNub);
                                // remove control points
                                this.canvasPoints.RemoveAt(this.clickedNub - 1);
                                this.canvasPoints.RemoveAt(this.clickedNub - 2);
                                for (int i = 1; i < this.canvasPoints.Count; i++)
                                {
                                    if (CanvasUtil.GetNubType(i) == NubType.ControlPoint1 && i > 3)
                                    {
                                        this.canvasPoints[i] = PointFUtil.ReverseAverage(this.canvasPoints[i - 2], this.canvasPoints[i - 1]);
                                    }
                                }
                                break;
                            case PathType.SmoothQuadratic:
                                if (CanvasUtil.GetNubType(this.clickedNub) != NubType.EndPoint)
                                {
                                    return;
                                }

                                this.canvasPoints.RemoveAt(this.clickedNub);
                                // remove control points
                                this.canvasPoints.RemoveAt(this.clickedNub - 1);
                                this.canvasPoints.RemoveAt(this.clickedNub - 2);
                                for (int i = 1; i < this.canvasPoints.Count; i++)
                                {
                                    if (CanvasUtil.GetNubType(i) == NubType.ControlPoint1 && i > 3)
                                    {
                                        this.canvasPoints[i] = PointFUtil.ReverseAverage(this.canvasPoints[i - 3], this.canvasPoints[i - 1]);
                                        if (i < this.canvasPoints.Count - 1)
                                        {
                                            this.canvasPoints[i + 1] = this.canvasPoints[i];
                                        }
                                    }
                                }
                                break;
                        }
                        this.canvas.Refresh();
                        #endregion
                    }
                    else
                    {
                        #region add
                        int pointCount = this.canvasPoints.Count;
                        if (pointCount >= maxPoints)
                        {
                            MessageBox.Show($"Too many Nubs in Path (Max is {maxPoints})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (pathType == PathType.EllipticalArc && this.canvasPoints.Count > 2)
                        {
                            return;
                        }

                        setUndo();

                        int eX = e.X, eY = e.Y;
                        if (this.Snap.Checked)
                        {
                            eX = eX.ConstrainToInterval(10);
                            eY = eY.ConstrainToInterval(10);
                        }

                        StatusBarNubLocation(eX, eY);

                        PointF clickedPoint = PointToCanvasCoord(eX, eY);
                        if (pointCount == 0)// first point
                        {
                            this.canvasPoints.Add(clickedPoint);
                        }
                        else // not first point
                        {
                            switch (pathType)
                            {
                                case PathType.Straight:
                                    this.canvasPoints.Add(clickedPoint);

                                    break;
                                case PathType.EllipticalArc:
                                    PointF[] ellipsePts = new PointF[5];
                                    ellipsePts[0] = this.canvasPoints[pointCount - 1];
                                    ellipsePts[4] = clickedPoint;
                                    PointF mid = PointFUtil.PointAverage(ellipsePts[0], ellipsePts[4]);
                                    PointF mid2 = PointFUtil.ThirdPoint(ellipsePts[0], mid, true, 1f);
                                    ellipsePts[1] = PointFUtil.PointAverage(ellipsePts[0], mid2);
                                    ellipsePts[2] = PointFUtil.PointAverage(ellipsePts[4], mid2);
                                    ellipsePts[3] = PointFUtil.ThirdPoint(ellipsePts[0], mid, false, 1f);

                                    this.canvasPoints.Clear();
                                    this.canvasPoints.AddRange(ellipsePts);
                                    break;

                                case PathType.Cubic:
                                    PointF[] cubicPts = new PointF[3];
                                    cubicPts[2] = clickedPoint;

                                    if (this.MacroCubic.Checked)
                                    {
                                        this.canvasPoints.AddRange(cubicPts);
                                        CubicAdjust();
                                    }
                                    else
                                    {
                                        PointF mid4;
                                        if (pointCount > 1)
                                        {
                                            PointF mid3 = PointFUtil.ReverseAverage(this.canvasPoints[pointCount - 1], this.canvasPoints[pointCount - 2]);
                                            mid4 = PointFUtil.AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], cubicPts[2], mid3);
                                        }
                                        else
                                        {
                                            PointF mid3 = PointFUtil.PointAverage(this.canvasPoints[pointCount - 1], cubicPts[2]);
                                            mid4 = PointFUtil.ThirdPoint(this.canvasPoints[pointCount - 1], mid3, true, 1f);
                                        }
                                        cubicPts[0] = PointFUtil.PointAverage(this.canvasPoints[pointCount - 1], mid4);
                                        cubicPts[1] = PointFUtil.PointAverage(cubicPts[2], mid4);
                                        this.canvasPoints.AddRange(cubicPts);
                                    }

                                    break;
                                case PathType.Quadratic:
                                    PointF[] quadPts = new PointF[3];
                                    quadPts[2] = clickedPoint;
                                    PointF tmp;
                                    // add
                                    if (pointCount > 1)
                                    {
                                        tmp = PointFUtil.AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], quadPts[2], this.canvasPoints[pointCount - 2]);
                                    }
                                    else
                                    {
                                        // add end
                                        quadPts[1] = PointFUtil.ThirdPoint(this.canvasPoints[pointCount - 1], quadPts[2], true, .5f);
                                        quadPts[0] = PointFUtil.ThirdPoint(quadPts[2], this.canvasPoints[pointCount - 1], false, .5f);
                                        tmp = PointFUtil.PointAverage(quadPts[1], quadPts[0]);
                                    }
                                    quadPts[1] = tmp;
                                    quadPts[0] = tmp;
                                    this.canvasPoints.AddRange(quadPts);
                                    break;

                                case PathType.SmoothCubic:
                                    PointF[] sCubicPts = new PointF[3];
                                    sCubicPts[2] = clickedPoint;
                                    // startchange
                                    PointF mid6;
                                    if (pointCount > 1)
                                    {
                                        PointF mid5 = PointFUtil.ReverseAverage(this.canvasPoints[pointCount - 1], this.canvasPoints[pointCount - 2]);
                                        mid6 = PointFUtil.AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], sCubicPts[2], mid5);
                                    }
                                    else
                                    {
                                        PointF mid5 = PointFUtil.PointAverage(this.canvasPoints[pointCount - 1], sCubicPts[2]);
                                        mid6 = PointFUtil.ThirdPoint(this.canvasPoints[pointCount - 1], mid5, true, 1f);
                                    }

                                    sCubicPts[1] = PointFUtil.PointAverage(mid6, sCubicPts[2]);
                                    if (pointCount > 1)
                                    {
                                        sCubicPts[0] = PointFUtil.ReverseAverage(this.canvasPoints[pointCount - 2], this.canvasPoints[pointCount - 1]);
                                    }
                                    else
                                    {
                                        sCubicPts[0] = this.canvasPoints[0];
                                    }
                                    this.canvasPoints.AddRange(sCubicPts);

                                    break;
                                case PathType.SmoothQuadratic:
                                    PointF[] sQuadPts = new PointF[3];
                                    sQuadPts[2] = clickedPoint;
                                    if (pointCount > 1)
                                    {
                                        sQuadPts[0] = PointFUtil.ReverseAverage(this.canvasPoints[pointCount - 2], this.canvasPoints[pointCount - 1]);
                                        sQuadPts[1] = sQuadPts[0];
                                    }
                                    else
                                    {
                                        sQuadPts[0] = this.canvasPoints[0];
                                        sQuadPts[1] = this.canvasPoints[0];
                                    }
                                    this.canvasPoints.AddRange(sQuadPts);
                                    break;
                            }
                        }

                        this.canvas.Refresh();
                        #endregion
                    }

                    if (selectedIndex != InvalidPath && this.clickedNub != 0)
                    {
                        UpdateExistingPath();
                    }

                    break;
                case MouseButtons.Middle:
                    this.panFlag = true;

                    if (this.canvas.Width > this.viewport.ClientSize.Width && this.canvas.Height > this.viewport.ClientSize.Height)
                    {
                        this.canvas.Cursor = Cursors.NoMove2D;
                    }
                    else if (this.canvas.Width > this.viewport.ClientSize.Width)
                    {
                        this.canvas.Cursor = Cursors.NoMoveHoriz;
                    }
                    else if (this.canvas.Height > this.viewport.ClientSize.Height)
                    {
                        this.canvas.Cursor = Cursors.NoMoveVert;
                    }
                    else
                    {
                        this.panFlag = false;
                    }

                    break;
            }

            if (!opBoxInit && this.operation == Operation.None)
            {
                this.HideOpBox();
            }
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.clickedNub != InvalidNub || this.operation != Operation.None)
            {
                if (this.PathListBox.SelectedIndex != InvalidPath)
                {
                    UpdateExistingPath();
                }
                else
                {
                    RefreshPdnCanvas();
                }
            }

            this.panFlag = false;
            this.moveFlag = false;
            this.magneticallyLinked = false;
            this.operation = Operation.None;
            this.drawAverage = false;
            this.clickedNub = InvalidNub;
            this.canvas.Cursor = Cursors.Default;

            if (!this.operationBox.IsEmpty && !this.canvas.ClientRectangle.Contains(this.operationBox))
            {
                this.operationBox.X = Math.Clamp(this.operationBox.X, 0, this.canvas.ClientSize.Width - this.operationBox.Width);
                this.operationBox.Y = Math.Clamp(this.operationBox.Y, 0, this.canvas.ClientSize.Height - this.operationBox.Height);
            }

            this.canvas.Refresh();
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            StatusBarMouseLocation(e.X, e.Y);

            int eX = e.X,
                eY = e.Y;
            if (this.Snap.Checked)
            {
                eX = eX.ConstrainToInterval(10);
                eY = eY.ConstrainToInterval(10);
            }

            //if (!this.canvas.ClientRectangle.Contains(eX, eY))
            //{
            //    eX = eX.Clamp(this.canvas.ClientRectangle.Left, this.canvas.ClientRectangle.Right);
            //    eY = eY.Clamp(this.canvas.ClientRectangle.Top, this.canvas.ClientRectangle.Bottom);
            //}

            PointF mouseCoord = PointToCanvasCoord(eX, eY);

            if (e.Button == MouseButtons.Left)
            {
                int nubIndex = this.clickedNub;
                int nubCount = this.canvasPoints.Count;

                if (this.operation != Operation.None)
                {
                    this.operationBox.Location = new Point(e.X - this.clickOffset.Width, e.Y - this.clickOffset.Height);
                    int undoIndex = (this.historyIndex - 1 + historyMax) % historyMax;

                    switch (this.operation)
                    {
                        case Operation.NoneRelocate:
                            // Do Nothing
                            break;
                        case Operation.Scale:
                            float newDist = PointFUtil.Hypot(PointToCanvasCoord(e.X, e.Y), this.averagePoint);
                            float scale = newDist / this.initialDist;

                            if (nubCount == 0 && this.PathListBox.Items.Count > 0)
                            {
                                for (int k = 0; k < this.paths.Count; k++)
                                {
                                    PointF[] tmp = this.paths[k].Points;
                                    PointF[] originalPoints = this.undoPaths[undoIndex][k].Points;
                                    tmp.Scale(originalPoints, scale, this.averagePoint);
                                }
                            }
                            else if (nubCount > 1)
                            {
                                if (this.operationRange.Item1 != InvalidPath && this.operationRange.Item2 != InvalidPath)
                                {
                                    for (int k = this.operationRange.Item1; k <= this.operationRange.Item2; k++)
                                    {
                                        PointF[] tmp1 = this.paths[k].Points;
                                        PointF[] originalPoints1 = this.undoPaths[undoIndex][k].Points;
                                        tmp1.Scale(originalPoints1, scale, this.averagePoint);
                                    }
                                }

                                PointF[] tmp = this.canvasPoints.ToArray();
                                PointF[] originalPoints = this.undoCanvas[undoIndex].Points;
                                tmp.Scale(originalPoints, scale, this.averagePoint);

                                this.canvasPoints.Clear();
                                this.canvasPoints.AddRange(tmp);
                            }
                            break;
                        case Operation.Rotate:
                            double newRadians = PointFUtil.XYToRadians(PointToCanvasCoord(e.X, e.Y), this.averagePoint);
                            double radians = this.initialRads - newRadians;

                            if (ModifierKeys.HasFlag(Keys.Shift))
                            {
                                double constrainedDegrees = double.RadiansToDegrees(radians).ConstrainToInterval(15);
                                radians = double.DegreesToRadians(constrainedDegrees);
                            }

                            if (nubCount == 0 && this.PathListBox.Items.Count > 0)
                            {
                                for (int k = 0; k < this.paths.Count; k++)
                                {
                                    PointF[] tmp = this.paths[k].Points;
                                    PointF[] originalPoints = this.undoPaths[undoIndex][k].Points;
                                    tmp.Rotate(originalPoints, radians, this.averagePoint);
                                }
                            }
                            else if (nubCount > 1)
                            {
                                if (this.operationRange.Item1 != InvalidPath && this.operationRange.Item2 != InvalidPath)
                                {
                                    for (int k = this.operationRange.Item1; k <= this.operationRange.Item2; k++)
                                    {
                                        PointF[] tmp1 = this.paths[k].Points;
                                        PointF[] originalPoints1 = this.undoPaths[undoIndex][k].Points;
                                        tmp1.Rotate(originalPoints1, radians, this.averagePoint);
                                    }
                                }

                                PointF[] tmp = this.canvasPoints.ToArray();
                                PointF[] originalPoints = this.undoCanvas[undoIndex].Points;
                                tmp.Rotate(originalPoints, radians, this.averagePoint);

                                this.canvasPoints.Clear();
                                this.canvasPoints.AddRange(tmp);
                            }
                            break;
                        case Operation.Move:
                            PointF rawMouseCoord = PointToCanvasCoord(e.X, e.Y);
                            PointF newCoord = new PointF(rawMouseCoord.X - initialDistSize.Width, rawMouseCoord.Y - initialDistSize.Height);
                            if (this.Snap.Checked)
                            {
                                PointF snapPoint = CanvasCoordToPoint(newCoord).ConstrainToInterval(10);
                                newCoord = PointToCanvasCoord(snapPoint.X, snapPoint.Y);
                            }

                            if (nubCount == 0 && this.PathListBox.Items.Count > 0)
                            {
                                for (int k = 0; k < this.paths.Count; k++)
                                {
                                    PointF[] pathPoints = this.paths[k].Points;
                                    for (int j = 0; j < pathPoints.Length; j++)
                                    {
                                        pathPoints[j] = PointFUtil.MovePoint(this.moveStart, newCoord, pathPoints[j]);
                                    }
                                }

                                this.moveStart = mouseCoord;
                            }
                            else if (nubCount > 0)
                            {
                                if (this.operationRange.Item1 != InvalidPath && this.operationRange.Item2 != InvalidPath)
                                {
                                    for (int k = this.operationRange.Item1; k <= this.operationRange.Item2; k++)
                                    {
                                        PointF[] pathPoints = this.paths[k].Points;
                                        for (int j = 0; j < pathPoints.Length; j++)
                                        {
                                            pathPoints[j] = PointFUtil.MovePoint(this.moveStart, newCoord, pathPoints[j]);
                                        }
                                    }
                                }

                                for (int j = 0; j < nubCount; j++)
                                {
                                    this.canvasPoints[j] = PointFUtil.MovePoint(this.moveStart, newCoord, this.canvasPoints[j]);
                                }

                                this.moveStart = mouseCoord;
                            }
                            break;
                    }
                }
                else if (this.moveFlag && (Control.ModifierKeys & Keys.Shift) == Keys.Shift) // left shift move line or path
                {
                    if (nubCount != 0 && nubIndex > InvalidNub && nubIndex < nubCount)
                    {
                        StatusBarNubLocation(eX, eY);

                        PointF oldPoint = this.canvasPoints[nubIndex];

                        for (int j = 0; j < nubCount; j++)
                        {
                            this.canvasPoints[j] = PointFUtil.MovePoint(oldPoint, mouseCoord, this.canvasPoints[j]);
                        }
                    }
                    else if (nubCount == 0 && this.PathListBox.Items.Count > 0)
                    {
                        StatusBarNubLocation(eX, eY);

                        for (int k = 0; k < this.paths.Count; k++)
                        {
                            PointF[] pathPoints = this.paths[k].Points;
                            for (int j = 0; j < pathPoints.Length; j++)
                            {
                                pathPoints[j] = PointFUtil.MovePoint(this.moveStart, mouseCoord, pathPoints[j]);
                            }
                        }
                        this.moveStart = mouseCoord;
                    }
                }
                else if (nubCount > 0 && nubIndex > InvalidNub && nubIndex < nubCount) // no shift movepoint
                {
                    bool isAltPressed = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;

                    bool moveLinkedLast = false;
                    bool moveLinkedPrevious = false;
                    bool moveLinkedNext = false;

                    // Nexus Nubs
                    if (!isAltPressed && !this.magneticallyLinked && nubCount > 1)
                    {
                        int selectedIndex = this.PathListBox.SelectedIndex;

                        if (selectedIndex == InvalidPath)
                        {
                            moveLinkedLast =
                                nubIndex == 0 &&
                                IsNewPathLinked();
                        }
                        else if (nubIndex == 0)
                        {
                            moveLinkedPrevious =
                                selectedIndex > 0 &&
                                selectedIndex < this.paths.Count &&
                                this.paths[selectedIndex].CloseType != CloseType.Individual &&
                                this.paths[selectedIndex - 1].CloseType == CloseType.None &&
                                this.canvasPoints[nubIndex] == this.paths[selectedIndex - 1].Points.Last();
                        }
                        else if (nubIndex == nubCount - 1)
                        {
                            moveLinkedNext =
                                selectedIndex + 1 < this.paths.Count &&
                                this.paths[selectedIndex].CloseType == CloseType.None &&
                                this.paths[selectedIndex + 1].CloseType != CloseType.Individual &&
                                this.canvasPoints[nubIndex] == this.paths[selectedIndex + 1].Points[0];
                        }
                    }

                    // Magnetic Nubs
                    if (!isAltPressed && !moveLinkedLast && !moveLinkedPrevious && !moveLinkedNext &&
                        (nubIndex == 0 || nubIndex == nubCount - 1))
                    {
                        Rectangle bhit = new Rectangle(e.X - 10, e.Y - 10, 20, 20);
                        int nearestPath = getNearestPath(bhit);
                        if (nearestPath != InvalidPath)
                        {
                            PointF otherTerminatingNub = nubIndex == 0
                                ? this.paths[nearestPath].Points[this.paths[nearestPath].Points.Length - 1]
                                : this.paths[nearestPath].Points[0];

                            Point nubPoint = CanvasCoordToPoint(otherTerminatingNub);
                            if (bhit.Contains(nubPoint))
                            {
                                eX = nubPoint.X;
                                eY = nubPoint.Y;
                                mouseCoord = otherTerminatingNub;

                                this.magneticallyLinked = true;
                            }
                        }
                    }

                    StatusBarNubLocation(eX, eY);

                    PointF oldPoint = this.canvasPoints[nubIndex];

                    NubType nubType = CanvasUtil.GetNubType(nubIndex);
                    PathType pathType = this.PathTypeFromUI;

                    switch (pathType)
                    {
                        case PathType.Straight:
                        case PathType.EllipticalArc:
                            this.canvasPoints[nubIndex] = mouseCoord;
                            break;
                        case PathType.Cubic:
                            switch (nubType)
                            {
                                case NubType.StartPoint:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if (nubCount > 1)
                                    {
                                        this.canvasPoints[nubIndex + 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                    }
                                    break;
                                case NubType.ControlPoint1:
                                case NubType.ControlPoint2:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    break;
                                case NubType.EndPoint:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    this.canvasPoints[nubIndex - 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                    if ((nubIndex + 1) < nubCount)
                                    {
                                        this.canvasPoints[nubIndex + 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                    }
                                    break;
                            }

                            if (this.MacroCubic.Checked)
                            {
                                CubicAdjust();
                            }

                            break;
                        case PathType.Quadratic:
                            switch (nubType)
                            {
                                case NubType.StartPoint:
                                    if (isAltPressed && nubCount != 1)
                                    {
                                        PointF rtmp = PointFUtil.ReverseAverage(this.canvasPoints[nubIndex + 1], this.canvasPoints[nubIndex]);
                                        this.canvasPoints[nubIndex] = PointFUtil.OnLinePoint(this.canvasPoints[nubIndex + 1], rtmp, mouseCoord);
                                    }
                                    else
                                    {
                                        this.canvasPoints[nubIndex] = mouseCoord;
                                    }
                                    break;
                                case NubType.ControlPoint1:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if ((nubIndex + 1) < nubCount)
                                    {
                                        this.canvasPoints[nubIndex + 1] = this.canvasPoints[nubIndex];
                                    }
                                    break;
                                case NubType.ControlPoint2:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if ((nubIndex - 1) > 0)
                                    {
                                        this.canvasPoints[nubIndex - 1] = this.canvasPoints[nubIndex];
                                    }
                                    break;
                                case NubType.EndPoint:
                                    if (isAltPressed)
                                    {
                                        //online
                                        PointF point = (nubIndex == nubCount - 1)
                                            ? PointFUtil.ReverseAverage(this.canvasPoints[nubIndex - 1], this.canvasPoints[nubIndex])
                                            : this.canvasPoints[nubIndex + 1];

                                        this.canvasPoints[nubIndex] = PointFUtil.OnLinePoint(this.canvasPoints[nubIndex - 1], point, mouseCoord);
                                    }
                                    else
                                    {
                                        this.canvasPoints[nubIndex] = mouseCoord;
                                    }
                                    break;
                            }

                            break;
                        case PathType.SmoothCubic:
                            switch (nubType)
                            {
                                case NubType.StartPoint:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if (nubCount > 1)
                                    {
                                        this.canvasPoints[nubIndex + 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                        this.canvasPoints[1] = this.canvasPoints[0];
                                    }
                                    break;
                                case NubType.ControlPoint1:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if (nubIndex > 1)
                                    {
                                        this.canvasPoints[nubIndex - 2] = PointFUtil.ReverseAverage(this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                    }
                                    else
                                    {
                                        this.canvasPoints[1] = this.canvasPoints[0];
                                    }
                                    break;
                                case NubType.ControlPoint2:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    if (nubIndex < nubCount - 2)
                                    {
                                        this.canvasPoints[nubIndex + 2] = PointFUtil.ReverseAverage(this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                    }
                                    break;
                                case NubType.EndPoint:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    this.canvasPoints[nubIndex - 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                    if ((nubIndex + 1) < nubCount)
                                    {
                                        this.canvasPoints[nubIndex + 1] = PointFUtil.MovePoint(oldPoint, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                    }
                                    break;
                            }

                            break;
                        case PathType.SmoothQuadratic:
                            switch (nubType)
                            {
                                case NubType.StartPoint:
                                    this.canvasPoints[0] = mouseCoord;
                                    if (nubCount > 1)
                                    {
                                        this.canvasPoints[1] = mouseCoord;
                                    }
                                    break;
                                case NubType.EndPoint:
                                    this.canvasPoints[nubIndex] = mouseCoord;
                                    break;
                            }

                            for (int j = 0; j < nubCount; j++)
                            {
                                if (CanvasUtil.GetNubType(j) == NubType.ControlPoint1 && j > 1)
                                {
                                    this.canvasPoints[j] = PointFUtil.ReverseAverage(this.canvasPoints[j - 3], this.canvasPoints[j - 1]);
                                    this.canvasPoints[j + 1] = this.canvasPoints[j];
                                }
                            }

                            break;
                    }

                    if (moveLinkedLast)
                    {
                        int lastPathIndex = this.PathListBox.Items.Count - 1;
                        int lastPointIndex = this.paths[lastPathIndex].Points.Length - 1;
                        this.paths[lastPathIndex].Points[lastPointIndex] = this.canvasPoints[nubIndex];
                    }
                    else if (moveLinkedPrevious)
                    {
                        int previousPathIndex = this.PathListBox.SelectedIndex - 1;
                        int lastPointIndex = this.paths[previousPathIndex].Points.Length - 1;
                        this.paths[previousPathIndex].Points[lastPointIndex] = this.canvasPoints[nubIndex];
                    }
                    else if (moveLinkedNext)
                    {
                        this.paths[this.PathListBox.SelectedIndex + 1].Points[0] = this.canvasPoints[nubIndex];
                    }
                    else
                    {
                        this.RebuildLinkFlagsCache();
                    }
                }

                this.canvas.Refresh();
            }
            else if (e.Button == MouseButtons.Middle && this.panFlag)
            {
                int mpx = (int)(mouseCoord.X * 100);
                int msx = (int)(this.moveStart.X * 100);
                int mpy = (int)(mouseCoord.Y * 100);
                int msy = (int)(this.moveStart.Y * 100);
                int tx = 10 * (mpx - msx);
                int ty = 10 * (mpy - msy);

                int maxMoveX = this.canvas.Width - this.viewport.ClientSize.Width;
                int maxMoveY = this.canvas.Height - this.viewport.ClientSize.Height;

                Point pannedCanvasPos = this.canvas.Location;
                if (this.canvas.Width > this.viewport.ClientSize.Width)
                {
                    pannedCanvasPos.X = (this.canvas.Location.X + tx < -maxMoveX) ? -maxMoveX : (this.canvas.Location.X + tx > 0) ? 0 : this.canvas.Location.X + tx;
                }

                if (this.canvas.Height > this.viewport.ClientSize.Height)
                {
                    pannedCanvasPos.Y = (this.canvas.Location.Y + ty < -maxMoveY) ? -maxMoveY : (this.canvas.Location.Y + ty > 0) ? 0 : this.canvas.Location.Y + ty;
                }

                this.canvas.Location = pannedCanvasPos;

                UpdateScrollBars();
            }
        }

        private void HideOpBox()
        {
            this.operationBox = Rectangle.Empty;
            this.operationRange = new Tuple<int, int>(InvalidPath, InvalidPath);
        }

        private void ShowOpBox()
        {
            if (this.PathListBox.SelectedIndex == InvalidPath && canvasPoints.Count == 1)
            {
                canvasPoints.Clear();
            }

            RectangleF bounds = (canvasPoints.Count > 1)
                ? canvasPoints.Bounds()
                : paths.Bounds();

            PointF coord = new PointF(bounds.Right, bounds.Bottom);

            ShowOpBox(coord);
        }

        private void ShowOpBox(PointF coord)
        {
            Rectangle opBoxRect = new Rectangle(CanvasCoordToPoint(coord), new Size(68, 20));
            opBoxRect.X += 5;
            opBoxRect.Y += 5;

            if (!this.canvas.ClientRectangle.Contains(opBoxRect))
            {
                opBoxRect.X = Math.Clamp(opBoxRect.X, 0, this.canvas.ClientSize.Width - opBoxRect.Width);
                opBoxRect.Y = Math.Clamp(opBoxRect.Y, 0, this.canvas.ClientSize.Height - opBoxRect.Height);
            }

            this.operationBox = opBoxRect;

            this.operationRange = new Tuple<int, int>(InvalidPath, InvalidPath);

            if (this.canvasPoints.Count > 1)
            {
                int selectedIndex = this.PathListBox.SelectedIndex;

                if (selectedIndex == InvalidPath)
                {
                    if (IsNewPathLinked())
                    {
                        int rangeEnd = this.PathListBox.Items.Count - 1;
                        int rangeStart = rangeEnd;

                        while (this.linkFlagsList[rangeStart].HasFlag(LinkFlags.Up))
                        {
                            rangeStart--;
                        }

                        operationRange = new Tuple<int, int>(rangeStart, rangeEnd);
                    }
                }
                else
                {
                    int rangeStart = selectedIndex;
                    int rangeEnd = selectedIndex;

                    while (this.linkFlagsList[rangeStart].HasFlag(LinkFlags.Up))
                    {
                        rangeStart--;
                    }

                    while (this.linkFlagsList[rangeEnd].HasFlag(LinkFlags.Down))
                    {
                        rangeEnd++;
                    }

                    operationRange = new Tuple<int, int>(rangeStart, rangeEnd);
                }
            }

            this.canvas.Invalidate();
        }

        private enum Operation
        {
            None,
            NoneRelocate,
            Scale,
            Rotate,
            Move
        }
        #endregion

        #region Misc Helper functions
        private void UpdateExistingPath()
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            this.paths[selectedIndex] = new PathData(this.PathTypeFromUI, this.canvasPoints, this.CloseTypeFromUI, this.ArcOptionsFromUI, this.paths[selectedIndex].Alias);
            this.PathListBox.Items[selectedIndex] = this.PathTypeFromUI.GetName();

            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();

            RefreshPdnCanvas();
        }

        private void AddNewPath(bool deSelected = false)
        {
            if (this.canvasPoints.Count <= 1)
            {
                return;
            }

            setUndo(deSelected);

            this.HideOpBox();

            PathType pathType = this.PathTypeFromUI;
            if (this.MacroCircle.Checked && pathType == PathType.EllipticalArc)
            {
                if (this.canvasPoints.Count < 5)
                {
                    return;
                }

                string arcName = PathType.EllipticalArc.GetName();

                PointF mid = PointFUtil.PointAverage(this.canvasPoints[0], this.canvasPoints[4]);
                this.canvasPoints[1] = this.canvasPoints[0];
                this.canvasPoints[2] = this.canvasPoints[4];
                this.canvasPoints[3] = mid;
                this.paths.Add(new PathData(PathType.EllipticalArc, this.canvasPoints, CloseType.None, this.ArcOptionsFromUI));
                this.PathListBox.Items.Add(arcName);

                PointF[] tmp = new PointF[]
                {
                    this.canvasPoints[4],
                    this.canvasPoints[4],
                    this.canvasPoints[0],
                    this.canvasPoints[3],
                    this.canvasPoints[0]
                };

                this.paths.Add(new PathData(PathType.EllipticalArc, tmp, CloseType.Contiguous, this.ArcOptionsFromUI));
                this.PathListBox.Items.Add(arcName);
            }
            else if (this.MacroRect.Checked && pathType == PathType.Straight)
            {
                for (int i = 1; i < this.canvasPoints.Count; i++)
                {
                    PointF[] tmp = new PointF[]
                    {
                        new PointF(this.canvasPoints[i - 1].X, this.canvasPoints[i - 1].Y),
                        new PointF(this.canvasPoints[i].X, this.canvasPoints[i - 1].Y),
                        new PointF(this.canvasPoints[i].X, this.canvasPoints[i].Y),
                        new PointF(this.canvasPoints[i - 1].X, this.canvasPoints[i].Y),
                        new PointF(this.canvasPoints[i - 1].X, this.canvasPoints[i - 1].Y)
                    };

                    this.paths.Add(new PathData(PathType.Straight, tmp, CloseType.None, ArcOptions.None));
                    this.PathListBox.Items.Add(PathType.Straight.GetName());
                }
            }
            else
            {
                this.paths.Add(new PathData(pathType, this.canvasPoints, this.CloseTypeFromUI, this.ArcOptionsFromUI));
                this.PathListBox.Items.Add(pathType.GetName());
            }

            if (this.LinkedPaths.Checked)
            {
                PointF hold = this.canvasPoints[this.canvasPoints.Count - 1];
                this.canvasPoints.Clear();
                this.canvasPoints.Add(hold);
            }
            else
            {
                this.canvasPoints.Clear();
            }

            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();
            this.PathListBox.Focus();
            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private void Deselect()
        {
            bool isNewPath = this.PathListBox.SelectedIndex == InvalidPath;
            if (isNewPath && this.canvasPoints.Count > 1)
            {
                setUndo();
            }

            this.HideOpBox();
            this.drawAverage = false;

            if (this.canvasPoints.Count == 0)
            {
                // No-op
            }
            else if (this.LinkedPaths.Checked)
            {
                int holdIndex = isNewPath ? 0 : this.canvasPoints.Count - 1;
                PointF hold = this.canvasPoints[holdIndex];
                this.canvasPoints.Clear();
                this.canvasPoints.Add(hold);
            }
            else
            {
                this.canvasPoints.Clear();
            }

            this.PathListBox.SelectedIndex = InvalidPath;
            this.PathListBox.Focus();
            this.canvas.Refresh();
        }

        private void CubicAdjust()
        {
            PointF[] knots = new PointF[(int)Math.Ceiling(this.canvasPoints.Count / 3f)];
            for (int ri = 0; ri < knots.Length; ri++)
            {
                knots[ri] = this.canvasPoints[ri * 3];
            }

            int n = knots.Length - 1;

            if (n == 1)
            {
                PointF mid3 = new PointF
                {
                    X = (2 * knots[0].X + knots[1].X) / 3,
                    Y = (2 * knots[0].Y + knots[1].Y) / 3
                };

                PointF mid4 = new PointF
                {
                    X = 2 * mid3.X - knots[0].X,
                    Y = 2 * mid3.Y - knots[0].Y
                };

                this.canvasPoints[1] = mid3;
                this.canvasPoints[2] = mid4;
            }
            else if (n > 1)
            {
                PointF[] rhs = new PointF[n];
                for (int ri = 1; ri < n - 1; ri++)
                {
                    rhs[ri].X = 4f * knots[ri].X + 2f * knots[ri + 1].X;
                    rhs[ri].Y = 4f * knots[ri].Y + 2f * knots[ri + 1].Y;
                }
                rhs[0].X = knots[0].X + 2f * knots[1].X;
                rhs[0].Y = knots[0].Y + 2f * knots[1].Y;
                rhs[n - 1].X = (8f * knots[n - 1].X + knots[n].X) / 2f;
                rhs[n - 1].Y = (8f * knots[n - 1].Y + knots[n].Y) / 2f;

                IReadOnlyList<PointF> xy = PointFUtil.GetFirstControlPoints(rhs);

                for (int ri = 0; ri < n; ri++)
                {
                    this.canvasPoints[ri * 3 + 1] = xy[ri];
                    if (ri < (n - 1))
                    {
                        this.canvasPoints[ri * 3 + 2] = new PointF
                        {
                            X = 2f * knots[1 + ri].X - xy[1 + ri].X,
                            Y = 2f * knots[1 + ri].Y - xy[1 + ri].Y
                        };
                    }
                    else
                    {
                        this.canvasPoints[ri * 3 + 2] = new PointF
                        {
                            X = (knots[n].X + xy[n - 1].X) / 2,
                            Y = (knots[n].Y + xy[n - 1].Y) / 2
                        };
                    }
                }
            }
        }

        private void SetUiForPath(PathData pathData)
        {
            PathType pathType = pathData.PathType;
            CloseType closeType = pathData.CloseType;
            ArcOptions arcOptions = pathData.ArcOptions;

            SuspendLayout();
            this.MacroCubic.Checked = false;
            this.MacroCircle.Checked = false;
            this.MacroRect.Checked = false;
            if (pathType != this.activeType)
            {
                this.activeType = pathType;
                PathTypeToggle();
            }

            this.ClosePath.Checked = closeType == CloseType.Individual;
            this.ClosePath.Image = (this.ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            this.CloseContPaths.Checked = closeType == CloseType.Contiguous;
            this.CloseContPaths.Image = (this.CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;

            if (pathType == PathType.EllipticalArc)
            {
                this.Arc.CheckState = arcOptions.HasFlag(ArcOptions.LargeArc) ? CheckState.Checked : CheckState.Indeterminate;
                this.Arc.Image = (this.Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcLarge : Properties.Resources.ArcSmall;

                this.Sweep.CheckState = arcOptions.HasFlag(ArcOptions.PositiveSweep) ? CheckState.Checked : CheckState.Indeterminate;
                this.Sweep.Image = (this.Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;
            }
            ResumeLayout();

            this.canvasPoints.Clear();
            this.canvasPoints.AddRange(pathData.Points);

            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private int getNearestPath(Rectangle hit)
        {
            if (this.PathListBox.Items.Count == 0)
            {
                return InvalidPath;
            }

            for (int i = 0; i < this.PathListBox.Items.Count; i++)
            {
                PathType pathType = this.paths[i].PathType;
                PointF[] tmp;

                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddLines(this.paths[i].Points);
                    gp.Flatten(null, .1f);

                    tmp = gp.PathPoints;
                }

                for (int j = 0; j < tmp.Length; j++)
                {
                    if (CanvasUtil.IsControlNub(j, pathType))
                    {
                        continue;
                    }

                    Point p = CanvasCoordToPoint(tmp[j]);
                    if (hit.Contains(p))
                    {
                        return i;
                    }
                }
            }

            return InvalidPath;
        }

        private void StatusBarMouseLocation(int x, int y)
        {
            this.statusLabelMousePos.Text = GetPointForStatusBar(x, y);
            this.statusStrip1.Refresh();
        }

        private void StatusBarNubLocation(int x, int y)
        {
            this.statusLabelNubPos.Text = GetPointForStatusBar(x, y);
            this.statusStrip1.Refresh();
        }

        private string GetPointForStatusBar(int x, int y)
        {
            int zoomFactor = this.canvas.Width / this.canvasBaseSize;
            return $"{Math.Round(x / (float)zoomFactor / dpiScale)}, {Math.Round(y / (float)zoomFactor / dpiScale)}";
        }

        private string GenerateStreamGeometry()
        {
            return StreamGeometryUtil.GenerateStreamGeometry(this.paths, this.solidFillCheckBox.Checked, CanvasUtil.CanvasSize, CanvasUtil.CanvasSize);
        }

        private string GeneratePathGeometry()
        {
            return PathGeometryUtil.GeneratePathGeometry(this.paths, this.linkFlagsList, CanvasUtil.CanvasSize, CanvasUtil.CanvasSize);
        }

        private string GenerateSvg()
        {
            return string.Format(
                ExportConsts.SvgFile,
                StreamGeometryUtil.GenerateStreamGeometry(this.paths, false, CanvasUtil.CanvasSize, CanvasUtil.CanvasSize));
        }

        private void LoadStreamGeometry(string streamGeometry)
        {
            IReadOnlyCollection<PathData> paths = PathDataCollection.FromStreamGeometry(streamGeometry).Paths;

            if (paths.Count == 0)
            {
                MessageBox.Show("No Paths found.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            setUndo();
            ZoomToFactor(1);

            this.paths.AddRange(paths);
            this.PathListBox.Items.AddRange(paths.Select(path => path.PathType.GetName()).ToArray());

            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();

            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private PointF PointToCanvasCoord(float x, float y)
        {
            return CanvasUtil.PointToCanvasCoord(x, y, this.canvas.ClientSize.Width, this.canvas.ClientSize.Height);
        }

        private Point CanvasCoordToPoint(PointF coord)
        {
            return CanvasUtil.CanvasCoordToPoint(coord.X, coord.Y, this.canvas.ClientSize.Width, this.canvas.ClientSize.Height);
        }

        private Point CanvasCoordToPoint(float x, float y)
        {
            return CanvasUtil.CanvasCoordToPoint(x, y, this.canvas.ClientSize.Width, this.canvas.ClientSize.Height);
        }

        private void ClearAllPaths()
        {
            this.canvasPoints.Clear();
            this.statusLabelNubsUsed.Text = $"{this.canvasPoints.Count}/{maxPoints} Nubs used";
            this.statusLabelNubPos.Text = "0, 0";

            this.paths.Clear();
            this.PathListBox.Items.Clear();
            this.statusLabelPathsUsed.Text = $"{this.PathListBox.Items.Count} Paths";

            this.canvas.Refresh();
        }

        private bool InView()
        {
            if (this.canvasPoints.Any(pt => pt.X > 1.5f || pt.Y > 1.5f))
            {
                return false;
            }

            foreach (PathData pathData in this.paths)
            {
                if (pathData.Points.Any(pt => pt.X > 1.5f || pt.Y > 1.5f))
                {
                    return false;
                }
            }

            return true;
        }

        private void LoadProjectFile(string projectFile)
        {
            PathDataCollection collection = null;

            XmlSerializer pDataSerializer = new XmlSerializer(typeof(ArrayListEx), new Type[] { typeof(PData) });
            XmlSerializer pathDataSerializer = new XmlSerializer(typeof(PathDataCollection));

            try
            {
                using (FileStream fileStream = File.OpenRead(projectFile))
                using (XmlTextReader xmlReader = new XmlTextReader(fileStream))
                {
                    if (pathDataSerializer.CanDeserialize(xmlReader))
                    {
                        collection = (PathDataCollection)pathDataSerializer.Deserialize(xmlReader);
                    }
                    else if (pDataSerializer.CanDeserialize(xmlReader))
                    {
                        IEnumerable<PData> paths = ((ArrayListEx)pDataSerializer.Deserialize(xmlReader)).OfType<PData>();
                        PData last = paths.Last();

                        collection = new PathDataCollection(
                            paths.Select(pData => pData.ToPathData()).ToList(),
                            last.SolidFill,
                            last.Meta);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Incorrect Format.\r\n" + ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (collection.IsEmpty)
            {
                MessageBox.Show("No Project data was found in the file.", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ClearAllPaths();

            this.FigureName.Text = collection.ShapeName;
            this.solidFillCheckBox.Checked = collection.SolidFill;

            foreach (PathData path in collection.Paths)
            {
                this.paths.Add(path);
                this.PathListBox.Items.Add(path.PathType.GetName());
            }

            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();

            ZoomToFactor(1);
            resetHistory();
            this.canvas.Refresh();
            RefreshPdnCanvas();
            AddToRecents(projectFile);
        }

        private bool IsNewPathLinked()
        {
            int lastPathIndex = this.PathListBox.Items.Count - 1;

            return this.paths.Count > 0 &&
                this.CloseTypeFromUI != CloseType.Individual &&
                this.paths[lastPathIndex].CloseType == CloseType.None &&
                this.paths[lastPathIndex].Points.Last() == this.canvasPoints[0];
        }

        private static string GetSanitizedShapeName(string shapeName)
        {
            string sanitizedName = Regex.Replace(shapeName.Trim(), "[\"<>]", string.Empty);

            return string.IsNullOrWhiteSpace(sanitizedName) ? "Untitled" : sanitizedName;
        }
        #endregion

        #region Path List functions
        private void PathListBox_DoubleClick(object sender, EventArgs e)
        {
            if (this.PathListBox.Items.Count == 0 || this.PathListBox.SelectedItem == null)
            {
                return;
            }

            string s = Microsoft.VisualBasic.Interaction.InputBox("Please enter a name for this path.", "Path Name", this.PathListBox.SelectedItem.ToString(), -1, -1).Trim();
            if (s.Length > 0)
            {
                this.paths[this.PathListBox.SelectedIndex].Alias = s;
            }
        }

        private void PathListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            if (selectedIndex == this.oldPathListBoxIndex)
            {
                return;
            }

            this.oldPathListBoxIndex = selectedIndex;

            if (this.isNewPath && this.canvasPoints.Count > 1)
            {
                AddNewPath(true);
            }

            this.isNewPath = false;

            if (selectedIndex == InvalidPath)
            {
                return;
            }

            if (selectedIndex < this.paths.Count)
            {
                SetUiForPath(this.paths[selectedIndex]);
            }

            if (this.operationBox != Rectangle.Empty)
            {
                ShowOpBox();
            }
        }

        private void LineListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            int itemIndex = e.Index;

            if (itemIndex >= 0 && itemIndex < this.PathListBox.Items.Count)
            {
                PathData itemPath = this.paths[itemIndex];

                Color backColor = isItemSelected ? itemPath.PathType.GetLightColor() : Color.White;

                if (isItemSelected)
                {
                    using (SolidBrush backgroundColorBrush = new SolidBrush(backColor))
                    {
                        e.Graphics.FillRectangle(backgroundColorBrush, e.Bounds);
                    }
                }

                string itemText = (itemPath.Alias.Length > 0)
                    ? itemPath.Alias
                    : this.PathListBox.Items[itemIndex].ToString();

                using (StringFormat vCenter = new StringFormat { LineAlignment = StringAlignment.Center })
                using (SolidBrush itemTextColorBrush = new SolidBrush(itemPath.PathType.GetColor()))
                {
                    e.Graphics.DrawString(itemText, e.Font, itemTextColorBrush, e.Bounds, vCenter);
                }

                const int padding = 4;
                int linkIndicatorSize = e.Bounds.Height - padding * 2;
                if (linkIndicatorSize % 2 == 0)
                {
                    // should be an odd number
                    linkIndicatorSize--;
                }

                Rectangle linkIndicatorRect = new Rectangle(e.Bounds.Right - padding - linkIndicatorSize - 2, e.Bounds.Top + padding, linkIndicatorSize, linkIndicatorSize);

                Rectangle gradientRect = Rectangle.FromLTRB(
                    linkIndicatorRect.Left - 25,
                    e.Bounds.Top,
                    linkIndicatorRect.Left,
                    e.Bounds.Bottom);

                using (LinearGradientBrush gradientBrush = new LinearGradientBrush(new Point(gradientRect.Left - 1, gradientRect.Top), new Point(gradientRect.Right + 1, gradientRect.Top), Color.Transparent, backColor))
                {
                    e.Graphics.FillRectangle(gradientBrush, gradientRect);
                }

                using (SolidBrush backBrush = new SolidBrush(backColor))
                {
                    e.Graphics.FillRectangle(backBrush, Rectangle.FromLTRB(linkIndicatorRect.Left - 3, e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom));
                }

                e.Graphics.FillRectangle(Brushes.MidnightBlue, linkIndicatorRect);

                if (this.PathListBox.Items.Count != this.linkFlagsList.Count)
                {
                    RebuildLinkFlagsCache();
                }

                LinkFlags linkFlags = this.linkFlagsList[itemIndex];

                if (linkFlags != LinkFlags.None)
                {
                    int indicatorX = e.Bounds.Right - padding - (int)Math.Ceiling(linkIndicatorSize / 2f) - 2;
                    int indicatorY = e.Bounds.Top + padding + (int)Math.Floor(linkIndicatorSize / 2f);
                    int closedIndicatorX = e.Bounds.Right - 3;// indicatorX + linkIndicatorSize;

                    bool upFlag = linkFlags.HasFlag(LinkFlags.Up);
                    bool downFlag = linkFlags.HasFlag(LinkFlags.Down);
                    bool closedFlag = linkFlags.HasFlag(LinkFlags.Closed);

                    if (upFlag)
                    {
                        e.Graphics.DrawLine(Pens.MidnightBlue, indicatorX, e.Bounds.Top, indicatorX, indicatorY);
                    }

                    if (downFlag)
                    {
                        e.Graphics.DrawLine(Pens.MidnightBlue, indicatorX, e.Bounds.Bottom, indicatorX, indicatorY);
                    }

                    if (closedFlag)
                    {
                        if (upFlag && downFlag)
                        {
                            e.Graphics.DrawLine(Pens.MidnightBlue, closedIndicatorX, e.Bounds.Top, closedIndicatorX, e.Bounds.Bottom);
                        }
                        else if (upFlag)
                        {
                            int curveTop = indicatorY - 5;

                            PointF[] bezier =
                            {
                                new PointF(linkIndicatorRect.Right, indicatorY),
                                new PointF((closedIndicatorX - linkIndicatorRect.Right) / 2f + linkIndicatorRect.Right, indicatorY),
                                new PointF(closedIndicatorX, curveTop + (indicatorY - curveTop) / 2f),
                                new PointF(closedIndicatorX, curveTop)
                            };

                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.DrawBeziers(Pens.MidnightBlue, bezier);
                            e.Graphics.SmoothingMode = SmoothingMode.None;
                            e.Graphics.DrawLine(Pens.MidnightBlue, closedIndicatorX, curveTop, closedIndicatorX, e.Bounds.Top);
                        }
                        else if (downFlag)
                        {
                            int curveBottom = indicatorY + 5;

                            PointF[] bezier =
                            {
                                new PointF(linkIndicatorRect.Right, indicatorY),
                                new PointF((closedIndicatorX - linkIndicatorRect.Right) / 2f + linkIndicatorRect.Right, indicatorY),
                                new PointF(closedIndicatorX, curveBottom - (curveBottom - indicatorY) / 2f),
                                new PointF(closedIndicatorX, curveBottom)
                            };

                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.DrawBeziers(Pens.MidnightBlue, bezier);
                            e.Graphics.SmoothingMode = SmoothingMode.None;
                            e.Graphics.DrawLine(Pens.MidnightBlue, closedIndicatorX, curveBottom, closedIndicatorX, e.Bounds.Bottom);
                        }
                        else
                        {
                            int topY = linkIndicatorRect.Top - 3;
                            int bottomY = linkIndicatorRect.Bottom + 2;

                            e.Graphics.DrawLine(Pens.MidnightBlue, indicatorX, topY, indicatorX, indicatorY);
                            e.Graphics.DrawLine(Pens.MidnightBlue, indicatorX, bottomY, indicatorX, indicatorY);

                            PointF[] bezier =
                            {
                                new PointF(indicatorX, topY),
                                new PointF(closedIndicatorX + 1, topY),
                                new PointF(closedIndicatorX + 1, bottomY),
                                new PointF(indicatorX, bottomY)
                            };

                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.DrawBeziers(Pens.MidnightBlue, bezier);
                            e.Graphics.SmoothingMode = SmoothingMode.None;
                        }
                    }
                }
            }

            e.DrawFocusRectangle();
        }

        private void RebuildLinkFlagsCache()
        {
            this.linkFlagsList.Clear();

            int selectedIndex = this.PathListBox.SelectedIndex;
            int pathCount = this.paths.Count;

            int linkStartIndex = -1;
            bool linkedToNext = false;

            for (int i = 0; i < pathCount; i++)
            {
                linkFlagsList.Add(LinkFlags.None);

                bool isActive = i == selectedIndex;

                CloseType closeType = isActive ? this.CloseTypeFromUI : this.paths[i].CloseType;

                if (closeType == CloseType.Individual)
                {
                    linkFlagsList[i] |= LinkFlags.Closed;
                    linkStartIndex = -1;
                    continue;
                }

                bool linkedToPrevious = linkedToNext;

                if (closeType != CloseType.None || i >= pathCount - 1)
                {
                    linkedToNext = false;
                }
                else
                {
                    //bool nextIsActive = i + 1 == selectedIndex;
                    //CloseType nextCloseType = nextIsActive ? this.CloseTypeFromUI : this.paths[i + 1].CloseType;
                    //PointF nextFirstPoint = nextIsActive ? this.canvasPoints[0] : this.paths[i + 1].Points[0];
                    //PointF lastPoint = isActive ? this.canvasPoints.Last() : this.paths[i].Points.Last();

                    CloseType nextCloseType = this.paths[i + 1].CloseType;
                    PointF nextFirstPoint = this.paths[i + 1].Points[0];
                    PointF lastPoint = this.paths[i].Points.Last();

                    linkedToNext =
                        nextCloseType != CloseType.Individual &&
                        lastPoint == nextFirstPoint;
                }

                if (linkedToPrevious)
                {
                    linkFlagsList[i] |= LinkFlags.Up;
                }

                if (linkedToNext)
                {
                    linkFlagsList[i] |= LinkFlags.Down;
                }

                if (closeType == CloseType.Contiguous)
                {
                    if (linkStartIndex != -1)
                    {
                        for (int j = i; j >= linkStartIndex; j--)
                        {
                            linkFlagsList[j] |= LinkFlags.Closed;
                        }
                    }
                    else
                    {
                        linkFlagsList[i] |= LinkFlags.Closed;
                    }

                }

                if (!linkedToPrevious && linkedToNext)
                {
                    linkStartIndex = i;
                }
                else if (!linkedToNext)
                {
                    linkStartIndex = -1;
                }
            }
        }

        private void removebtn_Click(object sender, EventArgs e)
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            if (selectedIndex == InvalidPath || this.PathListBox.Items.Count == 0 || selectedIndex >= this.paths.Count)
            {
                return;
            }

            setUndo();

            this.paths.RemoveAt(selectedIndex);
            this.PathListBox.Items.RemoveAt(selectedIndex);
            this.canvasPoints.Clear();
            this.PathListBox.SelectedIndex = InvalidPath;
            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();
            this.PathListBox.Focus();
            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private void Clonebtn_Click(object sender, EventArgs e)
        {
            if (this.PathListBox.SelectedIndex == InvalidPath || this.canvasPoints.Count == 0)
            {
                return;
            }

            setUndo();

            this.paths.Add(new PathData(this.PathTypeFromUI, this.canvasPoints, this.CloseTypeFromUI, this.ArcOptionsFromUI));
            this.PathListBox.Items.Add(this.PathTypeFromUI.GetName());
            this.PathListBox.SelectedIndex = this.PathListBox.Items.Count - 1;
            this.RebuildLinkFlagsCache();
            this.PathListBox.Invalidate();
            this.PathListBox.Focus();
            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private void DNList_Click(object sender, EventArgs e)
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            if (selectedIndex > InvalidPath && selectedIndex < this.PathListBox.Items.Count - 1)
            {
                this.PathListBox.SelectedIndexChanged -= PathListBox_SelectedIndexChanged;
                ReOrderPath(selectedIndex);
                this.PathListBox.SelectedIndexChanged += PathListBox_SelectedIndexChanged;
                this.PathListBox.SelectedIndex++;

                this.RebuildLinkFlagsCache();
                this.PathListBox.Invalidate();
                this.PathListBox.Focus();
                this.canvas.Refresh();
            }
        }

        private void upList_Click(object sender, EventArgs e)
        {
            int selectedIndex = this.PathListBox.SelectedIndex;

            if (selectedIndex > 0)
            {
                this.PathListBox.SelectedIndexChanged -= PathListBox_SelectedIndexChanged;
                ReOrderPath(selectedIndex - 1);
                this.PathListBox.SelectedIndexChanged += PathListBox_SelectedIndexChanged;
                this.PathListBox.SelectedIndex--;

                this.RebuildLinkFlagsCache();
                this.PathListBox.Invalidate();
                this.PathListBox.Focus();
                this.canvas.Refresh();
            }
        }

        private void ReOrderPath(int index)
        {
            if (index == InvalidPath)
            {
                return;
            }

            PathData pd1 = this.paths[index];
            string LineTxt1 = this.PathListBox.Items[index].ToString();

            PathData pd2 = this.paths[index + 1];
            string LineTxt2 = this.PathListBox.Items[index + 1].ToString();

            this.paths[index] = pd2;
            this.PathListBox.Items[index] = LineTxt2;

            this.paths[index + 1] = pd1;
            this.PathListBox.Items[index + 1] = LineTxt1;
        }

        private void ToggleUpDownButtons()
        {
            int selectedIndex = this.PathListBox.SelectedIndex;
            int itemCount = this.PathListBox.Items.Count;

            if (itemCount < 2 || selectedIndex == InvalidPath)
            {
                this.upList.Enabled = false;
                this.DNList.Enabled = false;
            }
            else if (selectedIndex == 0)
            {
                this.upList.Enabled = false;
                this.DNList.Enabled = true;
            }
            else if (selectedIndex == itemCount - 1)
            {
                this.upList.Enabled = true;
                this.DNList.Enabled = false;
            }
            else
            {
                this.upList.Enabled = true;
                this.DNList.Enabled = true;
            }
        }

        [Flags]
        internal enum LinkFlags
        {
            None = 0,
            Up = 1,
            Down = 2,
            Closed = 4
        }
        #endregion

        #region Zoom functions
        private void splitButtonZoom_ButtonClick(object sender, EventArgs e)
        {
            int zoomFactor = this.canvas.Width / this.canvasBaseSize;
            int delta = ((ModifierKeys & Keys.Alt) == Keys.Alt) ? -1 : 1;

            int newZoomFactor = GetNewZoomFactor(zoomFactor, delta, true);

            ZoomToFactor(newZoomFactor);
        }

        private void ZoomDropDownItem_Click(object sender, EventArgs e)
        {
            if (sender is ZoomMenuItem zoomItem)
            {
                ZoomToFactor(zoomItem.ZoomFactor);
            }
        }

        private void ZoomToFactor(int zoomFactor)
        {
            Point viewportCenter = new Point(this.viewport.ClientSize.Width / 2, this.viewport.ClientSize.Height / 2);
            ZoomToFactor(zoomFactor, viewportCenter);
        }

        private void ZoomToFactor(int zoomFactor, Point zoomPoint)
        {
            int oldZoomFactor = this.canvas.Width / this.canvasBaseSize;
            if (oldZoomFactor == zoomFactor)
            {
                return;
            }

            PointF opBoxCoord = PointToCanvasCoord(operationBox.X, operationBox.Y);

            int newDimension = this.canvasBaseSize * zoomFactor;

            Point zoomedCanvasPos = new Point
            {
                X = (this.canvas.Location.X - zoomPoint.X) * newDimension / this.canvas.Width + zoomPoint.X,
                Y = (this.canvas.Location.Y - zoomPoint.Y) * newDimension / this.canvas.Height + zoomPoint.Y
            };

            // Clamp the canvas location; we're not overscrolling... yet
            int minX = (this.viewport.ClientSize.Width > newDimension) ? (this.viewport.ClientSize.Width - newDimension) / 2 : this.viewport.ClientSize.Width - newDimension;
            int maxX = (this.viewport.ClientSize.Width > newDimension) ? (this.viewport.ClientSize.Width - newDimension) / 2 : 0;
            zoomedCanvasPos.X = Math.Clamp(zoomedCanvasPos.X, minX, maxX);

            int minY = (this.viewport.ClientSize.Height > newDimension) ? (this.viewport.ClientSize.Height - newDimension) / 2 : this.viewport.ClientSize.Height - newDimension;
            int maxY = (this.viewport.ClientSize.Height > newDimension) ? (this.viewport.ClientSize.Height - newDimension) / 2 : 0;
            zoomedCanvasPos.Y = Math.Clamp(zoomedCanvasPos.Y, minY, maxY);

            // to avoid flicker, the order of execution is important
            if (oldZoomFactor > zoomFactor) // Zooming Out
            {
                this.canvas.Location = zoomedCanvasPos;
                this.canvas.Width = newDimension;
                this.canvas.Height = newDimension;
            }
            else // Zooming In
            {
                this.canvas.Width = newDimension;
                this.canvas.Height = newDimension;
                this.canvas.Location = zoomedCanvasPos;
            }
            this.canvas.Refresh();

            this.splitButtonZoom.Text = $"Zoom {zoomFactor}x";

            UpdateScrollBars();

            if (!operationBox.IsEmpty)
            {
                this.operationBox.Location = CanvasCoordToPoint(opBoxCoord);
                this.canvas.Invalidate();
            }
        }

        private void canvas_MouseEnter(object sender, EventArgs e)
        {
            this.canScrollZoom = true;
            this.hadFocus = this.ActiveControl;
            this.canvas.Focus();
        }

        private void canvas_MouseLeave(object sender, EventArgs e)
        {
            this.canScrollZoom = false;
            this.hadFocus?.Focus();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!this.canScrollZoom)
            {
                return;
            }

            int delta = Math.Sign(e.Delta);
            if (delta == 0)
            {
                return;
            }

            int oldZoomFactor = this.canvas.Width / this.canvasBaseSize;
            if ((delta > 0 && oldZoomFactor == zoomFactors[^1]) || (delta < 0 && oldZoomFactor == zoomFactors[0]))
            {
                return;
            }

            int newZoomFactor = GetNewZoomFactor(oldZoomFactor, delta, false);
            Point mousePosition = new Point(e.X - this.viewport.Location.X, e.Y - this.viewport.Location.Y);

            ZoomToFactor(newZoomFactor, mousePosition.ConstrainToInterval(10));

            base.OnMouseWheel(e);
        }

        private static int GetNewZoomFactor(int oldZoomFactor, int delta, bool wrapAround)
        {
            int oldZoomIndex = zoomFactors.IndexOf(oldZoomFactor);
            if (oldZoomIndex == -1)
            {
                return (delta > 0) ? zoomFactors[^1] : zoomFactors[0];
            }

            int factorCount = zoomFactors.Count;

            int newZoomIndex = wrapAround
                ? (((oldZoomIndex + delta) % factorCount) + factorCount) % factorCount
                : Math.Clamp(oldZoomIndex + delta, 0, factorCount - 1);

            return zoomFactors[newZoomIndex];
        }

        private sealed class ZoomMenuItem : ToolStripMenuItem
        {
            internal int ZoomFactor { get; }

            internal ZoomMenuItem(string text, int zoomFactor, EventHandler onClick)
                : base(text, null, onClick)
            {
                ZoomFactor = zoomFactor;
            }
        }
        #endregion

        #region Position Bar functions
        private void horScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            this.canvas.Left = -this.horScrollBar.Value;
        }

        private void verScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            this.canvas.Top = -this.verScrollBar.Value;
        }

        private void UpdateScrollBars()
        {
            this.horScrollBar.Visible = this.canvas.Width > this.viewport.ClientSize.Width;
            if (this.horScrollBar.Visible)
            {
                this.horScrollBar.Maximum = this.canvas.Width;
                this.horScrollBar.Value = Math.Abs(this.canvas.Location.X);
                this.horScrollBar.LargeChange = this.viewport.ClientSize.Width;
            }

            this.verScrollBar.Visible = this.canvas.Height > this.viewport.ClientSize.Height;
            if (this.verScrollBar.Visible)
            {
                this.verScrollBar.Maximum = this.canvas.Height;
                this.verScrollBar.Value = Math.Abs(this.canvas.Location.Y);
                this.verScrollBar.LargeChange = this.viewport.ClientSize.Height;
            }
        }
        #endregion

        #region Menubar functions
        private void newProjectMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show("Clear the current Shape Project, and start over?", "New Shape", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                ClearAllPaths();
                RefreshPdnCanvas();
                ZoomToFactor(1);
                resetHistory();
                this.FigureName.Text = "Untitled";
            }
        }

        private void openProject_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog OFD = new OpenFileDialog())
            {
                OFD.InitialDirectory = Settings.ProjectFolder;
                OFD.Filter = "Project Files (.dhp)|*.dhp|All Files (*.*)|*.*";
                OFD.FilterIndex = 1;
                OFD.RestoreDirectory = false;

                if (OFD.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                if (!File.Exists(OFD.FileName))
                {
                    MessageBox.Show("Specified file not found", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Settings.ProjectFolder = Path.GetDirectoryName(OFD.FileName);

                LoadProjectFile(OFD.FileName);
            }
        }

        private void saveProject_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("There are no paths to save.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string shapeName = GetSanitizedShapeName(this.FigureName.Text);

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = shapeName;
                sfd.InitialDirectory = Settings.ProjectFolder;
                sfd.Filter = "Project Files (.dhp)|*.dhp|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ProjectFolder = Path.GetDirectoryName(sfd.FileName);

                try
                {
                    ArrayListEx collection = new ArrayListEx(this.paths.Select(pathData => PData.FromPathData(pathData)).ToArray());
                    (collection[collection.Count - 1] as PData).Meta = this.FigureName.Text;
                    (collection[collection.Count - 1] as PData).SolidFill = this.solidFillCheckBox.Checked;

                    XmlSerializer ser = new XmlSerializer(typeof(ArrayListEx), new Type[] { typeof(PData) });

                    //PathDataCollection collection = new PathDataCollection(this.paths, this.solidFillCheckBox.Checked, this.FigureName.Text);

                    //XmlSerializer ser = new XmlSerializer(typeof(PathDataCollection));
                    using (StringWriterWithEncoding stringWriter = new StringWriterWithEncoding())
                    {
                        ser.Serialize(stringWriter, collection);
                        File.WriteAllText(sfd.FileName, stringWriter.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("There was an error saving the file.\r\n\r" + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                AddToRecents(sfd.FileName);
            }
        }

        private void ExportPdnStreamGeometry_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("There are no paths to save.", "Paint.NET Shape Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string shapeName = GetSanitizedShapeName(this.FigureName.Text);

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = shapeName;
                sfd.InitialDirectory = Settings.ShapeFolder;
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ShapeFolder = Path.GetDirectoryName(sfd.FileName);

                string output = string.Format(
                    ExportConsts.PdnStreamGeometryFile,
                    shapeName,
                    GenerateStreamGeometry());

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("The shape has been exported as a XAML file for use in paint.net.\r\n\r\nPlease note that paint.net needs to be restarted to use the shape.", "Paint.NET Shape Export - StreamGeometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportPdnPathGeometry_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("There are no paths to save.", "Paint.NET Shape Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string shapeName = GetSanitizedShapeName(this.FigureName.Text);

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = shapeName;
                sfd.InitialDirectory = Settings.ShapeFolder;
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ShapeFolder = Path.GetDirectoryName(sfd.FileName);

                string output = string.Format(
                    ExportConsts.PdnPathGeometryFile,
                    shapeName,
                    this.solidFillCheckBox.Checked ? "Nonzero" : "EvenOdd",
                    GeneratePathGeometry());

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("The shape has been exported as a XAML file for use in paint.net.\r\n\r\nPlease note that paint.net needs to be restarted to use the shape.", "Paint.NET Shape Export - PathGeometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void exportSvgMenuItem_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("There are no paths to save.", "SVG Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string shapeName = GetSanitizedShapeName(this.FigureName.Text);

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = shapeName;
                sfd.InitialDirectory = Settings.ShapeFolder;
                sfd.Filter = "SVG Files (.svg)|*.svg|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ShapeFolder = Path.GetDirectoryName(sfd.FileName);

                File.WriteAllText(sfd.FileName, GenerateSvg());
                MessageBox.Show("The shape has been exported as an SVG.", "SVG Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ImportGeometry_Click(object sender, EventArgs e)
        {
            string fileName = null;

            using (OpenFileDialog OFD = new OpenFileDialog())
            {
                OFD.InitialDirectory = Settings.ShapeFolder;
                OFD.Filter = "All Supported Files|*.svg;*.xaml;*.xml|SVG Files|*.svg|XAML Files|*.xaml|XML Files|*.xml|All Files|*.*";
                OFD.FilterIndex = 1;
                OFD.RestoreDirectory = false;

                if (OFD.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                fileName = OFD.FileName;
            }

            if (!File.Exists(fileName))
            {
                MessageBox.Show("Specified file not found", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Settings.ShapeFolder = Path.GetDirectoryName(fileName);

            string fileContents = File.ReadAllText(fileName);
            string streamGeometry = StreamGeometryUtil.TryExtractStreamGeometry(fileContents);

            if (streamGeometry == null)
            {
                MessageBox.Show("No valid Geometry was found in the file.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadStreamGeometry(streamGeometry);
        }

        private void PasteStreamGeometry_Click(object sender, EventArgs e)
        {
            string clipboardString = null;
            bool pasted = false;
            try
            {
                clipboardString = Clipboard.GetText();
                pasted = true;
            }
            catch
            {
            }

            if (!pasted)
            {
                MessageBox.Show("There was an error retrieving text from the Clipboard.", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(clipboardString))
            {
                MessageBox.Show("The text on the Clipboard is empty.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string validatedStreamGeometry = StreamGeometryUtil.TryGetValidatedStreamGeometry(clipboardString);
            if (validatedStreamGeometry == null)
            {
                MessageBox.Show("The text on the Clipboard is not in the StreamGeometry Format.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadStreamGeometry(validatedStreamGeometry);
        }

        private void CopyStreamGeometry_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("There are no paths to copy.", "Copy StreamGeometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool copied = false;
            try
            {
                Clipboard.SetText(GenerateStreamGeometry());
                copied = true;
            }
            catch
            {
            }

            if (!copied)
            {
                MessageBox.Show("There was an error copying the StreamGeometry to Clipboard.", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            this.undoMenuItem.Enabled = (this.undoCount > 0);
            this.redoMenuItem.Enabled = (this.redoCount > 0);
            this.removePathToolStripMenuItem.Enabled = (this.PathListBox.SelectedIndex > InvalidPath);
            this.clonePathToolStripMenuItem.Enabled = (this.PathListBox.SelectedIndex > InvalidPath);
            this.loopPathToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1);
            this.flipHorizontalToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1 || this.PathListBox.Items.Count > 0);
            this.flipVerticalToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1 || this.PathListBox.Items.Count > 0);
            this.opBoxMenuItem.Enabled = (this.canvasPoints.Count > 1 || this.PathListBox.Items.Count > 0);
            this.autoScaleMenuItem.Enabled = (this.canvasPoints.Count <= 1 && this.PathListBox.Items.Count > 0);
        }

        private void editToolStripMenuItem_DropDownClosed(object sender, EventArgs e)
        {
            this.undoMenuItem.Enabled = true;
            this.redoMenuItem.Enabled = true;
            this.removePathToolStripMenuItem.Enabled = true;
            this.loopPathToolStripMenuItem.Enabled = true;
            this.opBoxMenuItem.Enabled = true;
        }

        private void Flip_Click(object sender, EventArgs e)
        {
            setUndo();

            bool isHorizontal = (sender is ToolStripMenuItem menuItem && menuItem.Tag.ToString() == "H");

            if (this.canvasPoints.Count == 0)
            {
                foreach (PathData path in this.paths)
                {
                    PointF[] pl = path.Points;

                    if (isHorizontal)
                    {
                        for (int i = 0; i < pl.Length; i++)
                        {
                            pl[i] = new PointF(1 - pl[i].X, pl[i].Y);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < pl.Length; i++)
                        {
                            pl[i] = new PointF(pl[i].X, 1 - pl[i].Y);
                        }
                    }

                    if (path.PathType == PathType.EllipticalArc)
                    {
                        if (path.ArcOptions.HasFlag(ArcOptions.PositiveSweep))
                        {
                            path.ArcOptions &= ~ArcOptions.PositiveSweep;
                        }
                        else
                        {
                            path.ArcOptions |= ArcOptions.PositiveSweep;
                        }
                    }
                }
            }
            else
            {
                PointF[] tmp = this.canvasPoints.ToArray();
                PointF mid = tmp.Average();
                if (isHorizontal)
                {
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i] = new PointF(-(tmp[i].X - mid.X) + mid.X, tmp[i].Y);
                    }
                }
                else
                {
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i] = new PointF(tmp[i].X, -(tmp[i].Y - mid.Y) + mid.Y);
                    }
                }

                if (this.Elliptical.Checked)
                {
                    this.Sweep.CheckState = (this.Sweep.CheckState == CheckState.Checked) ? CheckState.Indeterminate : CheckState.Checked;
                }

                this.canvasPoints.Clear();
                this.canvasPoints.AddRange(tmp);

                if (this.PathListBox.SelectedIndex != InvalidPath)
                {
                    UpdateExistingPath();
                }
            }
            this.canvas.Refresh();
        }

        private void LineLoop_Click(object sender, EventArgs e)
        {
            if (this.canvasPoints.Count > 2 && !this.Elliptical.Checked)
            {
                setUndo();
                this.canvasPoints[this.canvasPoints.Count - 1] = this.canvasPoints[0];
                this.canvas.Refresh();
            }
        }

        private void opBoxMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.operationBox.IsEmpty)
            {
                HideOpBox();
            }
            else
            {
                ShowOpBox();
            }
        }

        private void autoScaleMenuItem_Click(object sender, EventArgs e)
        {
            setUndo();
            CanvasUtil.AutoScaleAndCenter(this.paths);
            this.canvas.Refresh();
            RefreshPdnCanvas();
        }

        private void HelpMenu_Click(object sender, EventArgs e)
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string pdfPath = (sender is ToolStripMenuItem menuItem && menuItem.Name.Equals(nameof(QuickStartStripMenuItem), StringComparison.OrdinalIgnoreCase))
                ? Path.Combine(directory, "ShapeMaker QuickStart.pdf")
                : Path.Combine(directory, "ShapeMaker User Guide.pdf");

            if (!File.Exists(pdfPath))
            {
                MessageBox.Show("Help File Not Found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open the Help Page\r\n{ex.Message}\r\n{pdfPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void keyboardShortcutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Shortcuts ks = new Shortcuts())
            {
                ks.ShowDialog(this);
            }
        }

        private void aboutShapeMakerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this.Text + "\nCopyright \u00A9 2020, The Dwarf Horde\n\n" +
                "Jason Wendt (toe_head2001)\n- Code Lead (v1.3 onward), Design\n\n" +
                "Rob Tauler (TechnoRobbo)\n- Code Lead (up to v1.2.3), Design\n\n" +
                "John Robbins (Red Ochre)\n- Graphics Lead, Design\n\n" +
                "Scott Stringer (Ego Eram Reputo)\n- Documentation Lead, Design\n\n" +
                "David Issel (BoltBait)\n- Beta Testing, Design",
                "About ShapeMaker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region Toolbar functions
        private void OptionToggle(object sender, EventArgs e)
        {
            if (sender is ToolStripButton button)
            {
                button.Checked = !button.Checked;
            }

            if (sender == this.Snap)
            {
                this.Snap.Image = (this.Snap.Checked) ? Properties.Resources.SnapOn : Properties.Resources.SnapOff;
            }
            else if (sender == this.LinkedPaths)
            {
                this.LinkedPaths.Image = (this.LinkedPaths.Checked) ? Properties.Resources.LinkOn : Properties.Resources.LinkOff;
            }
        }

        private void PathTypeToggle(object sender, EventArgs e)
        {
            if (sender is ToolStripButton button && button.Checked)
            {
                return;
            }

            if (sender is ToolStripButtonWithKeys buttonWithKeys)
            {
                this.activeType = buttonWithKeys.PathType;
            }

            PathTypeToggle();
            this.canvas.Refresh();
        }

        private void PathTypeToggle()
        {
            foreach (ToolStripButtonWithKeys button in this.typeButtons)
            {
                button.Checked = (button.PathType == this.activeType);
                if (button.Checked && this.canvasPoints.Count > 1)
                {
                    PointF hold = this.canvasPoints[0];
                    this.canvasPoints.Clear();
                    this.canvasPoints.Add(hold);
                }
            }

            bool circleMacro = (this.Elliptical.Checked && !this.MacroCircle.Checked);
            this.Arc.Enabled = circleMacro;
            this.Sweep.Enabled = circleMacro;
        }

        private void MacroToggle(object sender, EventArgs e)
        {
            bool state;
            if (sender == this.MacroRect)
            {
                state = !this.MacroRect.Checked;
                this.StraightLine.PerformClick();
                this.MacroRect.Checked = state;
            }
            else if (sender == this.MacroCubic)
            {
                state = !this.MacroCubic.Checked;
                this.CubicBezier.PerformClick();
                this.MacroCubic.Checked = state;
            }
            else if (sender == this.MacroCircle)
            {
                state = !this.MacroCircle.Checked;
                this.Elliptical.PerformClick();
                this.MacroCircle.Checked = state;
            }

            bool enableArcButtons = (this.Elliptical.Checked && !this.MacroCircle.Checked);
            this.Arc.Enabled = enableArcButtons;
            this.Sweep.Enabled = enableArcButtons;

            bool enableCloseButtons = !(this.MacroCircle.Checked || this.MacroRect.Checked);
            this.ClosePath.Enabled = enableCloseButtons;
            this.CloseContPaths.Enabled = enableCloseButtons;

            this.canvas.Refresh();
        }

        private void ClosePath_Click(object sender, EventArgs e)
        {
            setUndo();

            if (sender == this.CloseContPaths)
            {
                this.CloseContPaths.Checked = !this.CloseContPaths.Checked;

                if (this.CloseContPaths.Checked && this.ClosePath.Checked)
                {
                    this.ClosePath.Checked = false;
                }
            }
            else if (sender == this.ClosePath)
            {
                this.ClosePath.Checked = !this.ClosePath.Checked;

                if (this.ClosePath.Checked && this.CloseContPaths.Checked)
                {
                    this.CloseContPaths.Checked = false;
                }
            }

            this.ClosePath.Image = (this.ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            this.CloseContPaths.Image = (this.CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;

            if (this.PathListBox.SelectedIndex != InvalidPath)
            {
                UpdateExistingPath();
            }

            this.canvas.Refresh();
        }

        private void Property_Click(object sender, EventArgs e)
        {
            setUndo();

            if (sender is ToolStripButton button)
            {
                button.CheckState = button.CheckState == CheckState.Checked ? CheckState.Indeterminate : CheckState.Checked;
            }

            if (sender == this.Arc)
            {
                this.Arc.Image = (this.Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcLarge : Properties.Resources.ArcSmall;
            }
            else if (sender == this.Sweep)
            {
                this.Sweep.Image = (this.Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;
            }

            this.canvas.Refresh();

            if (this.PathListBox.SelectedIndex != InvalidPath)
            {
                UpdateExistingPath();
            }
        }
        #endregion

        #region Image Tracing
        private void setTraceImage()
        {
#if !FASTDEBUG
            if (this.traceLayer.Checked)
            {
                this.canvas.Bitmap = this.Environment
                    .GetSourceBitmapBgra32()
                    .CreateClipper(this.Environment.Selection.RenderBounds);
            }
            else if (this.traceImage.Checked)
            {
                this.canvas.Bitmap = this.Environment.Document
                    .GetBitmapBgra32()
                    .CreateClipper(this.Environment.Selection.RenderBounds);
            }
            else if (this.traceClipboard.Checked)
            {
                IClipboardImage<ColorBgra32> bitmap = this.Services.GetService<IClipboardService>().TryGetImageBgra32();
                if (bitmap == null)
                {
                    this.traceLayer.Focus();
                    MessageBox.Show("Couldn't load an image from the clipboard.", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                this.canvas.Bitmap = bitmap;
            }
#else
            if (this.traceClipboard.Checked)
            {
                Thread t = new Thread(new ThreadStart(GetImageFromClipboard));
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();

                if (this.clipboardImage == null)
                {
                    this.traceLayer.Focus();
                    MessageBox.Show("Couldn't load an image from the clipboard.", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                this.canvas.BackgroundImage = this.clipboardImage;
            }

            void GetImageFromClipboard()
            {
                this.clipboardImage?.Dispose();
                this.clipboardImage = null;
                try
                {
                    IDataObject clippy = Clipboard.GetDataObject();
                    if (clippy == null)
                    {
                        return;
                    }

                    if (Clipboard.ContainsData("PNG"))
                    {
                        object pngObject = Clipboard.GetData("PNG");
                        if (pngObject is MemoryStream pngStream)
                        {
                            this.clipboardImage = (Bitmap)Image.FromStream(pngStream);
                        }
                    }
                    else if (clippy.GetDataPresent(DataFormats.Bitmap))
                    {
                        this.clipboardImage = (Bitmap)clippy.GetData(typeof(Bitmap));
                    }
                }
                catch
                {
                }
            }
#endif
        }

        private void traceSource_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radio && radio.Checked)
            {
                setTraceImage();
            }
        }

        private void opacitySlider_Scroll(object sender, EventArgs e)
        {
            this.toolTip1.SetToolTip(this.opacitySlider, $"{this.opacitySlider.Value}%");
            this.canvas.Refresh();
        }

        private void FitBG_CheckedChanged(object sender, EventArgs e)
        {
            this.canvas.SizeMode = (this.FitBG.Checked) ? Direct2DPictureBoxSizeMode.Zoom : Direct2DPictureBoxSizeMode.Normal;
            this.canvas.Refresh();
        }
        #endregion

        #region Misc Form Controls' event functions
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (this.canvasPoints.Count > 1 && this.PathListBox.SelectedIndex == InvalidPath)
            {
                AddNewPath();
            }
        }

        private void Deselect_Click(object sender, EventArgs e)
        {
            Deselect();
        }

        private void ApplyBtn_Click(object sender, EventArgs e)
        {
            AddNewPath();
        }

        private void FigureName_Enter(object sender, EventArgs e)
        {
            if (this.FigureName.Text == "Untitled")
            {
                this.FigureName.Text = string.Empty;
            }
        }

        private void FigureName_Leave(object sender, EventArgs e)
        {
            if (this.FigureName.Text.Length == 0)
            {
                this.FigureName.Text = "Untitled";
            }
        }

        private void solidFillCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            RefreshPdnCanvas();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // ToDo: get rid of this timer

            if (!this.Elliptical.Checked)
            {
                this.MacroCircle.Checked = false;
            }

            if (!this.StraightLine.Checked)
            {
                this.MacroRect.Checked = false;
            }

            if (!this.CubicBezier.Checked)
            {
                this.MacroCubic.Checked = false;
            }

            ToggleUpDownButtons();

            bool newPath = this.PathListBox.SelectedIndex == InvalidPath;
            this.clonePathButton.Enabled = !newPath;
            this.removePathButton.Enabled = !newPath;
            this.MacroCircle.Enabled = newPath;
            this.MacroRect.Enabled = newPath;
            this.MacroCubic.Enabled = newPath;
            bool enableCloseButtons = !(this.MacroCircle.Checked || this.MacroRect.Checked);
            this.ClosePath.Enabled = enableCloseButtons;
            this.CloseContPaths.Enabled = enableCloseButtons;
            this.DeselectBtn.Enabled = (!newPath && this.canvasPoints.Count != 0);
            this.AddBtn.Enabled = (newPath && this.canvasPoints.Count > 1);
            this.DiscardBtn.Enabled = (newPath && this.canvasPoints.Count > 1);

            if (Control.ModifierKeys == Keys.Control)
            {
                this.keyTrak = true;
            }
            else if (this.keyTrak)
            {
                this.keyTrak = false;
                this.canvas.Refresh();
            }

            if (this.canvasPoints.Count > 0 || this.PathListBox.Items.Count > 0)
            {
                this.statusLabelNubsUsed.Text = $"{this.canvasPoints.Count}/{maxPoints} Nubs used";
                this.statusLabelPathsUsed.Text = $"{this.PathListBox.Items.Count} Paths";
            }

            if (newPath)
            {
                this.isNewPath = true;
            }
        }
        #endregion

        #region Recent Items functions
        private void AddToRecents(string filePath)
        {
            string recents = Settings.RecentProjects;

            if (recents.Length == 0)
            {
                recents = filePath;
            }
            else
            {
                recents = filePath + "|" + recents;

                string[] paths = recents.Split('|', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

                int length = Math.Min(8, paths.Length);
                recents = string.Join("|", paths, 0, length);
            }

            Settings.RecentProjects = recents;
        }

        private void openRecentProject_DropDownOpening(object sender, EventArgs e)
        {
            this.openRecentProject.DropDownItems.Clear();

            string recents = Settings.RecentProjects;

            List<ToolStripItem> recentsList = new List<ToolStripItem>();
            XmlSerializer pDataSerializer = new XmlSerializer(typeof(ArrayListEx), new Type[] { typeof(PData) });
            XmlSerializer pathDataSerializer = new XmlSerializer(typeof(PathDataCollection));
            IEnumerable<string> paths = recents.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 1;

            foreach (string projectPath in paths)
            {
                if (!File.Exists(projectPath))
                {
                    continue;
                }

                string shapeName = null;

                try
                {
                    using (FileStream fileStream = File.OpenRead(projectPath))
                    using (XmlTextReader xmlReader = new XmlTextReader(fileStream))
                    {
                        if (pathDataSerializer.CanDeserialize(xmlReader))
                        {
                            PathDataCollection collection = (PathDataCollection)pathDataSerializer.Deserialize(xmlReader);
                            shapeName = collection.ShapeName;
                        }
                        else if (pDataSerializer.CanDeserialize(xmlReader))
                        {
                            PData pData = ((ArrayListEx)pDataSerializer.Deserialize(xmlReader)).OfType<PData>().Last();
                            shapeName = pData.Meta;
                        }
                    }
                }
                catch
                {
                }

                string menuText = string.IsNullOrWhiteSpace(shapeName)
                    ? $"&{count} {Path.GetFileName(projectPath)}"
                    : $"&{count} {shapeName} ({Path.GetFileName(projectPath)})";

                ToolStripMenuItem recentItem = new ToolStripMenuItem();
                recentItem.Text = menuText;
                recentItem.ToolTipText = projectPath;
                recentItem.Click += RecentItem_Click;

                recentsList.Add(recentItem);
                count++;
            }

            if (recentsList.Count > 0)
            {
                ToolStripSeparator toolStripSeparator = new ToolStripSeparator();
                recentsList.Add(toolStripSeparator);

                ToolStripMenuItem clearRecents = new ToolStripMenuItem
                {
                    Text = "&Clear List"
                };
                clearRecents.Click += ClearRecents_Click;
                recentsList.Add(clearRecents);

                this.openRecentProject.DropDownItems.AddRange(recentsList.ToArray());
            }
            else
            {
                ToolStripMenuItem noRecents = new ToolStripMenuItem
                {
                    Text = "No Recent Projects",
                    Enabled = false
                };

                this.openRecentProject.DropDownItems.Add(noRecents);
            }
        }

        private void ClearRecents_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear the Open Recent Project list?", "ShapeMaker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            Settings.RecentProjects = string.Empty;
        }

        private void RecentItem_Click(object sender, EventArgs e)
        {
            string projectPath = (sender as ToolStripMenuItem)?.ToolTipText;
            if (!File.Exists(projectPath))
            {
                MessageBox.Show("File not found.\n" + projectPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadProjectFile(projectPath);
        }
        #endregion

        #region Draw on Canvas Properties
        private void ColorPanel_Click(object sender, EventArgs e)
        {
#if !FASTDEBUG
            if (sender is Panel colorPanel)
            {
                using (ColorWindow colorWindow = new ColorWindow())
                {
                    colorWindow.ShowAlpha = true;
                    colorWindow.Color = colorPanel.BackColor;
                    colorWindow.PaletteColors = this.Services.GetService<IPalettesService>().CurrentPalette.Select(colorBgra => colorBgra.ToColor()).ToList();
                    if (colorWindow.ShowDialog() == DialogResult.OK)
                    {
                        colorPanel.BackColor = colorWindow.Color;
                        RefreshPdnCanvas();
                    }
                }
            }
#endif
        }

        private void ColorPanel_Paint(object sender, PaintEventArgs e)
        {
#if !FASTDEBUG
            Rectangle outerRect = new RectangleF(e.Graphics.VisibleClipBounds.X, e.Graphics.VisibleClipBounds.Y, e.Graphics.VisibleClipBounds.Width - 1, e.Graphics.VisibleClipBounds.Height - 1).Round();
            Rectangle innerRect = new Rectangle(outerRect.X + 1, outerRect.Y + 1, outerRect.Width - 2, outerRect.Height - 2);

            if (sender is Panel panel && panel.Enabled)
            {
                using (HatchBrush hatchBrush = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.LightGray, Color.Gray))
                using (LinearGradientBrush gradientBrush = new LinearGradientBrush(innerRect, Color.FromArgb(byte.MaxValue, panel.BackColor), panel.BackColor, LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(hatchBrush, innerRect);
                    e.Graphics.FillRectangle(gradientBrush, innerRect);
                }
                e.Graphics.DrawRectangle(Pens.Black, outerRect);
                e.Graphics.DrawRectangle(Pens.White, innerRect);
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.Control, e.ClipRectangle);
                e.Graphics.DrawRectangle(Pens.Gray, outerRect);
            }
#endif
        }

        private void DrawOnCanvas_CheckedChanged(object sender, EventArgs e)
        {
#if !FASTDEBUG
            DrawModes drawModes = this.drawModeBox.SelectedIndex switch
            {
                0 => DrawModes.Stroke,
                1 => DrawModes.Fill,
                2 => DrawModes.Stroke | DrawModes.Fill,
                _ => DrawModes.Stroke,
            };

            bool enable = this.DrawOnCanvas.Checked;
            this.strokeColorPanel.Enabled = enable;
            this.fillColorPanel.Enabled = enable && drawModes.HasFlag(DrawModes.Stroke) && drawModes.HasFlag(DrawModes.Fill);
            this.strokeThicknessBox.Enabled = enable && drawModes.HasFlag(DrawModes.Stroke);
            this.drawModeBox.Enabled = enable;
            this.fitCanvasBox.Enabled = enable;
            this.drawClippingArea = enable && !this.fitCanvasBox.Checked;
            this.canvas.Refresh();

            RefreshPdnCanvas();
#endif
        }

        private void strokeThicknessBox_ValueChanged(object sender, EventArgs e)
        {
#if !FASTDEBUG
            RefreshPdnCanvas();
#endif
        }

        private void drawModeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
#if !FASTDEBUG
            bool enable = this.DrawOnCanvas.Checked;

            DrawModes drawModes = this.drawModeBox.SelectedIndex switch
            {
                0 => DrawModes.Stroke,
                1 => DrawModes.Fill,
                2 => DrawModes.Stroke | DrawModes.Fill,
                _ => DrawModes.Stroke,
            };

            this.fillColorPanel.Enabled = enable && drawModes.HasFlag(DrawModes.Stroke) && drawModes.HasFlag(DrawModes.Fill);
            this.strokeThicknessBox.Enabled = enable && drawModes.HasFlag(DrawModes.Stroke);

            RefreshPdnCanvas();
#endif
        }

        private void fitCanvasBox_CheckedChanged(object sender, EventArgs e)
        {
#if !FASTDEBUG
            this.drawClippingArea = this.DrawOnCanvas.Checked && !this.fitCanvasBox.Checked;
            this.canvas.Refresh();

            RefreshPdnCanvas();
#endif
        }

        private void RefreshPdnCanvas()
        {
#if !FASTDEBUG
            this.geometryForPdnCanvas = (this.DrawOnCanvas.Checked && this.paths.Count > 0) ?
                GenerateStreamGeometry() :
                null;

            UpdateTokenFromDialog();
#endif
        }
        #endregion
    }
}
