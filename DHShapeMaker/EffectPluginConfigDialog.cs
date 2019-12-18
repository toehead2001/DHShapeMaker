//Elliptical Arc algorithm from svg.codeplex.com
#if PDNPLUGIN
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.Effects;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ShapeMaker
{
#if PDNPLUGIN
    internal partial class EffectPluginConfigDialog : EffectConfigDialog
#else
    internal partial class EffectPluginConfigDialog : Form
#endif
    {
        private static readonly string[] lineNames =
        {
            "Straight Lines",
            "Ellipse",
            "Cubic Beziers",
            "Smooth Cubic Beziers",
            "Quadratic Beziers",
            "Smooth Quadratic Beziers"
        };

        private static readonly Color[] lineColors =
        {
            Color.Black,
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.DarkGoldenrod,
            Color.Purple
        };

        private static readonly Color[] lineColorsLight =
        {
            Color.FromArgb(204, 204, 204),
            Color.FromArgb(255, 204, 204),
            Color.FromArgb(204, 204, 255),
            Color.FromArgb(204, 230, 204),
            Color.FromArgb(241, 231, 206),
            Color.FromArgb(230, 204, 230)
        };

        private static readonly Color anchorColor = Color.Teal;

        private PathType activeType;
        private readonly ToolStripButton[] typeButtons = new ToolStripButton[6];

        private const int maxPaths = 200;
        private const int maxPoints = byte.MaxValue;
        private const int InvalidNub = -1;
        private int clickedNub = InvalidNub;
        private PointF moveStart;
        private readonly List<PointF> canvasPoints = new List<PointF>(maxPoints);

        private const int undoMax = 16;
        private readonly List<PData>[] undoLines = new List<PData>[undoMax];
        private readonly PointF[][] undoPoints = new PointF[undoMax][];
        private readonly PathType[] undoType = new PathType[undoMax];
        private readonly int[] undoSelected = new int[undoMax];
        private int undoCount = 0;
        private int redoCount = 0;
        private int undoPointer = 0;

        private float lastRot = 180;
        private bool keyTrak = false;
        private readonly List<PData> paths = new List<PData>();
        private bool panFlag = false;
        private bool canScrollZoom = false;
        private static float dpiScale = 1;
        private Control hadFocus;
        private bool isNewPath = true;
        private int canvasBaseSize;
#if PDNPLUGIN
        private GraphicsPath[] pathForPdnCanvas = null;
#else
        private Bitmap clipboardImage = null;
#endif
        private bool moveFlag = false;
        private bool wheelScaleOrRotate = false;
        private bool drawAverage = false;
        private PointF averagePoint = new PointF(0.5f, 0.5f);
        private readonly Dictionary<Keys, ToolStripButtonWithKeys> hotKeys = new Dictionary<Keys, ToolStripButtonWithKeys>();

        internal EffectPluginConfigDialog()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentUICulture;
            InitializeComponent();

#if PDNPLUGIN
            this.UseAppThemeColors = true;
#else
            this.buttonOK.Visible = false;
            this.DrawOnCanvas.Visible = false;

            this.BackColor = Color.White;
            this.ForeColor = Color.Black;
            this.ShowInTaskbar = true;
#endif

            // Theming
            PdnTheme.SetColors(this.ForeColor, this.BackColor);

            this.menuStrip1.Renderer = PdnTheme.Renderer;
            this.statusStrip1.Renderer = PdnTheme.Renderer;

            this.toolStripUndo.Renderer = new ThemeRenderer(Color.White, Color.Silver);
            this.toolStripBlack.Renderer = new ThemeRenderer(lineColorsLight[0], lineColors[0]);
            this.toolStripBlue.Renderer = new ThemeRenderer(lineColorsLight[2], lineColors[2]);
            this.toolStripGreen.Renderer = new ThemeRenderer(lineColorsLight[3], lineColors[3]);
            this.toolStripYellow.Renderer = new ThemeRenderer(lineColorsLight[4], lineColors[4]);
            this.toolStripPurple.Renderer = new ThemeRenderer(lineColorsLight[5], lineColors[5]);
            this.toolStripRed.Renderer = new ThemeRenderer(lineColorsLight[1], lineColors[1]);
            this.toolStripOptions.Renderer = new ThemeRenderer(Color.White, Color.Silver);

            this.LineList.ForeColor = PdnTheme.ForeColor;
            this.LineList.BackColor = PdnTheme.BackColor;
            this.FigureName.ForeColor = PdnTheme.ForeColor;
            this.FigureName.BackColor = PdnTheme.BackColor;
            this.OutputScale.ForeColor = PdnTheme.ForeColor;
            this.OutputScale.BackColor = PdnTheme.BackColor;
        }

#if PDNPLUGIN
        #region Effect Token functions
        protected override void InitialInitToken()
        {
            this.theEffectToken = new EffectPluginConfigToken(this.pathForPdnCanvas, this.paths, false, 100, true, "Untitled", false);
        }

        protected override void InitTokenFromDialog()
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)this.EffectToken;
            token.GP = this.pathForPdnCanvas;
            token.PathData = this.paths;
            token.Draw = this.DrawOnCanvas.Checked;
            token.ShapeName = this.FigureName.Text;
            token.Scale = this.OutputScale.Value;
            token.SnapTo = this.Snap.Checked;
            token.SolidFill = this.SolidFillMenuItem.Checked;
        }

        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)effectTokenCopy;
            this.DrawOnCanvas.Checked = token.Draw;
            this.FigureName.Text = token.ShapeName;
            this.OutputScale.Value = token.Scale;
            this.Snap.Checked = token.SnapTo;
            this.SolidFillMenuItem.Checked = token.SolidFill;

            this.paths.Clear();
            this.LineList.Items.Clear();
            foreach (PData p in token.PathData)
            {
                this.paths.Add(p);
                this.LineList.Items.Add(lineNames[p.LineType]);
            }
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
            this.Text = "ShapeMaker - Test";// v" + version;

            this.Arc.Enabled = false;
            this.Sweep.Enabled = false;

            this.typeButtons[0] = this.StraightLine;
            this.typeButtons[1] = this.Elliptical;
            this.typeButtons[2] = this.CubicBezier;
            this.typeButtons[3] = this.SCubicBezier;
            this.typeButtons[4] = this.QuadBezier;
            this.typeButtons[5] = this.SQuadBezier;

            this.toolTip1.ReshowDelay = 0;
            this.toolTip1.AutomaticDelay = 0;
            this.toolTip1.AutoPopDelay = 0;
            this.toolTip1.InitialDelay = 0;
            this.toolTip1.UseFading = false;
            this.toolTip1.UseAnimation = false;

            this.timer1.Enabled = true;

            #region DPI fixes
            this.MinimumSize = this.Size;
            this.LineList.ItemHeight = getDpiSize(this.LineList.ItemHeight);
            this.LineList.Height = this.upList.Top - this.LineList.Top;
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

            this.statusLabelPathsUsed.Text = $"{this.LineList.Items.Count}/{maxPaths} Paths used";

            // Store hotkeys in a Dictionary
            foreach (object control in this.Controls)
            {
                if (control is ToolStrip toolStrip)
                {
                    foreach (object subControl in toolStrip.Items)
                    {
                        if (subControl is ToolStripButtonWithKeys button)
                        {
                            Keys keys = button.ShortcutKeys;
                            if (keys != Keys.None && !this.hotKeys.ContainsKey(keys))
                            {
                                this.hotKeys.Add(keys, button);
                            }
                        }
                    }
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
            this.viewport.Width = this.LineList.Left - this.viewport.Left - getDpiSize(32);
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
                if (this.canvasPoints.Count > 1 && this.LineList.SelectedIndex == -1)
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
        private void setUndo()
        {
            setUndo(false);
        }

        private void setUndo(bool deSelected)
        {
            // set undo
            this.Undo.Enabled = true;
            this.Redo.Enabled = false;

            this.redoCount = 0;
            this.undoCount++;
            this.undoCount = (this.undoCount > undoMax) ? undoMax : this.undoCount;
            this.undoType[this.undoPointer] = getPathType();
            this.undoSelected[this.undoPointer] = (deSelected) ? -1 : this.LineList.SelectedIndex;
            this.undoPoints[this.undoPointer] = this.canvasPoints.ToArray();
            if (this.undoLines[this.undoPointer] == null)
            {
                this.undoLines[this.undoPointer] = new List<PData>();
            }
            else
            {
                this.undoLines[this.undoPointer].Clear();
            }

            foreach (PData pd in this.paths)
            {
                PointF[] tmp = new PointF[pd.Lines.Length];
                Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                this.undoLines[this.undoPointer].Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
            }

            this.undoPointer++;
            this.undoPointer %= undoMax;
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
                this.undoPointer--;
            }

            this.undoPointer--;
            this.undoPointer += undoMax;
            this.undoPointer %= undoMax;

            this.canvasPoints.Clear();
            if (this.undoPoints[this.undoPointer].Length != 0)
            {
                this.canvasPoints.AddRange(this.undoPoints[this.undoPointer]);
            }

            this.LineList.Items.Clear();
            this.paths.Clear();
            if (this.undoLines[this.undoPointer].Count != 0)
            {
                this.LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                foreach (PData pd in this.undoLines[this.undoPointer])
                {
                    PointF[] tmp = new PointF[pd.Lines.Length];
                    Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                    this.paths.Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
                    this.LineList.Items.Add(lineNames[pd.LineType]);
                }
                if (this.undoSelected[this.undoPointer] < this.LineList.Items.Count)
                {
                    this.LineList.SelectedIndex = this.undoSelected[this.undoPointer];
                }

                this.LineList.SelectedValueChanged += LineList_SelectedValueChanged;
            }

            if (this.LineList.SelectedIndex != -1)
            {
                PData selectedPath = this.paths[this.LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
            }
            else
            {
                setUiForPath(this.undoType[this.undoPointer], false, false, false, false);
            }

            this.undoCount--;
            this.undoCount = (this.undoCount < 0) ? 0 : this.undoCount;
            this.redoCount++;

            this.Undo.Enabled = (this.undoCount > 0);
            this.Redo.Enabled = true;
            resetRotation();
            this.canvas.Refresh();
        }

        private void Redo_Click(object sender, EventArgs e)
        {
            if (this.redoCount == 0)
            {
                return;
            }

            this.undoPointer++;
            this.undoPointer += undoMax;
            this.undoPointer %= undoMax;

            this.canvasPoints.Clear();
            if (this.undoPoints[this.undoPointer].Length != 0)
            {
                this.canvasPoints.AddRange(this.undoPoints[this.undoPointer]);
            }

            this.LineList.Items.Clear();
            this.paths.Clear();
            if (this.undoLines[this.undoPointer].Count != 0)
            {
                this.LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                foreach (PData pd in this.undoLines[this.undoPointer])
                {
                    PointF[] tmp = new PointF[pd.Lines.Length];
                    Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                    this.paths.Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
                    this.LineList.Items.Add(lineNames[pd.LineType]);
                }
                if (this.undoSelected[this.undoPointer] < this.LineList.Items.Count)
                {
                    this.LineList.SelectedIndex = this.undoSelected[this.undoPointer];
                }

                this.LineList.SelectedValueChanged += LineList_SelectedValueChanged;
            }

            if (this.LineList.SelectedIndex != -1)
            {
                PData selectedPath = this.paths[this.LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
            }
            else
            {
                setUiForPath(this.undoType[this.undoPointer], false, false, false, false);
            }

            this.undoCount++;
            this.redoCount--;

            this.Redo.Enabled = (this.redoCount > 0);
            this.Undo.Enabled = true;
            resetRotation();
            this.canvas.Refresh();
        }

        private void resetHistory()
        {
            this.undoCount = 0;
            this.redoCount = 0;
            this.undoPointer = 0;
            this.Undo.Enabled = false;
            this.Redo.Enabled = false;
        }
        #endregion

        #region Canvas functions
        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            Image gridImg = Properties.Resources.bg;
            ImageAttributes attr = new ImageAttributes();
            ColorMatrix mx = new ColorMatrix
            {
                Matrix33 = (101f - this.opacitySlider.Value) / 100f
            };
            attr.SetColorMatrix(mx);
            using (TextureBrush texture = new TextureBrush(gridImg, new Rectangle(Point.Empty, gridImg.Size), attr))
            {
                texture.WrapMode = WrapMode.Tile;
                e.Graphics.FillRectangle(texture, e.ClipRectangle);
            }
            attr.Dispose();

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            PointF loopBack = new PointF(-9999, -9999);
            PointF Oldxy = new PointF(-9999, -9999);

            PathType pathType = 0;
            bool isClosed = false;
            bool mpMode = false;
            bool isLarge = false;
            bool revSweep = false;
            PointF[] pPoints;

            int j;
            for (int jj = -1; jj < this.paths.Count; jj++)
            {
                j = jj + 1;
                if (j == this.paths.Count && this.LineList.SelectedIndex == -1)
                {
                    j = -1;
                }

                if (j >= this.paths.Count)
                {
                    continue;
                }

                if (j == this.LineList.SelectedIndex)
                {
                    pPoints = this.canvasPoints.ToArray();
                    pathType = getPathType();
                    isClosed = this.ClosePath.Checked;
                    mpMode = this.CloseContPaths.Checked;
                    isLarge = (this.Arc.CheckState == CheckState.Checked);
                    revSweep = (this.Sweep.CheckState == CheckState.Checked);
                }
                else
                {
                    PData itemPath = this.paths[j];
                    pPoints = itemPath.Lines;
                    pathType = (PathType)itemPath.LineType;
                    isClosed = itemPath.ClosedType;
                    mpMode = itemPath.LoopBack;
                    isLarge = itemPath.IsLarge;
                    revSweep = itemPath.RevSweep;
                }

                if (pPoints.Length == 0)
                {
                    continue;
                }

                PointF[] pts = new PointF[pPoints.Length];
                for (int i = 0; i < pPoints.Length; i++)
                {
                    pts[i].X = this.canvas.ClientSize.Width * pPoints[i].X;
                    pts[i].Y = this.canvas.ClientSize.Height * pPoints[i].Y;
                }

                PointF[] Qpts = Array.Empty<PointF>();
                #region cube to quad
                if (pathType == PathType.Quadratic || pathType == PathType.SmoothQuadratic)
                {
                    Qpts = new PointF[pPoints.Length];

                    for (int i = 0; i < pPoints.Length; i++)
                    {
                        switch (GetNubType(i))
                        {
                            case NubType.StartPoint:
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
                            case NubType.EndPoint:
                                Qpts[i] = pts[i];
                                break;
                        }
                    }
                }
                #endregion

                bool isLinked = true;
                if (!Oldxy.Equals(pts[0]) || (j == this.LineList.SelectedIndex && this.ClosePath.Checked))
                {
                    loopBack = new PointF(pts[0].X, pts[0].Y);
                    isLinked = false;
                }

                #region Draw Nubs
                if (j == this.LineList.SelectedIndex && (Control.ModifierKeys & Keys.Control) != Keys.Control)
                {
                    if ((isClosed || mpMode) && pts.Length > 1)
                    {
                        e.Graphics.DrawRectangle(new Pen(anchorColor), loopBack.X - 4, loopBack.Y - 4, 6, 6);
                    }
                    else if (isLinked)
                    {
                        PointF[] tri = {new PointF(pts[0].X, pts[0].Y - 4f),
                                        new PointF(pts[0].X + 3f, pts[0].Y + 3f),
                                        new PointF(pts[0].X - 4f, pts[0].Y + 3f)};
                        e.Graphics.DrawPolygon(new Pen(anchorColor), tri);
                    }
                    else
                    {
                        e.Graphics.DrawEllipse(new Pen(anchorColor), pts[0].X - 4, pts[0].Y - 4, 6, 6);
                    }

                    for (int i = 1; i < pts.Length; i++)
                    {
                        switch (pathType)
                        {
                            case PathType.Straight:
                                e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                break;
                            case PathType.Ellipse:
                                if (i == 4)
                                {
                                    PointF mid = pointAverage(pts[0], pts[4]);
                                    e.Graphics.DrawEllipse(Pens.Black, pts[4].X - 4, pts[4].Y - 4, 6, 6);
                                    if (!this.MacroCircle.Checked || this.LineList.SelectedIndex != -1)
                                    {
                                        e.Graphics.DrawRectangle(Pens.Black, pts[1].X - 4, pts[1].Y - 4, 6, 6);
                                        e.Graphics.FillEllipse(Brushes.Black, pts[3].X - 4, pts[3].Y - 4, 6, 6);
                                        e.Graphics.FillRectangle(Brushes.Black, pts[2].X - 4, pts[2].Y - 4, 6, 6);

                                        e.Graphics.DrawLine(Pens.Black, mid, pts[1]);
                                        e.Graphics.DrawLine(Pens.Black, mid, pts[2]);
                                        e.Graphics.DrawLine(Pens.Black, mid, pts[3]);
                                    }
                                    e.Graphics.DrawLine(Pens.Black, pts[0], pts[4]);
                                }
                                break;
                            case PathType.Quadratic:
                                if (GetNubType(i) == NubType.ControlPoint1)
                                {
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                    e.Graphics.DrawLine(Pens.Black, pts[i - 1], pts[i]);
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i + 2].X - 4, pts[i + 2].Y - 4, 6, 6);
                                    e.Graphics.DrawLine(Pens.Black, pts[i], pts[i + 2]);
                                }
                                break;
                            case PathType.SmoothQuadratic:
                                if (GetNubType(i) == NubType.EndPoint)
                                {
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                }
                                break;
                            case PathType.Cubic:
                            case PathType.SmoothCubic:
                                if (GetNubType(i) == NubType.ControlPoint1 && !this.MacroCubic.Checked)
                                {
                                    if (i != 1 || pathType == PathType.Cubic)
                                    {
                                        e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                    }

                                    e.Graphics.DrawLine(Pens.Black, pts[i - 1], pts[i]);
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i + 2].X - 4, pts[i + 2].Y - 4, 6, 6);
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i + 1].X - 4, pts[i + 1].Y - 4, 6, 6);
                                    e.Graphics.DrawLine(Pens.Black, pts[i + 1], pts[i + 2]);
                                }
                                else if (GetNubType(i) == NubType.EndPoint && this.MacroCubic.Checked)
                                {
                                    e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                }
                                break;
                        }
                    }
                }
                #endregion

                #region Draw Paths
                using (Pen p = new Pen(lineColors[(int)pathType]))
                using (Pen activePen = new Pen(lineColors[(int)pathType]))
                {
                    p.DashStyle = DashStyle.Solid;
                    p.Width = 1;

                    activePen.Width = 5f;
                    activePen.Color = Color.FromArgb(51, p.Color);
                    activePen.LineJoin = LineJoin.Bevel;

                    switch (pathType)
                    {
                        case PathType.Straight:
                            if (pPoints.Length > 1)
                            {
                                if (this.MacroRect.Checked && j == -1 && this.LineList.SelectedIndex == -1)
                                {
                                    for (int i = 1; i < pts.Length; i++)
                                    {
                                        PointF[] rectPts =
                                        {
                                            new PointF(pts[i - 1].X, pts[i - 1].Y),
                                            new PointF(pts[i].X, pts[i - 1].Y),
                                            new PointF(pts[i].X, pts[i].Y),
                                            new PointF(pts[i - 1].X, pts[i].Y),
                                            new PointF(pts[i - 1].X, pts[i - 1].Y)
                                        };

                                        e.Graphics.DrawLines(p, rectPts);
                                        e.Graphics.DrawLines(activePen, rectPts);
                                    }
                                }
                                else
                                {
                                    e.Graphics.DrawLines(p, pts);
                                    if (j == this.LineList.SelectedIndex)
                                    {
                                        e.Graphics.DrawLines(activePen, pts);
                                    }
                                }
                            }
                            break;
                        case PathType.Ellipse:
                            if (pPoints.Length == 5)
                            {
                                PointF mid = pointAverage(pts[0], pts[4]);
                                if (this.MacroCircle.Checked && j == -1 && this.LineList.SelectedIndex == -1)
                                {
                                    float far = pythag(pts[0], pts[4]);
                                    e.Graphics.DrawEllipse(p, mid.X - far / 2f, mid.Y - far / 2f, far, far);
                                    e.Graphics.DrawEllipse(activePen, mid.X - far / 2f, mid.Y - far / 2f, far, far);
                                }
                                else
                                {
                                    float l = pythag(mid, pts[1]);
                                    float h = pythag(mid, pts[2]);

                                    if ((int)h == 0 || (int)l == 0)
                                    {
                                        PointF[] nullLine = { pts[0], pts[4] };
                                        e.Graphics.DrawLines(p, nullLine);
                                        if (j == this.LineList.SelectedIndex)
                                        {
                                            e.Graphics.DrawLines(activePen, nullLine);
                                        }
                                    }
                                    else
                                    {
                                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);

                                        using (GraphicsPath gp = new GraphicsPath())
                                        {
                                            gp.Add(pts[0], l, h, a, (isLarge) ? 1 : 0, (revSweep) ? 1 : 0, pts[4]);
                                            e.Graphics.DrawPath(p, gp);
                                            if (j == this.LineList.SelectedIndex)
                                            {
                                                e.Graphics.DrawPath(activePen, gp);
                                            }
                                        }

                                        if (j == -1 && (!this.MacroCircle.Checked || this.LineList.SelectedIndex != -1))
                                        {
                                            using (GraphicsPath gp = new GraphicsPath())
                                            {
                                                gp.Add(pts[0], l, h, a, (isLarge) ? 0 : 1, (revSweep) ? 0 : 1, pts[4]);
                                                using (Pen p2 = new Pen(Color.LightGray))
                                                {
                                                    p2.DashStyle = DashStyle.Dash;
                                                    e.Graphics.DrawPath(p2, gp);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case PathType.Cubic:
                        case PathType.SmoothCubic:
                            if (pPoints.Length > 3)
                            {
                                e.Graphics.DrawBeziers(p, pts);
                                if (j == this.LineList.SelectedIndex)
                                {
                                    e.Graphics.DrawBeziers(activePen, pts);
                                }
                            }
                            break;
                        case PathType.Quadratic:
                        case PathType.SmoothQuadratic:
                            if (pPoints.Length > 3)
                            {
                                e.Graphics.DrawBeziers(p, Qpts);
                                if (j == this.LineList.SelectedIndex)
                                {
                                    e.Graphics.DrawBeziers(activePen, Qpts);
                                }
                            }
                            break;
                    }

                    //join line
                    bool join = !(j == -1 && ((this.MacroCircle.Checked && this.Elliptical.Checked) || (this.MacroRect.Checked && this.StraightLine.Checked)));

                    if (!mpMode)
                    {
                        if (join && isClosed && pts.Length > 1)
                        {
                            e.Graphics.DrawLine(p, pts[0], pts[pts.Length - 1]); //preserve
                            if (j == this.LineList.SelectedIndex)
                            {
                                e.Graphics.DrawLine(activePen, pts[0], pts[pts.Length - 1]); //preserve
                            }

                            loopBack = pts[pts.Length - 1];
                        }
                    }
                    else
                    {
                        if (join && pts.Length > 1)
                        {
                            e.Graphics.DrawLine(p, pts[pts.Length - 1], loopBack);
                            if (j == this.LineList.SelectedIndex)
                            {
                                e.Graphics.DrawLine(activePen, pts[pts.Length - 1], loopBack);
                            }

                            loopBack = pts[pts.Length - 1];
                        }
                    }
                }
                #endregion

                Oldxy = pts[pts.Length - 1];
            }

            // render average point for when Scaling and Rotation
            if (this.drawAverage)
            {
                Point tmpPoint = new Point
                {
                    X = (int)Math.Round(this.averagePoint.X * this.canvas.ClientSize.Width),
                    Y = (int)Math.Round(this.averagePoint.Y * this.canvas.ClientSize.Height)
                };
                e.Graphics.DrawLine(Pens.Red, tmpPoint.X - 3, tmpPoint.Y, tmpPoint.X + 3, tmpPoint.Y);
                e.Graphics.DrawLine(Pens.Red, tmpPoint.X, tmpPoint.Y - 3, tmpPoint.X, tmpPoint.Y + 3);
            }
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (this.LineList.SelectedIndex != -1)
            {
                int bottomIndex = Math.Min(this.LineList.TopIndex + (this.LineList.Height / this.LineList.ItemHeight) - 1, this.LineList.Items.Count - 1);
                if (this.LineList.SelectedIndex < this.LineList.TopIndex || this.LineList.SelectedIndex > bottomIndex)
                {
                    this.LineList.TopIndex = this.LineList.SelectedIndex;
                }
            }

            this.moveStart = PointToCanvasCoord(e.X, e.Y);

            //identify node selected
            this.clickedNub = InvalidNub;
            RectangleF hit = new RectangleF(e.X - 4, e.Y - 4, 9, 9);
            for (int i = 0; i < this.canvasPoints.Count; i++)
            {
                PointF p = CanvasCoordToPoint(this.canvasPoints[i].X, this.canvasPoints[i].Y);
                if (hit.Contains(p))
                {
                    this.clickedNub = i;
                    break;
                }
            }

            if (Control.ModifierKeys == Keys.Alt)
            {
                if (this.clickedNub == InvalidNub)
                {
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
                }
                else
                {
                    setUndo();
                }
            }
            else if (e.Button == MouseButtons.Right) //process add or delete
            {
                PathType pathType = getPathType();

                if (this.clickedNub > InvalidNub) //delete
                {
                    #region delete
                    if (this.clickedNub == 0)
                    {
                        return; //don't delete moveto 
                    }

                    setUndo();

                    switch (pathType)
                    {
                        case PathType.Straight:
                            this.canvasPoints.RemoveAt(this.clickedNub);
                            break;
                        case PathType.Ellipse:
                            if (this.clickedNub != 4)
                            {
                                return;
                            }

                            PointF hold = this.canvasPoints[this.canvasPoints.Count - 1];
                            this.canvasPoints.Clear();
                            this.canvasPoints.Add(hold);
                            break;
                        case PathType.Cubic:
                            if (GetNubType(this.clickedNub) != NubType.EndPoint)
                            {
                                return;
                            }

                            this.canvasPoints.RemoveAt(this.clickedNub);
                            //remove control points
                            this.canvasPoints.RemoveAt(this.clickedNub - 1);
                            this.canvasPoints.RemoveAt(this.clickedNub - 2);
                            if (this.MacroCubic.Checked)
                            {
                                CubicAdjust();
                            }

                            break;
                        case PathType.Quadratic:
                            if (GetNubType(this.clickedNub) != NubType.EndPoint)
                            {
                                return;
                            }

                            this.canvasPoints.RemoveAt(this.clickedNub);
                            //remove control points
                            this.canvasPoints.RemoveAt(this.clickedNub - 1);
                            this.canvasPoints.RemoveAt(this.clickedNub - 2);
                            break;
                        case PathType.SmoothCubic:
                            if (GetNubType(this.clickedNub) != NubType.EndPoint)
                            {
                                return;
                            }

                            this.canvasPoints.RemoveAt(this.clickedNub);
                            //remove control points
                            this.canvasPoints.RemoveAt(this.clickedNub - 1);
                            this.canvasPoints.RemoveAt(this.clickedNub - 2);
                            for (int i = 1; i < this.canvasPoints.Count; i++)
                            {
                                if (GetNubType(i) == NubType.ControlPoint1 && i > 3)
                                {
                                    this.canvasPoints[i] = reverseAverage(this.canvasPoints[i - 2], this.canvasPoints[i - 1]);
                                }
                            }
                            break;
                        case PathType.SmoothQuadratic:
                            if (GetNubType(this.clickedNub) != NubType.EndPoint)
                            {
                                return;
                            }

                            this.canvasPoints.RemoveAt(this.clickedNub);
                            //remove control points
                            this.canvasPoints.RemoveAt(this.clickedNub - 1);
                            this.canvasPoints.RemoveAt(this.clickedNub - 2);
                            for (int i = 1; i < this.canvasPoints.Count; i++)
                            {
                                if (GetNubType(i) == NubType.ControlPoint1 && i > 3)
                                {
                                    this.canvasPoints[i] = reverseAverage(this.canvasPoints[i - 3], this.canvasPoints[i - 1]);
                                    if (i < this.canvasPoints.Count - 1)
                                    {
                                        this.canvasPoints[i + 1] = this.canvasPoints[i];
                                    }
                                }
                            }
                            break;
                    }
                    this.canvas.Refresh();
                    #endregion //delete
                }
                else //add new
                {
                    #region add
                    int pointCount = this.canvasPoints.Count;
                    if (pointCount >= maxPoints)
                    {
                        MessageBox.Show($"Too many Nubs in Path (Max is {maxPoints})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (pathType == PathType.Ellipse && this.canvasPoints.Count > 2)
                    {
                        return;
                    }

                    setUndo();

                    int eX = e.X, eY = e.Y;
                    if (this.Snap.Checked)
                    {
                        eX = (int)(Math.Floor((double)(5 + e.X) / 10) * 10);
                        eY = (int)(Math.Floor((double)(5 + e.Y) / 10) * 10);
                    }

                    StatusBarNubLocation(eX, eY);

                    PointF clickedPoint = PointToCanvasCoord(eX, eY);
                    if (pointCount == 0)//first point
                    {
                        this.canvasPoints.Add(clickedPoint);
                    }
                    else//not first point
                    {
                        switch (pathType)
                        {
                            case PathType.Straight:
                                this.canvasPoints.Add(clickedPoint);

                                break;
                            case PathType.Ellipse:
                                PointF[] ellipsePts = new PointF[5];
                                ellipsePts[0] = this.canvasPoints[pointCount - 1];
                                ellipsePts[4] = clickedPoint;
                                PointF mid = pointAverage(ellipsePts[0], ellipsePts[4]);
                                PointF mid2 = ThirdPoint(ellipsePts[0], mid, true, 1f);
                                ellipsePts[1] = pointAverage(ellipsePts[0], mid2);
                                ellipsePts[2] = pointAverage(ellipsePts[4], mid2);
                                ellipsePts[3] = ThirdPoint(ellipsePts[0], mid, false, 1f);

                                this.canvasPoints.Clear();
                                this.canvasPoints.AddRange(ellipsePts);
                                break;

                            case PathType.Cubic:
                                PointF[] cubicPts = new PointF[3];
                                cubicPts[2] = clickedPoint;
                                if (this.MacroCubic.Checked)
                                {
                                    CubicAdjust();
                                }
                                else
                                {
                                    PointF mid4;
                                    if (pointCount > 1)
                                    {
                                        PointF mid3 = reverseAverage(this.canvasPoints[pointCount - 1], this.canvasPoints[pointCount - 2]);
                                        mid4 = AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], cubicPts[2], mid3);
                                    }
                                    else
                                    {
                                        PointF mid3 = pointAverage(this.canvasPoints[pointCount - 1], cubicPts[2]);
                                        mid4 = ThirdPoint(this.canvasPoints[pointCount - 1], mid3, true, 1f);
                                    }
                                    cubicPts[0] = pointAverage(this.canvasPoints[pointCount - 1], mid4);
                                    cubicPts[1] = pointAverage(cubicPts[2], mid4);
                                }
                                this.canvasPoints.AddRange(cubicPts);

                                break;
                            case PathType.Quadratic:
                                PointF[] quadPts = new PointF[3];
                                quadPts[2] = clickedPoint;
                                PointF tmp;
                                //add
                                if (pointCount > 1)
                                {
                                    tmp = AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], quadPts[2], this.canvasPoints[pointCount - 2]);
                                }
                                else
                                {
                                    //add end
                                    quadPts[1] = ThirdPoint(this.canvasPoints[pointCount - 1], quadPts[2], true, .5f);
                                    quadPts[0] = ThirdPoint(quadPts[2], this.canvasPoints[pointCount - 1], false, .5f);
                                    tmp = pointAverage(quadPts[1], quadPts[0]);
                                }
                                quadPts[1] = tmp;
                                quadPts[0] = tmp;
                                this.canvasPoints.AddRange(quadPts);
                                break;

                            case PathType.SmoothCubic:
                                PointF[] sCubicPts = new PointF[3];
                                sCubicPts[2] = clickedPoint;
                                //startchange
                                PointF mid6;
                                if (pointCount > 1)
                                {
                                    PointF mid5 = reverseAverage(this.canvasPoints[pointCount - 1], this.canvasPoints[pointCount - 2]);
                                    mid6 = AsymRevAverage(this.canvasPoints[pointCount - 4], this.canvasPoints[pointCount - 1], sCubicPts[2], mid5);
                                }
                                else
                                {
                                    PointF mid5 = pointAverage(this.canvasPoints[pointCount - 1], sCubicPts[2]);
                                    mid6 = ThirdPoint(this.canvasPoints[pointCount - 1], mid5, true, 1f);
                                }

                                sCubicPts[1] = pointAverage(mid6, sCubicPts[2]);
                                if (pointCount > 1)
                                {
                                    sCubicPts[0] = reverseAverage(this.canvasPoints[pointCount - 2], this.canvasPoints[pointCount - 1]);
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
                                    sQuadPts[0] = reverseAverage(this.canvasPoints[pointCount - 2], this.canvasPoints[pointCount - 1]);
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
                    #endregion //add
                }

                if (this.LineList.SelectedIndex != -1 && this.clickedNub != 0)
                {
                    UpdateExistingPath();
                }
            }
            else if (Control.ModifierKeys == Keys.Shift && e.Button == MouseButtons.Left)
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
                else if (this.LineList.Items.Count > 0)
                {
                    setUndo();
                    this.moveFlag = true;
                    this.canvas.Cursor = Cursors.SizeAll;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (this.clickedNub == InvalidNub)
                {
                    RectangleF bhit = new RectangleF(e.X - 10, e.Y - 10, 20, 20);
                    int clickedPath = getNearestPath(bhit);
                    if (clickedPath != InvalidNub)
                    {
                        this.LineList.SelectedIndex = clickedPath;

                        for (int i = 0; i < this.canvasPoints.Count; i++)
                        {
                            PointF nub = CanvasCoordToPoint(this.canvasPoints[i].X, this.canvasPoints[i].Y);
                            if (bhit.Contains(nub))
                            {
                                StatusBarNubLocation((int)Math.Round(nub.X), (int)Math.Round(nub.Y));
                                break;
                            }
                        }
                    }
                }
                else
                {
                    setUndo();
                    PointF nub = CanvasCoordToPoint(this.canvasPoints[this.clickedNub].X, this.canvasPoints[this.clickedNub].Y);
                    StatusBarNubLocation((int)Math.Round(nub.X), (int)Math.Round(nub.Y));
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
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
            }
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.clickedNub != InvalidNub && this.LineList.SelectedIndex != -1)
            {
                UpdateExistingPath();
            }

            this.panFlag = false;
            this.moveFlag = false;
            this.clickedNub = InvalidNub;
            this.canvas.Refresh();
            this.canvas.Cursor = Cursors.Default;
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            StatusBarMouseLocation(e.X, e.Y);

            int eX = e.X,
                eY = e.Y;
            if (this.Snap.Checked)
            {
                eX = (int)(Math.Floor((double)(5 + eX) / 10) * 10);
                eY = (int)(Math.Floor((double)(5 + eY) / 10) * 10);
            }

            if (!this.canvas.ClientRectangle.Contains(eX, eY))
            {
                eX = eX.Clamp(this.canvas.ClientRectangle.Left, this.canvas.ClientRectangle.Right);
                eY = eY.Clamp(this.canvas.ClientRectangle.Top, this.canvas.ClientRectangle.Bottom);
            }

            PointF mapPoint = PointToCanvasCoord(eX, eY);

            if (e.Button == MouseButtons.Left)
            {
                PathType pathType = getPathType();
                NubType nubType = GetNubType(this.clickedNub);
                int nubIndex = this.clickedNub;

                //left shift move line or path
                if (this.moveFlag && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    if (this.canvasPoints.Count != 0 && nubIndex > InvalidNub && nubIndex < this.canvasPoints.Count)
                    {
                        StatusBarNubLocation(eX, eY);

                        PointF oldp = this.canvasPoints[nubIndex];

                        for (int j = 0; j < this.canvasPoints.Count; j++)
                        {
                            this.canvasPoints[j] = movePoint(oldp, mapPoint, this.canvasPoints[j]);
                        }
                    }
                    else if (this.canvasPoints.Count == 0 && this.LineList.Items.Count > 0)
                    {
                        StatusBarNubLocation(eX, eY);

                        for (int k = 0; k < this.paths.Count; k++)
                        {
                            PointF[] pl = this.paths[k].Lines;
                            for (int j = 0; j < pl.Length; j++)
                            {
                                pl[j] = movePoint(this.moveStart, mapPoint, pl[j]);
                            }
                        }
                        this.moveStart = mapPoint;
                    }
                } //no shift movepoint
                else if (this.canvasPoints.Count != 0 && nubIndex > 0 && nubIndex < this.canvasPoints.Count)
                {
                    StatusBarNubLocation(eX, eY);

                    PointF oldp = this.canvasPoints[nubIndex];
                    switch (pathType)
                    {
                        case PathType.Straight:
                        case PathType.Ellipse:
                            this.canvasPoints[nubIndex] = mapPoint;
                            break;
                        case PathType.Cubic:

                            #region cubic

                            if (nubType == NubType.StartPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if (this.canvasPoints.Count > 1)
                                {
                                    this.canvasPoints[nubIndex + 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }
                            }
                            else if (nubType == NubType.ControlPoint1 || nubType == NubType.ControlPoint2)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                            }
                            else if (nubType == NubType.EndPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                this.canvasPoints[nubIndex - 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                if ((nubIndex + 1) < this.canvasPoints.Count)
                                {
                                    this.canvasPoints[nubIndex + 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }
                            }
                            if (this.MacroCubic.Checked)
                            {
                                CubicAdjust();
                            }

                            #endregion

                            break;
                        case PathType.Quadratic:

                            #region Quadratic

                            if (nubType == NubType.StartPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                            }
                            else if (nubType == NubType.ControlPoint1)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if ((nubIndex + 1) < this.canvasPoints.Count)
                                {
                                    this.canvasPoints[nubIndex + 1] = this.canvasPoints[nubIndex];
                                }
                            }
                            else if (nubType == NubType.ControlPoint2)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if ((nubIndex - 1) > 0)
                                {
                                    this.canvasPoints[nubIndex - 1] = this.canvasPoints[nubIndex];
                                }
                            }
                            else if (nubType == NubType.EndPoint)
                            {
                                if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                {
                                    //online
                                    if (nubIndex == this.canvasPoints.Count - 1)
                                    {
                                        PointF rtmp = reverseAverage(this.canvasPoints[nubIndex - 1], this.canvasPoints[nubIndex]);
                                        this.canvasPoints[nubIndex] = onLinePoint(this.canvasPoints[nubIndex - 1], rtmp, mapPoint);
                                    }
                                    else
                                    {
                                        this.canvasPoints[nubIndex] =
                                            onLinePoint(this.canvasPoints[nubIndex - 1], this.canvasPoints[nubIndex + 1], mapPoint);
                                    }
                                }
                                else
                                {
                                    this.canvasPoints[nubIndex] = mapPoint;
                                }
                            }

                            #endregion

                            break;
                        case PathType.SmoothCubic:

                            #region smooth Cubic

                            if (nubType == NubType.StartPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if (this.canvasPoints.Count > 1)
                                {
                                    this.canvasPoints[nubIndex + 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }

                                this.canvasPoints[1] = this.canvasPoints[0];
                            }
                            else if (nubType == NubType.ControlPoint1)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if (nubIndex > 1)
                                {
                                    this.canvasPoints[nubIndex - 2] = reverseAverage(this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                }
                                else
                                {
                                    this.canvasPoints[1] = this.canvasPoints[0];
                                }
                            }
                            else if (nubType == NubType.ControlPoint2)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                if (nubIndex < this.canvasPoints.Count - 2)
                                {
                                    this.canvasPoints[nubIndex + 2] = reverseAverage(this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }
                            }
                            else if (nubType == NubType.EndPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                                this.canvasPoints[nubIndex - 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex - 1]);
                                if ((nubIndex + 1) < this.canvasPoints.Count)
                                {
                                    this.canvasPoints[nubIndex + 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }
                            }

                            #endregion

                            break;
                        case PathType.SmoothQuadratic:

                            #region Smooth Quadratic

                            if (nubType == NubType.StartPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                            }
                            else if (nubType == NubType.EndPoint)
                            {
                                this.canvasPoints[nubIndex] = mapPoint;
                            }
                            for (int j = 0; j < this.canvasPoints.Count; j++)
                            {
                                if (GetNubType(j) == NubType.ControlPoint1 && j > 1)
                                {
                                    this.canvasPoints[j] = reverseAverage(this.canvasPoints[j - 3], this.canvasPoints[j - 1]);
                                    this.canvasPoints[j + 1] = this.canvasPoints[j];
                                }
                            }

                            #endregion

                            break;
                    }
                } //move first point
                else if (this.canvasPoints.Count != 0 && nubIndex == 0)
                {
                    StatusBarNubLocation(eX, eY);

                    if (nubType == NubType.StartPoint) //special quadratic
                    {
                        switch (pathType)
                        {
                            case PathType.Straight:
                                this.canvasPoints[nubIndex] = mapPoint;
                                break;
                            case PathType.Ellipse:
                                this.canvasPoints[nubIndex] = mapPoint;
                                break;
                            case PathType.Cubic:
                            case PathType.SmoothCubic:
                                this.canvasPoints[nubIndex] = mapPoint;
                                if (this.canvasPoints.Count > 1)
                                {
                                    PointF oldp = this.canvasPoints[nubIndex];
                                    this.canvasPoints[nubIndex + 1] = movePoint(oldp, this.canvasPoints[nubIndex], this.canvasPoints[nubIndex + 1]);
                                }

                                break;
                            case PathType.Quadratic:
                                if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                {
                                    if (this.canvasPoints.Count == 1)
                                    {
                                        this.canvasPoints[nubIndex] = mapPoint;
                                    }
                                    else
                                    {
                                        PointF rtmp = reverseAverage(this.canvasPoints[nubIndex + 1], this.canvasPoints[nubIndex]);
                                        this.canvasPoints[nubIndex] = onLinePoint(this.canvasPoints[nubIndex + 1], rtmp, mapPoint);
                                    }
                                }
                                else
                                {
                                    this.canvasPoints[nubIndex] = mapPoint;
                                }
                                break;
                            case PathType.SmoothQuadratic:
                                this.canvasPoints[0] = mapPoint;
                                if (this.canvasPoints.Count > 1)
                                {
                                    this.canvasPoints[1] = mapPoint;
                                }

                                for (int j = 0; j < this.canvasPoints.Count; j++)
                                {
                                    if (GetNubType(j) == NubType.ControlPoint1 && j > 1)
                                    {
                                        this.canvasPoints[j] =
                                            reverseAverage(this.canvasPoints[j - 3], this.canvasPoints[j - 1]);
                                        this.canvasPoints[j + 1] = this.canvasPoints[j];
                                    }
                                }
                                break;
                        }
                    }
                } //Pan zoomed
                else if (this.panFlag)
                {
                    Pan();
                }

                this.canvas.Refresh();
            }
            else if (e.Button == MouseButtons.Middle && this.panFlag)
            {
                Pan();
            }

            void Pan()
            {
                int mpx = (int)(mapPoint.X * 100);
                int msx = (int)(this.moveStart.X * 100);
                int mpy = (int)(mapPoint.Y * 100);
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
        #endregion

        #region Utility functions
        private static PointF[] GetFirstControlPoints(PointF[] rhs)
        {
            int n = rhs.Length;
            PointF[] x = new PointF[n]; // Solution vector.
            float[] tmp = new float[n]; // Temp workspace.

            float b = 2.0f;
            x[0] = new PointF(rhs[0].X / b, rhs[0].Y / b);

            for (int i = 1; i < n; i++) // Decomposition and forward substitution.
            {
                tmp[i] = 1f / b;
                b = (i < n - 1 ? 4.0f : 3.5f) - tmp[i];
                x[i].X = (rhs[i].X - x[i - 1].X) / b;
                x[i].Y = (rhs[i].Y - x[i - 1].Y) / b;
            }
            for (int i = 1; i < n; i++)
            {
                x[n - i - 1].X -= tmp[n - i] * x[n - i].X; // Backsubstitution.
                x[n - i - 1].Y -= tmp[n - i] * x[n - i].Y;
            }
            return x;
        }

        private static PointF onLinePoint(PointF sp, PointF ep, PointF mt)
        {
            PointF xy = new PointF(sp.X, sp.Y);
            float dist = 9999;

            for (float i = 0; i < 1; i += .001f)
            {
                PointF test = new PointF(ep.X * i + sp.X - sp.X * i, ep.Y * i + sp.Y - sp.Y * i);

                float tmp = pythag(mt, test);
                if (tmp < dist)
                {
                    dist = tmp;
                    xy = new PointF(test.X, test.Y);
                }
            }

            return xy;
        }

        private static PointF movePoint(PointF orig, PointF dest, PointF target)
        {
            return new PointF
            {
                X = target.X + (dest.X - orig.X),
                Y = target.Y + (dest.Y - orig.Y)
            };
        }

        private static PointF ThirdPoint(PointF p1, PointF p2, bool flip, float curve)
        {
            float Shift = (float)(1f / Math.Sqrt(3));
            float x3, y3;
            if (!flip)
            {
                x3 = p2.X + Shift * (p1.Y - p2.Y);
                y3 = p2.Y + Shift * (p2.X - p1.X);
            }
            else
            {
                x3 = p2.X + Shift * (p2.Y - p1.Y);
                y3 = p2.Y + Shift * (p1.X - p2.X);
            }
            x3 = (x3 - p2.X) * curve + p2.X;
            y3 = (y3 - p2.Y) * curve + p2.Y;
            return new PointF(x3, y3);
        }

        private static PointF reverseAverage(PointF p1, PointF p2)
        {
            return new PointF
            {
                X = p2.X * 2f - p1.X,
                Y = p2.Y * 2f - p1.Y
            };
        }

        private static PointF AsymRevAverage(PointF p0, PointF p1, PointF p2, PointF c1)
        {
            PointF tmp = reverseAverage(c1, p1);
            float py1 = pythag(p0, p1);
            float py2 = pythag(p2, p1);
            float norm = (py1 + py2) / (py1 * 2f + .00001f);
            tmp.X = (tmp.X - c1.X) * norm + c1.X;
            tmp.Y = (tmp.Y - c1.Y) * norm + c1.Y;
            return tmp;
        }

        private static PointF pointAverage(PointF p1, PointF p2)
        {
            return new PointF
            {
                X = (p2.X + p1.X) / 2f,
                Y = (p2.Y + p1.Y) / 2f
            };
        }

        private static float pythag(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
        #endregion

        #region Rotation Knob functions
        private void resetRotation()
        {
            this.RotationKnob.Value = 180;
            this.toolTip1.SetToolTip(this.RotationKnob, "0.0\u00B0");
            this.lastRot = 180;
        }

        private void RotationKnob_ValueChanged(object sender, EventArgs e)
        {
            this.toolTip1.SetToolTip(this.RotationKnob, $"{this.RotationKnob.Value - 180f:0.0}\u00B0");

            double rad = (this.lastRot - this.RotationKnob.Value) * Math.PI / 180;
            this.lastRot = this.RotationKnob.Value;

            if (this.canvasPoints.Count == 0 && this.LineList.Items.Count > 0)
            {
                this.averagePoint = new PointF(.5f, .5f);
                for (int k = 0; k < this.paths.Count; k++)
                {
                    PointF[] tmp = this.paths[k].Lines;

                    for (int i = 0; i < tmp.Length; i++)
                    {
                        double x = tmp[i].X - this.averagePoint.X;
                        double y = tmp[i].Y - this.averagePoint.Y;
                        double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + this.averagePoint.X;
                        double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + this.averagePoint.Y;

                        tmp[i] = new PointF((float)nx, (float)ny);
                    }
                }
            }
            else if (this.canvasPoints.Count > 1)
            {
                PointF[] tmp = this.canvasPoints.ToArray();
                this.averagePoint = tmp.Average();

                for (int i = 0; i < tmp.Length; i++)
                {
                    double x = tmp[i].X - this.averagePoint.X;
                    double y = tmp[i].Y - this.averagePoint.Y;
                    double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + this.averagePoint.X;
                    double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + this.averagePoint.Y;

                    tmp[i] = new PointF((float)nx, (float)ny);
                }

                this.canvasPoints.Clear();
                this.canvasPoints.AddRange(tmp);

                if (this.LineList.SelectedIndex != -1)
                {
                    UpdateExistingPath();
                }
            }

            this.canvas.Refresh();
        }

        private void RotationKnob_MouseDown(object sender, MouseEventArgs e)
        {
            this.drawAverage = true;
            setUndo();
            this.toolTip1.Show($"{this.RotationKnob.Value:0.0}\u00B0", this.RotationKnob);
        }

        private void RotationKnob_MouseUp(object sender, MouseEventArgs e)
        {
            this.drawAverage = false;
            this.canvas.Refresh();
            this.toolTip1.Hide(this.RotationKnob);
        }
        #endregion

        #region Misc Helper functions
        private void UpdateExistingPath()
        {
            this.paths[this.LineList.SelectedIndex] = new PData(this.canvasPoints.ToArray(), this.ClosePath.Checked, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked),
                (this.Sweep.CheckState == CheckState.Checked), this.paths[this.LineList.SelectedIndex].Alias, this.CloseContPaths.Checked);
            this.LineList.Items[this.LineList.SelectedIndex] = lineNames[(int)getPathType()];
        }

        private void AddNewPath(bool deSelected = false)
        {
            if (this.canvasPoints.Count <= 1)
            {
                return;
            }

            if (this.paths.Count < maxPaths)
            {
                setUndo(deSelected);
                if (this.MacroCircle.Checked && getPathType() == PathType.Ellipse)
                {
                    if (this.canvasPoints.Count < 5)
                    {
                        return;
                    }

                    PointF mid = pointAverage(this.canvasPoints[0], this.canvasPoints[4]);
                    this.canvasPoints[1] = this.canvasPoints[0];
                    this.canvasPoints[2] = this.canvasPoints[4];
                    this.canvasPoints[3] = mid;
                    this.paths.Add(new PData(this.canvasPoints.ToArray(), false, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked), (this.Sweep.CheckState == CheckState.Checked), string.Empty, false));
                    this.LineList.Items.Add(lineNames[(int)PathType.Ellipse]);
                    PointF[] tmp = new PointF[this.canvasPoints.Count];
                    //fix
                    tmp[0] = this.canvasPoints[4];
                    tmp[4] = this.canvasPoints[0];
                    tmp[3] = this.canvasPoints[3];
                    tmp[1] = tmp[0];
                    tmp[2] = tmp[4];
                    //test below
                    this.paths.Add(new PData(tmp, false, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked), (this.Sweep.CheckState == CheckState.Checked), string.Empty, true));
                    this.LineList.Items.Add(lineNames[(int)PathType.Ellipse]);
                }
                else if (this.MacroRect.Checked && getPathType() == PathType.Straight)
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

                        this.paths.Add(new PData(tmp, false, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked), (this.Sweep.CheckState == CheckState.Checked), string.Empty, false));
                        this.LineList.Items.Add(lineNames[(int)getPathType()]);
                    }
                }
                else
                {
                    this.paths.Add(new PData(this.canvasPoints.ToArray(), this.ClosePath.Checked, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked), (this.Sweep.CheckState == CheckState.Checked), string.Empty, this.CloseContPaths.Checked));
                    this.LineList.Items.Add(lineNames[(int)getPathType()]);
                }
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            resetRotation();

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

            this.canvas.Refresh();
        }

        private void Deselect()
        {
            if (this.LineList.SelectedIndex == -1 && this.canvasPoints.Count > 1)
            {
                setUndo();
            }

            this.canvasPoints.Clear();
            this.LineList.SelectedIndex = -1;
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

                PointF[] xy = GetFirstControlPoints(rhs);

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

        private static NubType GetNubType(int nubIndex)
        {
            if (nubIndex == 0)
            {
                return NubType.StartPoint;
            }

            int nubType = ((nubIndex - 1) % 3) + 1;
            return (NubType)nubType;
        }

        private PathType getPathType()
        {
            return this.activeType;
        }

        private void setUiForPath(PathType pathType, bool closedPath, bool largeArc, bool revSweep, bool multiClosedPath)
        {
            SuspendLayout();
            this.MacroCubic.Checked = false;
            this.MacroCircle.Checked = false;
            this.MacroRect.Checked = false;
            if (pathType != this.activeType)
            {
                this.activeType = pathType;
                PathTypeToggle();
            }
            this.ClosePath.Checked = closedPath;
            this.ClosePath.Image = (this.ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            this.CloseContPaths.Checked = multiClosedPath;
            this.CloseContPaths.Image = (this.CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;
            if (pathType == PathType.Ellipse)
            {
                this.Arc.CheckState = largeArc ? CheckState.Checked : CheckState.Indeterminate;
                this.Arc.Image = (this.Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcSmall : Properties.Resources.ArcLarge;

                this.Sweep.CheckState = revSweep ? CheckState.Checked : CheckState.Indeterminate;
                this.Sweep.Image = (this.Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;
            }
            ResumeLayout();
        }

        private int getNearestPath(RectangleF hit)
        {
            if (this.LineList.Items.Count == 0)
            {
                return -1;
            }

            int pathIndex = -1;
            for (int i = 0; i < this.LineList.Items.Count; i++)
            {
                PointF[] tmp = this.paths[i].Lines;

                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddLines(tmp);

                    gp.Flatten(null, .1f);
                    tmp = gp.PathPoints;
                    for (int j = 0; j < tmp.Length; j++)
                    {
                        // exclude 'control' nubs.
                        switch ((PathType)this.paths[i].LineType)
                        {
                            case PathType.Ellipse: // Ellipse (Red)
                                if (j % 4 != 0)
                                {
                                    continue;
                                }

                                break;
                            case PathType.Cubic: // Cubic (Blue)
                            case PathType.SmoothCubic: // Smooth Cubic (Green)
                            case PathType.Quadratic: // Quadratic (Goldenrod)
                                if (j % 3 != 0)
                                {
                                    continue;
                                }

                                break;
                        }

                        PointF p = CanvasCoordToPoint(tmp[j].X, tmp[j].Y);
                        if (hit.Contains(p))
                        {
                            pathIndex = i;
                            break;
                        }
                    }
                    if (pathIndex > -1)
                    {
                        break;
                    }
                }
            }
            return pathIndex;
        }

        private void StatusBarMouseLocation(int x, int y)
        {
            int zoomFactor = this.canvas.Width / this.canvasBaseSize;
            this.statusLabelMousePos.Text = $"{Math.Round(x / (float)zoomFactor / dpiScale)}, {Math.Round(y / (float)zoomFactor / dpiScale)}";
            this.statusStrip1.Refresh();
        }

        private void StatusBarNubLocation(int x, int y)
        {
            int zoomFactor = this.canvas.Width / this.canvasBaseSize;
            this.statusLabelNubPos.Text = $"{Math.Round(x / (float)zoomFactor / dpiScale)}, {Math.Round(y / (float)zoomFactor / dpiScale)}";
            this.statusStrip1.Refresh();
        }

        private bool getPathData(float width, float height, out string output)
        {
            string strPath = (this.SolidFillMenuItem.Checked) ? "F1 " : string.Empty;
            if (this.paths.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;

            for (int index = 0; index < this.paths.Count; index++)
            {
                PData currentPath = this.paths[index];
                PathType pathType = (PathType)currentPath.LineType;
                PointF[] line = currentPath.Lines;
                bool islarge = currentPath.IsLarge;
                bool revsweep = currentPath.RevSweep;
                if (line.Length < 2)
                {
                    continue;
                }
                float x, y;

                x = width * line[0].X;
                y = height * line[0].Y;

                if (index == 0 || (x != oldx || y != oldy) || currentPath.ClosedType)
                {
                    if (index > 0)
                    {
                        strPath += " ";
                    }

                    strPath += "M ";
                    strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                    strPath += ",";
                    strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                }

                switch (pathType)
                {
                    case PathType.Straight:
                        strPath += " L ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                            strPath += ",";
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                            if (i < line.Length - 1)
                            {
                                strPath += ",";
                            }
                        }
                        oldx = x; oldy = y;
                        break;
                    case PathType.Ellipse:
                        strPath += " A ";
                        PointF[] pts = new PointF[line.Length];
                        for (int i = 0; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            pts[i] = new PointF(x, y);
                        }
                        PointF mid = pointAverage(pts[0], pts[4]);
                        float l = pythag(mid, pts[1]);
                        float h = pythag(mid, pts[2]);
                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                        float b = (islarge) ? 1 : 0;
                        float s = (revsweep) ? 1 : 0;
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", l);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", h);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", a);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0}", b);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0}", s);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", pts[4].X);
                        strPath += ",";
                        strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", pts[4].Y);
                        oldx = pts[4].X;
                        oldy = pts[4].Y;
                        break;
                    case PathType.Cubic:
                        strPath += " C ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                            strPath += ",";
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                            if (i < line.Length - 1)
                            {
                                strPath += ",";
                            }

                            oldx = x; oldy = y;
                        }
                        break;
                    case PathType.Quadratic:
                        strPath += " Q ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (GetNubType(i) != NubType.ControlPoint2)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                {
                                    strPath += ",";
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case PathType.SmoothCubic:
                        strPath += " S ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (GetNubType(i) != NubType.ControlPoint1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                {
                                    strPath += ",";
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case PathType.SmoothQuadratic:
                        strPath += " T ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (GetNubType(i) != NubType.ControlPoint2 && GetNubType(i) != NubType.ControlPoint1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                {
                                    strPath += ",";
                                }

                                oldx = x; oldy = y;
                            }
                        }
                        break;
                }

                if (currentPath.ClosedType || currentPath.LoopBack)
                {
                    strPath += " Z";
                    if (currentPath.ClosedType)
                    {
                        oldx += 10;
                        oldy += 10;
                    }
                }
            }
            output = strPath;
            return true;
        }

        private bool getPGPathData(float width, float height, out string output)
        {
            string strPath = string.Empty;
            if (this.paths.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;
            string[] repstr = { "~1", "~2", "~3" };
            string tmpstr = string.Empty;
            for (int index = 0; index < this.paths.Count; index++)
            {
                Application.DoEvents();

                PData currentPath = this.paths[index];
                PathType pathType = (PathType)currentPath.LineType;
                PointF[] line = currentPath.Lines;
                bool islarge = currentPath.IsLarge;
                bool revsweep = currentPath.RevSweep;
                if (line.Length < 2)
                {
                    continue;
                }
                float x, y;

                x = width * line[0].X;
                y = height * line[0].Y;

                if (index == 0)
                {
                    strPath += $"\t\t\t\t{Properties.Resources.PGMove}\r\n";
                    strPath = strPath.Replace("~1", $"{x:0.##},{y:0.##}");
                }
                else if (currentPath.ClosedType || (x != oldx || y != oldy))//mod 091515
                {
                    strPath = strPath.Replace("~0", "False");
                    strPath += "\t\t\t\t</PathFigure>\r\n";
                    strPath += $"\t\t\t\t{Properties.Resources.PGMove}\r\n";
                    strPath = strPath.Replace("~1", $"{x:0.##},{y:0.##}");
                }

                switch (pathType)
                {
                    case PathType.Straight:
                        tmpstr = string.Empty;
                        for (int i = 1; i < line.Length; i++)
                        {
                            strPath += $"\t\t\t\t\t{Properties.Resources.PGLine}\r\n";
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";

                            strPath = strPath.Replace("~1", tmpstr);
                        }

                        oldx = x; oldy = y;
                        break;
                    case PathType.Ellipse:
                        strPath += $"\t\t\t\t\t{Properties.Resources.PGEllipse}\r\n";
                        PointF[] pts = new PointF[line.Length];
                        for (int i = 0; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            pts[i] = new PointF(x, y);
                        }
                        PointF mid = pointAverage(pts[0], pts[4]);
                        float l = pythag(mid, pts[1]);
                        float h = pythag(mid, pts[2]);
                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                        float b = (islarge) ? 1 : 0;
                        float s = (revsweep) ? 1 : 0;

                        tmpstr = $"{l:0.##}";
                        tmpstr += ",";
                        tmpstr += $"{h:0.##}";
                        strPath = strPath.Replace("~1", tmpstr);
                        strPath = strPath.Replace("~2", $"{a:0.##}");
                        strPath = strPath.Replace("~3", (b == 1) ? "True" : "False");
                        strPath = strPath.Replace("~4", (s == 1) ? "Clockwise" : "CounterClockwise");

                        tmpstr = $"{pts[4].X:0.##},{pts[4].Y:0.##}";
                        strPath = strPath.Replace("~5", tmpstr);
                        oldx = pts[4].X; oldy = pts[4].Y;
                        break;
                    case PathType.SmoothCubic:
                    case PathType.Cubic:

                        for (int i = 1; i < line.Length - 1; i += 3)
                        {
                            strPath += $"\t\t\t\t\t{Properties.Resources.PGBezier}\r\n";
                            for (int j = 0; j < 3; j++)
                            {
                                x = width * line[j + i].X;
                                y = height * line[j + i].Y;
                                tmpstr = $"{x:0.##},{y:0.##}";
                                strPath = strPath.Replace(repstr[j], tmpstr);
                            }
                        }

                        oldx = x; oldy = y;
                        break;
                    case PathType.SmoothQuadratic:
                    case PathType.Quadratic:

                        for (int i = 1; i < line.Length - 1; i += 3)
                        {
                            strPath += $"\t\t\t\t\t{Properties.Resources.PQQuad}\r\n";

                            x = width * line[i].X;
                            y = height * line[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~1", tmpstr);
                            x = width * line[i + 2].X;
                            y = height * line[i + 2].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~2", tmpstr);
                        }

                        oldx = x; oldy = y;
                        break;
                }

                if (currentPath.ClosedType || currentPath.LoopBack)
                {
                    strPath = strPath.Replace("~0", "True");
                    oldx += 10;
                    oldy += 10;
                }
            }
            strPath += "\t\t\t\t</PathFigure>\r\n";
            strPath = strPath.Replace("~0", "False");
            strPath += "\r\n";
            output = strPath;

            return true;
        }

        private static string scrubNums(string strPath)
        {
            const string command = "fmlacsqthvz";
            const string number = "e.-0123456789";
            string TMP = string.Empty;
            bool alpha = false;
            bool blank = false;

            char[] strChars = strPath.ToLower().Replace(',', ' ').ToCharArray();
            for (int i = 0; i < strChars.Length; i++)
            {
                char mychar = strChars[i];
                bool isNumber = number.IndexOf(mychar) > -1;
                bool isCommand = command.IndexOf(mychar) > -1;

                if (TMP.Length == 0)
                {
                    TMP += mychar;
                    alpha = true;
                    blank = false;
                }
                else if (mychar.Equals(' '))
                {
                    alpha = true;
                    blank = true;
                }
                else if (isCommand && (!alpha || blank))
                {
                    TMP += "," + mychar;
                    alpha = true;
                    blank = false;
                }
                else if (isCommand)
                {
                    TMP += mychar;
                    alpha = true;
                    blank = false;
                }
                else if (isNumber && (alpha || blank))
                {
                    TMP += "," + mychar;
                    alpha = false;
                    blank = false;
                }
                else if (isNumber)
                {
                    TMP += mychar;
                    alpha = false;
                    blank = false;
                }
            }
            return TMP;
        }

        private void parsePathData(string strPath)
        {
            if (strPath.Length == 0)
            {
                return;
            }

            PointF[] pts = Array.Empty<PointF>();
            int lineType = -1;
            bool closedType = false;
            bool mpmode = false;
            bool islarge = true;
            bool revsweep = false;
            PointF LastPos = new PointF();
            PointF HomePos = new PointF();

            //cook data
            strPath = strPath.Trim();
            strPath = scrubNums(strPath);
            string[] str = strPath.Split(',');

            //parse
            string strMode = string.Empty;
            const string match = "fmlacsqthvz";
            bool errorflagx = false;
            bool errorflagy = false;
            float x = 0, y = 0;
            for (int i = 0; i < str.Length; i++)
            {
                errorflagx = true; errorflagy = true;
                if (match.Contains(str[i]))
                {
                    strMode = str[i];
                    int tmpline = match.IndexOf(strMode, StringComparison.Ordinal);
                    tmpline = (tmpline > 7) ? 0 : (tmpline > 1) ? tmpline - 2 : -1;
                    if (tmpline != -1)
                    {
                        if (pts.Length > 1 && this.LineList.Items.Count < maxPaths)
                        {
                            addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);
                        }

                        Array.Resize(ref pts, 1);
                        pts[0] = LastPos;

                        lineType = tmpline;
                        closedType = false;
                    }
                    if (strMode != "z")
                    {
                        continue;
                    }
                }
                NubType ptype;
                int len = 0;

                // https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/path-markup-syntax
                switch (strMode)
                {
                    case "n":
                    case "z":
                        Array.Resize(ref pts, pts.Length + 1);
                        pts[pts.Length - 1] = HomePos;
                        break;
                    case "f":
                        errorflagx = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        this.SolidFillMenuItem.Checked = (x == 1);
                        break;
                    case "m":
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        HomePos = LastPos;
                        break;

                    case "c":
                    case "l":
                        Array.Resize(ref pts, pts.Length + 1);
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "s":
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        len = pts.Length;
                        Array.Resize(ref pts, len + 1);
                        ptype = GetNubType(len);
                        if (len > 1)
                        {
                            if (ptype == NubType.ControlPoint1)
                            {
                                Array.Resize(ref pts, len + 2);
                                pts[len + 1] = LastPos;
                                pts[len] = reverseAverage(pts[len - 2], pts[len - 1]);
                            }
                            else if (ptype == NubType.EndPoint)
                            {
                                pts[len] = LastPos;
                            }
                        }
                        else
                        {
                            pts[1] = pts[0];
                            Array.Resize(ref pts, len + 2);
                            pts[2] = LastPos;
                        }

                        break;
                    case "t":
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        len = pts.Length;
                        Array.Resize(ref pts, len + 3);
                        pts[len + 2] = LastPos;
                        if (len > 1)
                        {
                            pts[len] = reverseAverage(pts[len - 2], pts[len - 1]);
                            pts[len + 1] = pts[len];
                        }
                        else
                        {
                            pts[1] = pts[0];
                            pts[2] = pts[0];
                        }
                        break;
                    case "q":
                        Array.Resize(ref pts, pts.Length + 1);
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        //
                        ptype = GetNubType(pts.Length - 1);
                        if (ptype == NubType.ControlPoint1)
                        {
                            Array.Resize(ref pts, pts.Length + 1);
                            pts[pts.Length - 1] = LastPos;
                        }

                        break;
                    case "h":
                        Array.Resize(ref pts, pts.Length + 1);
                        y = LastPos.Y;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        x = x / this.canvas.ClientSize.Height;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "v":
                        Array.Resize(ref pts, pts.Length + 1);
                        x = LastPos.X;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        y = y / this.canvas.ClientSize.Height;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "a":
                        int ptbase = 0;
                        Array.Resize(ref pts, pts.Length + 4);
                        errorflagx = float.TryParse(str[i + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i + 6], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy)
                        {
                            break;
                        }

                        LastPos = PointToCanvasCoord(x, y);
                        pts[ptbase + 4] = LastPos; //ENDPOINT

                        PointF From = CanvasCoordToPoint(pts[ptbase].X, pts[ptbase].Y);
                        PointF To = new PointF(x, y);

                        PointF mid = pointAverage(From, To);
                        PointF mid2 = ThirdPoint(From, mid, true, 1f);
                        float far = pythag(From, mid);
                        float atan = (float)Math.Atan2(mid2.Y - mid.Y, mid2.X - mid.X);

                        float dist, dist2;
                        errorflagx = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //W
                        if (!errorflagx)
                        {
                            break;
                        }

                        pts[ptbase + 1] = pointOrbit(mid, atan - (float)Math.PI / 4f, dist);

                        errorflagx = float.TryParse(str[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //H
                        if (!errorflagx)
                        {
                            break;
                        }

                        pts[ptbase + 2] = pointOrbit(mid, atan + (float)Math.PI / 4f, dist);
                        errorflagx = float.TryParse(str[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                        float rot = dist * (float)Math.PI / 180f; //ROT
                        pts[ptbase + 3] = pointOrbit(mid, rot, far);
                        errorflagx = float.TryParse(str[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                        if (!errorflagx)
                        {
                            break;
                        }

                        errorflagy = float.TryParse(str[i + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out dist2);
                        if (!errorflagy)
                        {
                            break;
                        }

                        islarge = Convert.ToBoolean(dist);
                        revsweep = Convert.ToBoolean(dist2);

                        i += 6;
                        strMode = "n";
                        break;
                }
                if (!errorflagx || !errorflagy)
                {
                    break;
                }
            }
            if (!errorflagx || !errorflagy || lineType < 0)
            {
                MessageBox.Show("No Line Type, or is not in the StreamGeometry Format", "Not a valid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (pts.Length > 1 && this.LineList.Items.Count < maxPaths)
            {
                addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);
            }

            this.canvas.Refresh();
        }

        private PointF pointOrbit(PointF center, float rotation, float distance)
        {
            float x = (float)Math.Cos(rotation) * distance;
            float y = (float)Math.Sin(rotation) * distance;
            return PointToCanvasCoord(center.X + x, center.Y + y);
        }

        private PointF PointToCanvasCoord(float x, float y)
        {
            return new PointF(x / this.canvas.ClientSize.Width, y / this.canvas.ClientSize.Height);
        }

        private PointF CanvasCoordToPoint(float x, float y)
        {
            return new PointF(x * this.canvas.ClientSize.Width, y * this.canvas.ClientSize.Height);
        }

        private void addPathtoList(PointF[] pbpoint, int lineType, bool closedType, bool islarge, bool revsweep, bool mpmtype)
        {
            if (this.paths.Count < maxPaths)
            {
                this.paths.Add(new PData(pbpoint, closedType, lineType, islarge, revsweep, string.Empty, mpmtype));
                this.LineList.Items.Add(lineNames[lineType]);
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearAllPaths()
        {
            this.canvasPoints.Clear();
            this.statusLabelNubsUsed.Text = $"{this.canvasPoints.Count}/{maxPoints} Nubs used";
            this.statusLabelNubPos.Text = "0, 0";

            this.paths.Clear();
            this.LineList.Items.Clear();
            this.statusLabelPathsUsed.Text = $"{this.LineList.Items.Count}/{maxPaths} Paths used";

            this.canvas.Refresh();
        }

#if PDNPLUGIN
        private void MakePathForPdnCanvas()
        {
            PointF loopBack = new PointF(-9999, -9999);
            PointF Oldxy = new PointF(-9999, -9999);

            Rectangle selection = this.Selection.GetBoundsInt();
            int selMinDim = Math.Min(selection.Width, selection.Height);

            this.pathForPdnCanvas = new GraphicsPath[this.paths.Count];

            for (int j = 0; j < this.paths.Count; j++)
            {
                PData currentPath = this.paths[j];
                PointF[] pathPoints = currentPath.Lines;
                PathType pathType = (PathType)currentPath.LineType;

                PointF[] pts = new PointF[pathPoints.Length];
                PointF[] Qpts = new PointF[pathPoints.Length];

                for (int i = 0; i < pathPoints.Length; i++)
                {
                    pts[i].X = (float)this.OutputScale.Value * selMinDim / 100f * pathPoints[i].X + selection.Left;
                    pts[i].Y = (float)this.OutputScale.Value * selMinDim / 100f * pathPoints[i].Y + selection.Top;
                }

                #region cube to quad
                if (pathType == PathType.Quadratic || pathType == PathType.SmoothQuadratic)
                {
                    for (int i = 0; i < pathPoints.Length; i++)
                    {
                        switch (GetNubType(i))
                        {
                            case NubType.StartPoint:
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
                            case NubType.EndPoint:
                                Qpts[i] = pts[i];
                                break;
                        }
                    }
                }
                #endregion

                if (pts.Length > 0 && !Oldxy.Equals(pts[0]))
                {
                    loopBack = new PointF(pts[0].X, pts[0].Y);
                }
                //render lines

                #region drawlines
                this.pathForPdnCanvas[j] = new GraphicsPath();

                switch (pathType)
                {
                    case PathType.Straight:
                        if (pathPoints.Length > 1)
                        {
                            this.pathForPdnCanvas[j].AddLines(pts);
                        }
                        break;
                    case PathType.Ellipse:
                        if (pathPoints.Length == 5)
                        {
                            PointF mid = pointAverage(pts[0], pts[4]);
                            float l = pythag(mid, pts[1]);
                            float h = pythag(mid, pts[2]);
                            if ((int)h == 0 || (int)l == 0)
                            {
                                PointF[] nullLine = { pts[0], pts[4] };
                                this.pathForPdnCanvas[j].AddLines(nullLine);
                            }
                            else
                            {
                                float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                                this.pathForPdnCanvas[j].Add(pts[0], l, h, a, (currentPath.IsLarge) ? 1 : 0, (currentPath.RevSweep) ? 1 : 0, pts[4]);
                            }
                        }
                        break;
                    case PathType.Cubic:
                    case PathType.SmoothCubic:
                        if (pathPoints.Length > 3)
                        {
                            this.pathForPdnCanvas[j].AddBeziers(pts);
                        }
                        break;
                    case PathType.Quadratic:
                    case PathType.SmoothQuadratic:
                        if (pathPoints.Length > 3)
                        {
                            this.pathForPdnCanvas[j].AddBeziers(Qpts);
                        }
                        break;
                }

                if (!currentPath.LoopBack)
                {
                    if (currentPath.ClosedType && pts.Length > 1)
                    {
                        PointF[] points = { pts[pts.Length - 1], pts[0] };
                        this.pathForPdnCanvas[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                else
                {
                    if (pts.Length > 1)
                    {
                        PointF[] points = { pts[pts.Length - 1], loopBack };
                        this.pathForPdnCanvas[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                #endregion
                Oldxy = pts[pts.Length - 1];
            }
        }
#endif
        private bool InView()
        {
            if (this.canvasPoints.Any(pt => pt.X > 1.5f || pt.Y > 1.5f))
            {
                return false;
            }

            foreach (PData pathData in this.paths)
            {
                if (pathData.Lines.Any(pt => pt.X > 1.5f || pt.Y > 1.5f))
                {
                    return false;
                }
            }

            return true;
        }

        private void LoadProjectFile(string projectPath)
        {
            List<PData> projectPaths = null;
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                using (FileStream stream = File.OpenRead(projectPath))
                {
                    projectPaths = new List<PData>(((ArrayList)ser.Deserialize(stream)).Cast<PData>());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Incorrect Format\r\n" + ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (projectPaths == null || projectPaths.Count == 0)
            {
                MessageBox.Show("Incorrect Format", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (projectPaths.Count > maxPaths)
            {
                MessageBox.Show($"Too many Paths in project file. (Max is {maxPaths})", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ClearAllPaths();

            PData documentProps = projectPaths[projectPaths.Count - 1];
            this.FigureName.Text = documentProps.Meta;
            this.SolidFillMenuItem.Checked = documentProps.SolidFill;
            foreach (PData path in projectPaths)
            {
                this.paths.Add(path);
                this.LineList.Items.Add(lineNames[path.LineType]);
            }

            ZoomToFactor(1);
            resetRotation();
            resetHistory();
            this.canvas.Refresh();
            AddToRecents(projectPath);
        }
        #endregion

        #region Path List functions
        private void LineList_DoubleClick(object sender, EventArgs e)
        {
            if (this.LineList.Items.Count == 0 || this.LineList.SelectedItem == null)
            {
                return;
            }

            string s = Microsoft.VisualBasic.Interaction.InputBox("Please enter a name for this path.", "Path Name", this.LineList.SelectedItem.ToString(), -1, -1).Trim();
            if (s.Length > 0)
            {
                this.paths[this.LineList.SelectedIndex].Alias = s;
            }
        }

        private void LineList_SelectedValueChanged(object sender, EventArgs e)
        {
            if (this.isNewPath && this.canvasPoints.Count > 1)
            {
                AddNewPath(true);
            }

            this.isNewPath = false;

            if (this.LineList.SelectedIndex == -1)
            {
                return;
            }

            if ((this.LineList.Items.Count > 0) && (this.LineList.SelectedIndex < this.paths.Count))
            {
                PData selectedPath = this.paths[this.LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
                this.canvasPoints.Clear();
                this.canvasPoints.AddRange(selectedPath.Lines);
            }
            this.canvas.Refresh();
        }

        private void LineList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            int itemIndex = e.Index;
            if (itemIndex >= 0 && itemIndex < this.LineList.Items.Count)
            {
                PData itemPath = this.paths[itemIndex];

                string itemText;
                if (itemPath.Alias.Length > 0)
                {
                    itemText = itemPath.Alias;
                }
                else
                {
                    itemText = this.LineList.Items[itemIndex].ToString();
                }

                if (itemPath.LoopBack)
                {
                    itemText = "(MZ)" + itemText;
                }
                else if (itemPath.ClosedType)
                {
                    itemText = "(Z)" + itemText;
                }
                else
                {
                    itemText = "   " + itemText;
                }

                if (isItemSelected)
                {
                    using (SolidBrush backgroundColorBrush = new SolidBrush(lineColorsLight[itemPath.LineType]))
                    {
                        e.Graphics.FillRectangle(backgroundColorBrush, e.Bounds);
                    }
                }

                using (StringFormat vCenter = new StringFormat { LineAlignment = StringAlignment.Center })
                using (SolidBrush itemTextColorBrush = new SolidBrush(lineColors[itemPath.LineType]))
                {
                    e.Graphics.DrawString(itemText, e.Font, itemTextColorBrush, e.Bounds, vCenter);
                }
            }

            e.DrawFocusRectangle();
        }

        private void removebtn_Click(object sender, EventArgs e)
        {
            if (this.LineList.SelectedIndex == -1 || this.LineList.Items.Count == 0 || this.LineList.SelectedIndex >= this.paths.Count)
            {
                return;
            }

            setUndo();

            int spi = this.LineList.SelectedIndex;
            this.paths.RemoveAt(spi);
            this.LineList.Items.RemoveAt(spi);
            this.canvasPoints.Clear();
            this.LineList.SelectedIndex = -1;

            this.canvas.Refresh();
        }

        private void Clonebtn_Click(object sender, EventArgs e)
        {
            if (this.LineList.SelectedIndex == -1 || this.canvasPoints.Count == 0)
            {
                return;
            }

            if (this.paths.Count < maxPaths)
            {
                setUndo();

                this.paths.Add(new PData(this.canvasPoints.ToArray(), this.ClosePath.Checked, (int)getPathType(), (this.Arc.CheckState == CheckState.Checked), (this.Sweep.CheckState == CheckState.Checked), string.Empty, this.CloseContPaths.Checked));
                this.LineList.Items.Add(lineNames[(int)getPathType()]);
                this.LineList.SelectedIndex = this.LineList.Items.Count - 1;

                this.canvas.Refresh();
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DNList_Click(object sender, EventArgs e)
        {
            if (this.LineList.SelectedIndex > -1 && this.LineList.SelectedIndex < this.LineList.Items.Count - 1)
            {
                this.LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                ReOrderPath(this.LineList.SelectedIndex);
                this.LineList.SelectedValueChanged += LineList_SelectedValueChanged;
                this.LineList.SelectedIndex++;
            }
        }

        private void upList_Click(object sender, EventArgs e)
        {
            if (this.LineList.SelectedIndex > 0)
            {
                this.LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                ReOrderPath(this.LineList.SelectedIndex - 1);
                this.LineList.SelectedValueChanged += LineList_SelectedValueChanged;
                this.LineList.SelectedIndex--;
            }
        }

        private void ReOrderPath(int index)
        {
            if (index == -1)
            {
                return;
            }

            PData pd1 = this.paths[index];
            string LineTxt1 = this.LineList.Items[index].ToString();

            PData pd2 = this.paths[index + 1];
            string LineTxt2 = this.LineList.Items[index + 1].ToString();

            this.paths[index] = pd2;
            this.LineList.Items[index] = LineTxt2;

            this.paths[index + 1] = pd1;
            this.LineList.Items[index + 1] = LineTxt1;
        }

        private void ToggleUpDownButtons()
        {
            if (this.LineList.Items.Count < 2 || this.LineList.SelectedIndex == -1)
            {
                this.upList.Enabled = false;
                this.DNList.Enabled = false;
            }
            else if (this.LineList.SelectedIndex == 0)
            {
                this.upList.Enabled = false;
                this.DNList.Enabled = true;
            }
            else if (this.LineList.SelectedIndex == this.LineList.Items.Count - 1)
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
        #endregion

        #region Zoom functions
        private void splitButtonZoom_ButtonClick(object sender, EventArgs e)
        {
            this.panFlag = false;

            int zoomFactor = this.canvas.Width / this.canvasBaseSize;
            switch (zoomFactor)
            {
                case 8:
                    ZoomToFactor(((ModifierKeys & Keys.Alt) == Keys.Alt) ? 4 : 1);
                    break;
                case 4:
                    ZoomToFactor(((ModifierKeys & Keys.Alt) == Keys.Alt) ? 2 : 8);
                    break;
                case 2:
                    ZoomToFactor(((ModifierKeys & Keys.Alt) == Keys.Alt) ? 1 : 4);
                    break;
                default:
                    ZoomToFactor(((ModifierKeys & Keys.Alt) == Keys.Alt) ? 8 : 2);
                    break;
            }
        }

        private void xToolStripMenuZoom1x_Click(object sender, EventArgs e)
        {
            ZoomToFactor(1);
        }

        private void xToolStripMenuZoom2x_Click(object sender, EventArgs e)
        {
            ZoomToFactor(2);
        }

        private void xToolStripMenuZoom4x_Click(object sender, EventArgs e)
        {
            ZoomToFactor(4);
        }

        private void xToolStripMenuZoom8x_Click(object sender, EventArgs e)
        {
            ZoomToFactor(8);
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

            int newDimension = this.canvasBaseSize * zoomFactor;

            Point zoomedCanvasPos = new Point
            {
                X = (this.canvas.Location.X - zoomPoint.X) * newDimension / this.canvas.Width + zoomPoint.X,
                Y = (this.canvas.Location.Y - zoomPoint.Y) * newDimension / this.canvas.Height + zoomPoint.Y
            };

            // Clamp the canvas location; we're not overscrolling... yet
            int minX = (this.viewport.ClientSize.Width > newDimension) ? (this.viewport.ClientSize.Width - newDimension) / 2 : this.viewport.ClientSize.Width - newDimension;
            int maxX = (this.viewport.ClientSize.Width > newDimension) ? (this.viewport.ClientSize.Width - newDimension) / 2 : 0;
            zoomedCanvasPos.X = zoomedCanvasPos.X.Clamp(minX, maxX);

            int minY = (this.viewport.ClientSize.Height > newDimension) ? (this.viewport.ClientSize.Height - newDimension) / 2 : this.viewport.ClientSize.Height - newDimension;
            int maxY = (this.viewport.ClientSize.Height > newDimension) ? (this.viewport.ClientSize.Height - newDimension) / 2 : 0;
            zoomedCanvasPos.Y = zoomedCanvasPos.Y.Clamp(minY, maxY);

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
            if (this.hadFocus != null)
            {
                this.hadFocus.Focus();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!this.canScrollZoom)
            {
                return;
            }

            int delta = Math.Sign(e.Delta);
            int oldZoomFactor = this.canvas.Width / this.canvasBaseSize;
            if ((delta > 0 && oldZoomFactor == 8) || (delta < 0 && oldZoomFactor == 1))
            {
                return;
            }

            int zoomFactor = (delta > 0) ? oldZoomFactor * 2 : oldZoomFactor / 2;
            Point mousePosition = new Point(e.X - this.viewport.Location.X, e.Y - this.viewport.Location.Y);
            ZoomToFactor(zoomFactor, mousePosition);

            base.OnMouseWheel(e);
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

                ZoomToFactor(1);
                resetRotation();
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
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!getPathData((int)(this.OutputScale.Value * this.canvas.ClientSize.Width / 100), (int)(this.OutputScale.Value * this.canvas.ClientSize.Height / 100), out _))
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string figure = Regex.Replace(this.FigureName.Text, "[^a-zA-Z0-9 -]", string.Empty);
            figure = figure.Length == 0 ? "Untitled" : figure;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = figure;
                sfd.InitialDirectory = Settings.ProjectFolder;
                sfd.Filter = "Project Files (.dhp)|*.dhp|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ProjectFolder = Path.GetDirectoryName(sfd.FileName);

                ArrayList paths = new ArrayList(this.paths);
                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                (paths[paths.Count - 1] as PData).Meta = this.FigureName.Text;
                (paths[paths.Count - 1] as PData).SolidFill = this.SolidFillMenuItem.Checked;
                using (FileStream stream = File.Open(sfd.FileName, FileMode.Create))
                {
                    ser.Serialize(stream, paths);
                }

                AddToRecents(sfd.FileName);
            }
        }

        private void exportPndShape_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ZoomToFactor(1);
            string TMP = string.Empty;
            bool r = getPathData((int)(this.OutputScale.Value * this.canvas.ClientSize.Width / 100), (int)(this.OutputScale.Value * this.canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string output = Properties.Resources.BaseString;
            string figure = this.FigureName.Text;
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            figure = rgx.Replace(figure, string.Empty);
            figure = (figure.Length == 0) ? "Untitled" : figure;
            output = output.Replace("~1", figure);
            output = output.Replace("~2", TMP);
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = figure;
                sfd.InitialDirectory = Settings.ShapeFolder;
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ShapeFolder = Path.GetDirectoryName(sfd.FileName);

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("The shape has been exported as a XAML file for use in paint.net.\r\n\r\nPlease note that paint.net needs to be restarted to use the shape.", "Paint.net Shape Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportPG_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string TMP = string.Empty;
            bool r = getPGPathData((int)(this.OutputScale.Value * this.canvas.ClientSize.Width / 100), (int)(this.OutputScale.Value * this.canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ZoomToFactor(1);
            string output = Properties.Resources.PGBaseString;
            string figure = this.FigureName.Text;
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            figure = rgx.Replace(figure, string.Empty);
            figure = (figure.Length == 0) ? "Untitled" : figure;
            output = output.Replace("~1", figure);
            output = output.Replace("~2", TMP);
            if (this.SolidFillMenuItem.Checked)
            {
                output = output.Replace("~3", "Nonzero");
            }
            else
            {
                output = output.Replace("~3", "EvenOdd");
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = figure;
                sfd.InitialDirectory = Settings.ShapeFolder;
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                Settings.ShapeFolder = Path.GetDirectoryName(sfd.FileName);

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("PathGeometry XAML Saved", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void importPdnShape_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog OFD = new OpenFileDialog())
            {
                OFD.InitialDirectory = Settings.ShapeFolder;
                OFD.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
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

                Settings.ShapeFolder = Path.GetDirectoryName(OFD.FileName);
                ZoomToFactor(1);
                setUndo();

                string data = File.ReadAllText(OFD.FileName);
                string[] d = data.Split(new char[] { '"' });
                bool loadConfirm = false;
                for (int i = 1; i < d.Length; i++)
                {
                    if (d[i - 1].Contains("DisplayName="))
                    {
                        data = d[i];
                        this.FigureName.Text = data;
                    }

                    if (d[i - 1].Contains("Geometry="))
                    {
                        data = d[i];
                        try
                        {
                            parsePathData(data);
                            loadConfirm = true;
                        }
                        catch
                        {
                            MessageBox.Show("Incorrect Format", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                }
                if (!loadConfirm)
                {
                    MessageBox.Show("Incorrect Format", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void pasteData_Click(object sender, EventArgs e)
        {
            setUndo();
            ZoomToFactor(1);

            parsePathData(Clipboard.GetText());
        }

        private void CopyStream_Click(object sender, EventArgs e)
        {
            if (this.paths.Count == 0)
            {
                MessageBox.Show("Nothing to Copy", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ZoomToFactor(1);
            string TMP = string.Empty;
            bool r = getPathData((int)(this.OutputScale.Value * this.canvas.ClientSize.Width / 100), (int)(this.OutputScale.Value * this.canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Copy Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Clipboard.SetText(TMP);
            MessageBox.Show("SVG Copied to Clipboard", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            this.undoMenuItem.Enabled = (this.undoCount > 0);
            this.redoMenuItem.Enabled = (this.redoCount > 0);
            this.removePathToolStripMenuItem.Enabled = (this.LineList.SelectedIndex > -1);
            this.clonePathToolStripMenuItem.Enabled = (this.LineList.SelectedIndex > -1);
            this.loopPathToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1);
            this.flipHorizontalToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1 || this.LineList.Items.Count > 0);
            this.flipVerticalToolStripMenuItem.Enabled = (this.canvasPoints.Count > 1 || this.LineList.Items.Count > 0);
        }

        private void editToolStripMenuItem_DropDownClosed(object sender, EventArgs e)
        {
            this.undoMenuItem.Enabled = true;
            this.redoMenuItem.Enabled = true;
            this.removePathToolStripMenuItem.Enabled = true;
            this.loopPathToolStripMenuItem.Enabled = true;
        }

        private void Flip_Click(object sender, EventArgs e)
        {
            setUndo();

            bool isHorizontal = (sender is ToolStripMenuItem menuItem && menuItem.Tag.ToString() == "H");

            if (this.canvasPoints.Count == 0)
            {
                foreach (PData path in this.paths)
                {
                    PointF[] pl = path.Lines;

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
                    if (path.LineType == (int)PathType.Ellipse)
                    {
                        path.RevSweep = !path.RevSweep;
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

                if (this.LineList.SelectedIndex != -1)
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

        private void HelpMenu_Click(object sender, EventArgs e)
        {
            string directory = Assembly.GetExecutingAssembly().Location;

            string pdfPath = (sender is ToolStripMenuItem menuItem && menuItem.Name.Equals(nameof(QuickStartStripMenuItem), StringComparison.OrdinalIgnoreCase))
                ? Path.Combine(directory, "ShapeMaker QuickStart.pdf")
                : Path.Combine(directory, "ShapeMaker User Guide.pdf");

            if (!File.Exists(pdfPath))
            {
                dest += "ShapeMaker User Guide.pdf";
            }

            try
            {
                Process.Start(pdfPath);
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
            MessageBox.Show(this.Text + "\nCopyright \u00A9 2017, The Dwarf Horde\n\n" +
                "Rob Tauler (TechnoRobbo)\n- Code Lead (up to v1.2.3), Design\n\n" +
                "Jason Wendt (toe_head2001)\n- Code Lead (v1.3 onward), Design\n\n" +
                "John Robbins (Red Ochre)\n- Graphics Lead, Design\n\n" +
                "Scott Stringer (Ego Eram Reputo)\n- Documentation Lead, Design\n\n" +
                "David Issel (BoltBait)\n- Beta Testing, Design",
                "About ShapeMaker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region Scale Slider functions
        private void scaleSlider_Scroll(object sender, EventArgs e)
        {
            float scale = this.scaleSlider.Value / 100f;
            this.toolTip1.SetToolTip(this.scaleSlider, $"{scale:0.00}x");

            if (scale > 1 && !InView())
            {
                return;
            }

            if (this.canvasPoints.Count == 0 && this.LineList.Items.Count > 0)
            {
                this.averagePoint = new PointF(.5f, .5f);
                int undoIndex = (this.undoPointer - 1 + undoMax) % undoMax;
                for (int k = 0; k < this.paths.Count; k++)
                {
                    PointF[] tmp = this.paths[k].Lines;
                    PointF[] tmp2 = this.undoLines[undoIndex][k].Lines;
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i].X = (tmp2[i].X - this.averagePoint.X) * scale + this.averagePoint.X;
                        tmp[i].Y = (tmp2[i].Y - this.averagePoint.Y) * scale + this.averagePoint.Y;
                    }
                }
            }
            else if (this.canvasPoints.Count > 1)
            {
                this.averagePoint = this.canvasPoints.ToArray().Average();
                int undoIndex = (this.undoPointer - 1 + undoMax) % undoMax;
                for (int idx = 0; idx < this.canvasPoints.Count; idx++)
                {
                    this.canvasPoints[idx] = new PointF
                    {
                        X = (this.undoPoints[undoIndex][idx].X - this.averagePoint.X) * scale + this.averagePoint.X,
                        Y = (this.undoPoints[undoIndex][idx].Y - this.averagePoint.Y) * scale + this.averagePoint.Y
                    };
                }
            }
            this.canvas.Refresh();
        }

        private void scaleSlider_MouseDown(object sender, MouseEventArgs e)
        {
            this.WheelTimer.Stop();
            this.drawAverage = true;
            setUndo();
            float scale = this.scaleSlider.Value / 100f;
            this.toolTip1.SetToolTip(this.scaleSlider, $"{scale:0.00}x");
        }

        private void scaleSlider_MouseUp(object sender, MouseEventArgs e)
        {
            this.drawAverage = false;
            this.scaleSlider.Value = 100;
            float scale = this.scaleSlider.Value / 100f;
            this.toolTip1.SetToolTip(this.scaleSlider, $"{scale:0.00}x");
            this.canvas.Refresh();
        }
        #endregion

        #region Toolbar functions
        private void OptionToggle(object sender, EventArgs e)
        {
            (sender as ToolStripButton).Checked = !(sender as ToolStripButton).Checked;

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
            if ((sender as ToolStripButton).Checked)
            {
                return;
            }

            this.activeType = (sender as ToolStripButtonWithKeys).PathType;
            PathTypeToggle();
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
                    this.canvas.Refresh();
                }
            }

            this.Arc.Enabled = (this.Elliptical.Checked && !this.MacroCircle.Checked);
            this.Sweep.Enabled = (this.Elliptical.Checked && !this.MacroCircle.Checked);
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

            this.Arc.Enabled = (this.Elliptical.Checked && !this.MacroCircle.Checked);
            this.Sweep.Enabled = (this.Elliptical.Checked && !this.MacroCircle.Checked);

            this.canvas.Refresh();
        }

        private void Loops_Click(object sender, EventArgs e)
        {
            setUndo();

            if (this.CloseContPaths.Equals(sender))
            {
                if (!this.CloseContPaths.Checked)
                {
                    this.ClosePath.Checked = false;
                    this.CloseContPaths.Checked = true;
                }
                else
                {
                    this.CloseContPaths.Checked = false;
                }
            }
            else
            {
                if (!this.ClosePath.Checked)
                {
                    this.ClosePath.Checked = true;
                    this.CloseContPaths.Checked = false;
                }
                else
                {
                    this.ClosePath.Checked = false;
                }
            }

            this.ClosePath.Image = (this.ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            this.CloseContPaths.Image = (this.CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;

            this.canvas.Refresh();

            if (this.LineList.SelectedIndex != -1)
            {
                UpdateExistingPath();
            }
        }

        private void Property_Click(object sender, EventArgs e)
        {
            setUndo();

            (sender as ToolStripButton).CheckState = (sender as ToolStripButton).CheckState == CheckState.Checked ? CheckState.Indeterminate : CheckState.Checked;

            if (sender == this.Arc)
            {
                this.Arc.Image = (this.Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcSmall : Properties.Resources.ArcLarge;
            }
            else if (sender == this.Sweep)
            {
                this.Sweep.Image = (this.Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;
            }

            this.canvas.Refresh();

            if (this.LineList.SelectedIndex != -1)
            {
                UpdateExistingPath();
            }
        }
        #endregion

        #region Image Tracing
        private void setTraceImage()
        {
#if PDNPLUGIN
            if (this.traceLayer.Checked)
            {
                Rectangle selection = this.Selection.GetBoundsInt();
                this.canvas.BackgroundImage = this.EffectSourceSurface.CreateAliasedBitmap(selection);
            }
            else
            {
                Surface surface = this.Services.GetService<IClipboardService>().TryGetSurface();
                if (surface == null)
                {
                    this.traceLayer.Focus();
                    MessageBox.Show("Couldn't load an image from the clipboard.", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                this.canvas.BackgroundImage = surface.CreateAliasedBitmap();
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
            this.canvas.BackgroundImageLayout = (this.FitBG.Checked) ? ImageLayout.Zoom : ImageLayout.Center;
            this.canvas.Refresh();
        }
        #endregion

        #region Misc Form Controls' event functions
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (this.canvasPoints.Count > 1 && this.LineList.SelectedIndex == -1)
            {
                AddNewPath();
            }

#if PDNPLUGIN
            if (this.DrawOnCanvas.Checked)
            {
                MakePathForPdnCanvas();
            }

            FinishTokenUpdate();
#endif
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
            if (this.FigureName.Text == string.Empty)
            {
                this.FigureName.Text = "Untitled";
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
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

            bool newPath = this.LineList.SelectedIndex == -1;
            this.clonePathButton.Enabled = !newPath;
            this.removePathButton.Enabled = !newPath;
            this.MacroCircle.Enabled = newPath;
            this.MacroRect.Enabled = newPath;
            this.MacroCubic.Enabled = newPath;
            this.ClosePath.Enabled = !((this.MacroCircle.Checked && this.MacroCircle.Enabled) || (this.MacroRect.Checked && this.MacroRect.Enabled));
            this.CloseContPaths.Enabled = !((this.MacroCircle.Checked && this.MacroCircle.Enabled) || (this.MacroRect.Checked && this.MacroRect.Enabled));
            this.DeselectBtn.Enabled = (!newPath && this.canvasPoints.Count != 0);
            this.AddBtn.Enabled = (newPath && this.canvasPoints.Count > 1);
            this.DiscardBtn.Enabled = (newPath && this.canvasPoints.Count > 1);
            this.scaleSlider.Enabled = (this.canvasPoints.Count > 1 || (this.canvasPoints.Count == 0 && this.LineList.Items.Count > 0));
            this.RotationKnob.Enabled = (this.canvasPoints.Count > 1 || (this.canvasPoints.Count == 0 && this.LineList.Items.Count > 0));

            if (Control.ModifierKeys == Keys.Control)
            {
                this.keyTrak = true;
            }
            else if (this.keyTrak)
            {
                this.keyTrak = false;
                this.canvas.Refresh();
            }

            if (this.canvasPoints.Count > 0 || this.LineList.Items.Count > 0)
            {
                this.statusLabelNubsUsed.Text = $"{this.canvasPoints.Count}/{maxPoints} Nubs used";
                this.statusLabelPathsUsed.Text = $"{this.LineList.Items.Count}/{maxPaths} Paths used";
            }

            if (newPath)
            {
                this.isNewPath = true;
            }
        }

        private void generic_MouseWheel(object sender, MouseEventArgs e)
        {
            this.WheelTimer.Stop();

            if (!this.wheelScaleOrRotate)
            {
                this.wheelScaleOrRotate = true;
                setUndo();
            }

            if (!this.drawAverage)
            {
                this.drawAverage = true;
            }

            this.WheelTimer.Start();
        }

        private void EndWheeling(object sender, EventArgs e)
        {
            this.WheelTimer.Stop();
            this.wheelScaleOrRotate = false;

            if (this.scaleSlider.Value != 100)
            {
                this.scaleSlider.Value = 100;
            }

            if (this.drawAverage)
            {
                this.drawAverage = false;
                this.canvas.Refresh();
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

                string[] paths = recents.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

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
            XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
            string[] paths = recents.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 1;
            foreach (string projectPath in paths)
            {
                if (!File.Exists(projectPath))
                {
                    continue;
                }

                string menuText = $"&{count} {Path.GetFileName(projectPath)}";
                try
                {
                    ArrayList projectPaths = (ArrayList)ser.Deserialize(File.OpenRead(projectPath));
                    menuText = $"&{count} {(projectPaths[projectPaths.Count - 1] as PData).Meta} ({Path.GetFileName(projectPath)})";
                }
                catch
                {
                }

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
    }
}
