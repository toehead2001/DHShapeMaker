//Elliptical Arc algorithm from svg.codeplex.com
using Microsoft.Win32;
using PaintDotNet;
using PaintDotNet.Effects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ShapeMaker
{
    internal partial class EffectPluginConfigDialog : EffectConfigDialog
    {
        readonly string[] LineNames =
        {
            "Straight Lines",
            "Ellipse",
            "Cubic Beziers",
            "Smooth Cubic Beziers",
            "Quadratic Beziers",
            "Smooth Quadratic Beziers"
        };

        readonly Color[] LineColors =
        {
            Color.Black,
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.DarkGoldenrod,
            Color.Purple
        };

        readonly Color[] LineColorsLight =
        {
            Color.FromArgb(204, 204, 204),
            Color.FromArgb(255, 204, 204),
            Color.FromArgb(204, 204, 255),
            Color.FromArgb(204, 230, 204),
            Color.FromArgb(241, 231, 206),
            Color.FromArgb(230, 204, 230)
        };

        readonly Color AnchorColor = Color.Teal;

        PathType activeType;
        readonly ToolStripButton[] typeButtons = new ToolStripButton[6];

        const double RadPerDeg = Math.PI / 180.0;
        const double twoPI = Math.PI * 2;

        bool countflag = false;
        const int maxPaths = 200;
        const int maxPoints = 256;
        int clickedNub = -1;
        PointF MoveStart;
        PointF[] canvasPoints = new PointF[0];

        const int UndoMax = 16;
        readonly List<PData>[] UDLines = new List<PData>[UndoMax];
        readonly PointF[][] UDPoints = new PointF[UndoMax][];
        readonly PathType[] UDType = new PathType[UndoMax];
        readonly int[] UDSelected = new int[UndoMax];
        int UDCount = 0;
        int RDCount = 0;
        int UDPointer = 0;

        float lastRot = 180;
        bool KeyTrak = false;
        GraphicsPath[] PGP = new GraphicsPath[0];
        readonly List<PData> Lines = new List<PData>();
        bool PanFlag = false;
        bool CanScrollZoom = false;
        float DPI = 1;
        Control hadFocus;
        bool isNewPath = true;
        int canvasBaseSize;
        Bitmap clipboardImage = null;
        bool MoveFlag = false;
        bool WheelScaleOrRotate = false;
        bool DrawAverage = false;
        PointF AveragePoint = new PointF(0.5f, 0.5f);

        internal EffectPluginConfigDialog()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentUICulture;
            InitializeComponent();

            // Theming
            PdnTheme.ForeColor = this.ForeColor;
            PdnTheme.BackColor = this.BackColor;

            this.menuStrip1.Renderer = PdnTheme.Renderer;
            this.statusStrip1.Renderer = PdnTheme.Renderer;

            this.toolStripUndo.Renderer = new ThemeRenderer(Color.White, Color.Silver);
            this.toolStripBlack.Renderer = new ThemeRenderer(LineColorsLight[0], LineColors[0]);
            this.toolStripBlue.Renderer = new ThemeRenderer(LineColorsLight[2], LineColors[2]);
            this.toolStripGreen.Renderer = new ThemeRenderer(LineColorsLight[3], LineColors[3]);
            this.toolStripYellow.Renderer = new ThemeRenderer(LineColorsLight[4], LineColors[4]);
            this.toolStripPurple.Renderer = new ThemeRenderer(LineColorsLight[5], LineColors[5]);
            this.toolStripRed.Renderer = new ThemeRenderer(LineColorsLight[1], LineColors[1]);
            this.toolStripOptions.Renderer = new ThemeRenderer(Color.White, Color.Silver);

            LineList.ForeColor = PdnTheme.ForeColor;
            LineList.BackColor = PdnTheme.BackColor;
            FigureName.ForeColor = PdnTheme.ForeColor;
            FigureName.BackColor = PdnTheme.BackColor;
            OutputScale.ForeColor = PdnTheme.ForeColor;
            OutputScale.BackColor = PdnTheme.BackColor;
        }

        #region Effect Token functions
        protected override void InitialInitToken()
        {
            theEffectToken = new EffectPluginConfigToken(PGP, Lines, false, 100, true, "Untitled", false);
        }

        protected override void InitTokenFromDialog()
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)EffectToken;
            token.GP = PGP;
            token.PathData = Lines;
            token.Draw = DrawOnCanvas.Checked;
            token.ShapeName = FigureName.Text;
            token.Scale = OutputScale.Value;
            token.SnapTo = Snap.Checked;
            token.SolidFill = SolidFillMenuItem.Checked;
        }

        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
            EffectPluginConfigToken token = (EffectPluginConfigToken)effectTokenCopy;
            DrawOnCanvas.Checked = token.Draw;
            FigureName.Text = token.ShapeName;
            OutputScale.Value = token.Scale;
            Snap.Checked = token.SnapTo;
            SolidFillMenuItem.Checked = token.SolidFill;

            Lines.Clear();
            LineList.Items.Clear();
            foreach (PData p in token.PathData)
            {
                Lines.Add(p);
                LineList.Items.Add(LineNames[p.LineType]);
            }
        }
        #endregion

        #region Form functions
        private void EffectPluginConfigDialog_Load(object sender, EventArgs e)
        {
            DPI = this.AutoScaleDimensions.Width / 96f;
            canvasBaseSize = this.canvas.Width;

            setTraceImage();

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = EffectPlugin.StaticName + " v" + version;

            Arc.Enabled = false;
            Sweep.Enabled = false;

            typeButtons[0] = StraightLine;
            typeButtons[1] = Elliptical;
            typeButtons[2] = CubicBezier;
            typeButtons[3] = SCubicBezier;
            typeButtons[4] = QuadBezier;
            typeButtons[5] = SQuadBezier;

            toolTip1.ReshowDelay = 0;
            toolTip1.AutomaticDelay = 0;
            toolTip1.AutoPopDelay = 0;
            toolTip1.InitialDelay = 0;
            toolTip1.UseFading = false;
            toolTip1.UseAnimation = false;

            timer1.Enabled = true;

            #region DPI fixes
            MinimumSize = Size;
            LineList.ItemHeight = getDpiSize(LineList.ItemHeight);
            LineList.Height = upList.Top - LineList.Top;
            statusLabelNubsUsed.Width = getDpiSize(statusLabelNubsUsed.Width);
            statusLabelPathsUsed.Width = getDpiSize(statusLabelPathsUsed.Width);
            statusLabelNubPos.Width = getDpiSize(statusLabelNubPos.Width);
            statusLabelMousePos.Width = getDpiSize(statusLabelMousePos.Width);
            horScrollBar.Height = getDpiSize(horScrollBar.Height);
            verScrollBar.Width = getDpiSize(verScrollBar.Width);
            traceLayer.Left = traceClipboard.Left;

            toolStripUndo.AutoSize = toolStripBlack.AutoSize = toolStripBlue.AutoSize = toolStripGreen.AutoSize =
                toolStripYellow.AutoSize = toolStripPurple.AutoSize = toolStripRed.AutoSize = toolStripOptions.AutoSize = false;
            toolStripUndo.ImageScalingSize = toolStripBlack.ImageScalingSize = toolStripBlue.ImageScalingSize =
                toolStripGreen.ImageScalingSize = toolStripYellow.ImageScalingSize = toolStripPurple.ImageScalingSize =
                toolStripRed.ImageScalingSize = toolStripOptions.ImageScalingSize = getDpiSize(toolStripOptions.ImageScalingSize);
            toolStripUndo.AutoSize = toolStripBlack.AutoSize = toolStripBlue.AutoSize = toolStripGreen.AutoSize =
                toolStripYellow.AutoSize = toolStripPurple.AutoSize = toolStripRed.AutoSize = toolStripOptions.AutoSize = true;

            toolStripBlack.Left = toolStripUndo.Right;
            toolStripBlue.Left = toolStripBlack.Right;
            toolStripGreen.Left = toolStripBlue.Right;
            toolStripYellow.Left = toolStripGreen.Right;
            toolStripPurple.Left = toolStripYellow.Right;
            toolStripRed.Left = toolStripPurple.Right;
            toolStripOptions.Left = toolStripRed.Right;
            #endregion

            adjustForWindowSize();

            statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";
        }

        private Size getDpiSize(Size size)
        {
            return new Size
            {
                Width = (int)Math.Round(size.Width * DPI),
                Height = (int)Math.Round(size.Height * DPI)
            };
        }

        private int getDpiSize(int dimension)
        {
            return (int)Math.Round(dimension * DPI);
        }

        private void EffectPluginConfigDialog_Resize(object sender, EventArgs e)
        {
            adjustForWindowSize();
        }

        private void adjustForWindowSize()
        {
            viewport.Width = LineList.Left - viewport.Left - (int)Math.Round(32 * DPI);
            viewport.Height = statusStrip1.Top - viewport.Top - (int)Math.Round(20 * DPI);

            horScrollBar.Top = viewport.Bottom;
            horScrollBar.Width = viewport.Width;

            verScrollBar.Left = viewport.Right;
            verScrollBar.Height = viewport.Height;

            Point newCanvasPos = canvas.Location;
            if (canvas.Width < viewport.ClientSize.Width || canvas.Location.X > 0)
                newCanvasPos.X = (viewport.ClientSize.Width - canvas.Width) / 2;
            if (canvas.Height < viewport.ClientSize.Height || canvas.Location.Y > 0)
                newCanvasPos.Y = (viewport.ClientSize.Height - canvas.Height) / 2;
            canvas.Location = newCanvasPos;

            UpdateScrollBars();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (canvasPoints.Length > 1 && LineList.SelectedIndex == -1)
                    AddNewPath();
                return true;
            }

            if (keyData == Keys.Escape)
            {
                Deselect_Click(DeselectBtn, new EventArgs());
                return true;
            }

            foreach (var control in this.Controls)
            {
                if (!(control is ToolStrip))
                    continue;

                foreach (var subControl in (control as ToolStrip).Items)
                {
                    if (subControl is ToolStripButtonWithKeys button)
                    {
                        if (button.Enabled && keyData == button.ShortcutKeys)
                        {
                            button.PerformClick();
                            return true;
                        }
                    }
                }
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
            Undo.Enabled = true;
            Redo.Enabled = false;

            RDCount = 0;
            UDCount++;
            UDCount = (UDCount > UndoMax) ? UndoMax : UDCount;
            UDType[UDPointer] = getPathType();
            UDSelected[UDPointer] = (deSelected) ? -1 : LineList.SelectedIndex;
            UDPoints[UDPointer] = new PointF[canvasPoints.Length];
            Array.Copy(canvasPoints, UDPoints[UDPointer], canvasPoints.Length);
            if (UDLines[UDPointer] == null)
                UDLines[UDPointer] = new List<PData>();
            else
                UDLines[UDPointer].Clear();
            foreach (PData pd in Lines)
            {
                PointF[] tmp = new PointF[pd.Lines.Length];
                Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                UDLines[UDPointer].Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
            }

            UDPointer++;
            UDPointer %= UndoMax;
        }

        private void Undo_Click(object sender, EventArgs e)
        {
            if (UDCount == 0)
                return;

            if (RDCount == 0)
            {
                setUndo();
                UDCount--;
                UDPointer--;
            }

            UDPointer--;
            UDPointer += UndoMax;
            UDPointer %= UndoMax;

            canvasPoints = new PointF[0];
            if (UDPoints[UDPointer].Length != 0)
            {
                canvasPoints = new PointF[UDPoints[UDPointer].Length];
                Array.Copy(UDPoints[UDPointer], canvasPoints, canvasPoints.Length);
            }

            LineList.Items.Clear();
            Lines.Clear();
            if (UDLines[UDPointer].Count != 0)
            {
                LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                foreach (PData pd in UDLines[UDPointer])
                {
                    PointF[] tmp = new PointF[pd.Lines.Length];
                    Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                    Lines.Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
                    LineList.Items.Add(LineNames[pd.LineType]);
                }
                if (UDSelected[UDPointer] < LineList.Items.Count)
                    LineList.SelectedIndex = UDSelected[UDPointer];
                LineList.SelectedValueChanged += LineList_SelectedValueChanged;
            }

            if (LineList.SelectedIndex != -1)
            {
                PData selectedPath = Lines[LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
            }
            else
            {
                setUiForPath(UDType[UDPointer], false, false, false, false);
            }

            UDCount--;
            UDCount = (UDCount < 0) ? 0 : UDCount;
            RDCount++;

            Undo.Enabled = (UDCount > 0);
            Redo.Enabled = true;
            resetRotation();
            canvas.Refresh();
        }

        private void Redo_Click(object sender, EventArgs e)
        {
            if (RDCount == 0)
                return;

            UDPointer++;
            UDPointer += UndoMax;
            UDPointer %= UndoMax;

            canvasPoints = new PointF[0];
            if (UDPoints[UDPointer].Length != 0)
            {
                canvasPoints = new PointF[UDPoints[UDPointer].Length];
                Array.Copy(UDPoints[UDPointer], canvasPoints, canvasPoints.Length);
            }

            LineList.Items.Clear();
            Lines.Clear();
            if (UDLines[UDPointer].Count != 0)
            {
                LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                foreach (PData pd in UDLines[UDPointer])
                {
                    PointF[] tmp = new PointF[pd.Lines.Length];
                    Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                    Lines.Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
                    LineList.Items.Add(LineNames[pd.LineType]);
                }
                if (UDSelected[UDPointer] < LineList.Items.Count)
                    LineList.SelectedIndex = UDSelected[UDPointer];
                LineList.SelectedValueChanged += LineList_SelectedValueChanged;
            }

            if (LineList.SelectedIndex != -1)
            {
                PData selectedPath = Lines[LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
            }
            else
            {
                setUiForPath(UDType[UDPointer], false, false, false, false);
            }

            UDCount++;
            RDCount--;

            Redo.Enabled = (RDCount > 0);
            Undo.Enabled = true;
            resetRotation();
            canvas.Refresh();
        }

        private void resetHistory()
        {
            UDCount = 0;
            RDCount = 0;
            UDPointer = 0;
            Undo.Enabled = false;
            Redo.Enabled = false;
        }
        #endregion

        #region Canvas functions
        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            Image gridImg = Properties.Resources.bg;
            ImageAttributes attr = new ImageAttributes();
            ColorMatrix mx = new ColorMatrix();
            mx.Matrix33 = (101f - opacitySlider.Value) / 100f;
            attr.SetColorMatrix(mx);
            using (TextureBrush texture = new TextureBrush(gridImg, new Rectangle(Point.Empty, gridImg.Size), attr))
            {
                texture.WrapMode = WrapMode.Tile;
                e.Graphics.FillRectangle(texture, e.ClipRectangle);
            }
            attr.Dispose();

            PointF loopBack = new PointF(-9999, -9999);
            PointF Oldxy = new PointF(-9999, -9999);

            PathType pType = 0;
            bool isClosed = false;
            bool mpMode = false;
            bool isLarge = false;
            bool revSweep = false;
            PointF[] pPoints;

            try
            {
                int j;
                for (int jj = -1; jj < Lines.Count; jj++)
                {
                    j = jj + 1;
                    if (j == Lines.Count && LineList.SelectedIndex == -1)
                        j = -1;

                    if (j >= Lines.Count)
                        continue;

                    if (j == LineList.SelectedIndex)
                    {
                        pPoints = canvasPoints;
                        pType = getPathType();
                        isClosed = ClosePath.Checked;
                        mpMode = CloseContPaths.Checked;
                        isLarge = (Arc.CheckState == CheckState.Checked);
                        revSweep = (Sweep.CheckState == CheckState.Checked);
                    }
                    else
                    {
                        PData itemPath = Lines[j];
                        pPoints = itemPath.Lines;
                        pType = (PathType)itemPath.LineType;
                        isClosed = itemPath.ClosedType;
                        mpMode = itemPath.LoopBack;
                        isLarge = itemPath.IsLarge;
                        revSweep = itemPath.RevSweep;
                    }

                    if (pPoints.Length == 0)
                        continue;

                    PointF[] pts = new PointF[pPoints.Length];
                    for (int i = 0; i < pPoints.Length; i++)
                    {
                        pts[i].X = canvas.ClientSize.Width * pPoints[i].X;
                        pts[i].Y = canvas.ClientSize.Height * pPoints[i].Y;
                    }

                    PointF[] Qpts = new PointF[pPoints.Length];
                    #region cube to quad
                    if (pType == PathType.Quadratic || pType == PathType.SmoothQuadratic)
                    {
                        for (int i = 0; i < pPoints.Length; i++)
                        {
                            int PT = getNubType(i);
                            if (PT == 0)
                            {
                                Qpts[i] = pts[i];
                            }
                            else if (PT == 1)
                            {
                                Qpts[i] = new PointF(pts[i].X * 2f / 3f + pts[i - 1].X * 1f / 3f,
                                    pts[i].Y * 2f / 3f + pts[i - 1].Y * 1f / 3f);
                            }
                            else if (PT == 2)
                            {
                                Qpts[i] = new PointF(pts[i - 1].X * 2f / 3f + pts[i + 1].X * 1f / 3f,
                                    pts[i - 1].Y * 2f / 3f + pts[i + 1].Y * 1f / 3f);
                            }

                            else if (PT == 3)
                            {
                                Qpts[i] = pts[i];
                            }
                        }

                    }
                    #endregion

                    bool islinked = true;
                    if (!Oldxy.Equals(pts[0]) || (j == LineList.SelectedIndex && ClosePath.Checked))
                    {
                        loopBack = new PointF(pts[0].X, pts[0].Y);
                        islinked = false;
                    }

                    //render lines
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    if ((Control.ModifierKeys & Keys.Control) != Keys.Control)
                    {
                        #region draw handles
                        if (j == LineList.SelectedIndex) //buffer draw
                        {
                            if ((isClosed || mpMode) && pts.Length > 1)
                            {
                                e.Graphics.DrawRectangle(new Pen(AnchorColor), loopBack.X - 4, loopBack.Y - 4, 6, 6);
                            }
                            else if (islinked)
                            {
                                PointF[] tri = {new PointF(pts[0].X, pts[0].Y - 4f),
                                            new PointF(pts[0].X + 3f, pts[0].Y + 3f),
                                            new PointF(pts[0].X - 4f, pts[0].Y + 3f)};
                                e.Graphics.DrawPolygon(new Pen(AnchorColor), tri);
                            }
                            else
                            {
                                e.Graphics.DrawEllipse(new Pen(AnchorColor), pts[0].X - 4, pts[0].Y - 4, 6, 6);
                            }

                            for (int i = 1; i < pts.Length; i++)
                            {
                                switch (pType)
                                {
                                    case PathType.Straight:
                                        e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                        break;
                                    case PathType.Ellipse:
                                        if (i == 4)
                                        {
                                            PointF mid = pointAverage(pts[0], pts[4]);
                                            e.Graphics.DrawEllipse(Pens.Black, pts[4].X - 4, pts[4].Y - 4, 6, 6);
                                            if (!MacroCircle.Checked || LineList.SelectedIndex != -1)
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
                                        if (getNubType(i) == 1)
                                        {
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i - 1], pts[i]);
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i + 2].X - 4, pts[i + 2].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i], pts[i + 2]);
                                        }
                                        break;
                                    case PathType.SmoothQuadratic:
                                        if (getNubType(i) == 3)
                                        {
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                        }
                                        break;
                                    case PathType.Cubic:
                                    case PathType.SmoothCubic:
                                        if (getNubType(i) == 1 && !MacroCubic.Checked)
                                        {
                                            if (i != 1 || pType == PathType.Cubic)
                                                e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i - 1], pts[i]);
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i + 2].X - 4, pts[i + 2].Y - 4, 6, 6);
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i + 1].X - 4, pts[i + 1].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i + 1], pts[i + 2]);
                                        }
                                        else if (getNubType(i) == 3 && MacroCubic.Checked)
                                        {
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                        }
                                        break;
                                }
                            }
                        }
                        #endregion
                    }

                    #region drawlines
                    using (Pen p = new Pen(LineColors[(int)pType]))
                    using (Pen activePen = new Pen(LineColors[(int)pType]))
                    {
                        p.DashStyle = DashStyle.Solid;
                        p.Width = 1;

                        activePen.Width = 5f;
                        activePen.Color = Color.FromArgb(51, p.Color);
                        activePen.LineJoin = LineJoin.Bevel;

                        if (pPoints.Length > 3 && (pType == PathType.Quadratic || pType == PathType.SmoothQuadratic))
                        {
                            try
                            {
                                e.Graphics.DrawBeziers(p, Qpts);
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawBeziers(activePen, Qpts);
                            }
                            catch
                            {
                            }
                        }
                        else if (pPoints.Length > 3 && (pType == PathType.Cubic || pType == PathType.SmoothCubic))
                        {
                            try
                            {
                                e.Graphics.DrawBeziers(p, pts);
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawBeziers(activePen, pts);
                            }
                            catch
                            {
                            }
                        }
                        else if (pPoints.Length > 1 && pType == PathType.Straight)
                        {
                            if (MacroRect.Checked && j == -1 && LineList.SelectedIndex == -1)
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
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawLines(activePen, pts);
                            }
                        }
                        else if (pPoints.Length == 5 && pType == PathType.Ellipse)
                        {
                            PointF mid = pointAverage(pts[0], pts[4]);
                            if (MacroCircle.Checked && j == -1 && LineList.SelectedIndex == -1)
                            {
                                float far = pythag(pts[0], pts[4]);
                                e.Graphics.DrawEllipse(p, mid.X - far / 2f, mid.Y - far / 2f, far, far);
                                e.Graphics.DrawEllipse(activePen, mid.X - far / 2f, mid.Y - far / 2f, far, far);
                            }
                            else
                            {
                                float l = pythag(mid, pts[1]);
                                float h = pythag(mid, pts[2]);
                                float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                                if ((int)h == 0 || (int)l == 0)
                                {
                                    PointF[] nullLine = { pts[0], pts[4] };
                                    e.Graphics.DrawLines(p, nullLine);
                                    if (j == LineList.SelectedIndex)
                                        e.Graphics.DrawLines(activePen, nullLine);
                                }
                                else
                                {
                                    using (GraphicsPath gp = new GraphicsPath())
                                    {
                                        AddToGraphicsPath(gp, pts[0], l, h, a, (isLarge) ? 1 : 0, (revSweep) ? 1 : 0, pts[4]);
                                        e.Graphics.DrawPath(p, gp);
                                        if (j == LineList.SelectedIndex)
                                            e.Graphics.DrawPath(activePen, gp);
                                    }
                                    if (j == -1)
                                    {
                                        if (!MacroCircle.Checked || LineList.SelectedIndex != -1)
                                        {
                                            using (GraphicsPath gp = new GraphicsPath())
                                            {
                                                AddToGraphicsPath(gp, pts[0], l, h, a, (isLarge) ? 0 : 1, (revSweep) ? 0 : 1, pts[4]);
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
                        }
                        //join line
                        bool noJoin = false;
                        if (j == -1)
                        {
                            if (MacroCircle.Checked && Elliptical.Checked)
                                noJoin = true;
                            if (MacroRect.Checked && StraightLine.Checked)
                                noJoin = true;
                        }

                        if (!mpMode)
                        {
                            if (!noJoin && isClosed && pts.Length > 1)
                            {
                                e.Graphics.DrawLine(p, pts[0], pts[pts.Length - 1]); //preserve
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawLine(activePen, pts[0], pts[pts.Length - 1]); //preserve

                                loopBack = pts[pts.Length - 1];
                            }
                        }
                        else
                        {
                            if (!noJoin && pts.Length > 1)
                            {
                                e.Graphics.DrawLine(p, pts[pts.Length - 1], loopBack);
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawLine(activePen, pts[pts.Length - 1], loopBack);

                                loopBack = pts[pts.Length - 1];
                            }
                        }
                    }
                    #endregion

                    Oldxy = pts[pts.Length - 1];

                    // render average point for when Scaling and Rotation
                    if (DrawAverage)
                    {
                        Point tmpPoint = new Point
                        {
                            X = (int)Math.Round(AveragePoint.X * canvas.ClientSize.Width),
                            Y = (int)Math.Round(AveragePoint.Y * canvas.ClientSize.Height)
                        };
                        e.Graphics.DrawLine(Pens.Red, tmpPoint.X - 3, tmpPoint.Y, tmpPoint.X + 3, tmpPoint.Y);
                        e.Graphics.DrawLine(Pens.Red, tmpPoint.X, tmpPoint.Y - 3, tmpPoint.X, tmpPoint.Y + 3);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (LineList.SelectedIndex != -1)
            {
                int bottomIndex = Math.Min(LineList.TopIndex + (LineList.Height / LineList.ItemHeight) - 1, LineList.Items.Count - 1);
                if (LineList.SelectedIndex < LineList.TopIndex || LineList.SelectedIndex > bottomIndex)
                    LineList.TopIndex = LineList.SelectedIndex;
            }

            RectangleF hit = new RectangleF(e.X - 4, e.Y - 4, 9, 9);
            RectangleF bhit = new RectangleF(e.X - 10, e.Y - 10, 20, 20);

            MoveStart = PointToCanvasCoord(e.X, e.Y);

            //identify node selected
            clickedNub = -1;
            for (int i = 0; i < canvasPoints.Length; i++)
            {
                PointF p = CanvasCoordToPoint(canvasPoints[i].X, canvasPoints[i].Y);
                if (hit.Contains(p))
                {
                    clickedNub = i;
                    break;
                }
            }
            try
            {
                PathType lt = getPathType();


                if (Control.ModifierKeys == Keys.Alt)
                {
                    if (clickedNub == -1)
                    {
                        PanFlag = true;

                        if (canvas.Width > viewport.ClientSize.Width && canvas.Height > viewport.ClientSize.Height)
                            canvas.Cursor = Cursors.NoMove2D;
                        else if (canvas.Width > viewport.ClientSize.Width)
                            canvas.Cursor = Cursors.NoMoveHoriz;
                        else if (canvas.Height > viewport.ClientSize.Height)
                            canvas.Cursor = Cursors.NoMoveVert;
                        else
                            PanFlag = false;
                    }
                    else
                    {
                        setUndo();
                    }
                }
                else if (e.Button == MouseButtons.Right) //process add or delete
                {
                    if (clickedNub > -1) //delete
                    {
                        #region delete
                        if (clickedNub == 0)
                            return; //don't delete moveto 

                        setUndo();

                        switch (lt)
                        {
                            case PathType.Straight:
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                break;
                            case PathType.Ellipse:
                                if (clickedNub != 4)
                                    return;
                                Array.Resize(ref canvasPoints, 1);
                                break;
                            case PathType.Cubic:
                                if (getNubType(clickedNub) != 3)
                                    return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                if (MacroCubic.Checked)
                                    CubicAdjust();
                                break;
                            case PathType.Quadratic:
                                if (getNubType(clickedNub) != 3)
                                    return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                break;
                            case PathType.SmoothCubic:
                                if (getNubType(clickedNub) != 3)
                                    return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                for (int i = 1; i < canvasPoints.Length; i++)
                                {
                                    if (getNubType(i) == 1 && i > 3)
                                        canvasPoints[i] = reverseAverage(canvasPoints[i - 2], canvasPoints[i - 1]);
                                }
                                break;
                            case PathType.SmoothQuadratic:
                                if (getNubType(clickedNub) != 3)
                                    return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                for (int i = 1; i < canvasPoints.Length; i++)
                                {
                                    if (getNubType(i) == 1 && i > 3)
                                    {
                                        canvasPoints[i] = reverseAverage(canvasPoints[i - 3], canvasPoints[i - 1]);
                                        if (i < canvasPoints.Length - 1)
                                            canvasPoints[i + 1] = canvasPoints[i];
                                    }
                                }
                                break;
                        }
                        canvas.Refresh();
                        #endregion //delete
                    }
                    else //add new
                    {
                        #region add
                        int len = canvasPoints.Length;
                        if (len >= maxPoints)
                        {
                            MessageBox.Show($"Too many Nubs in Path (Max is {maxPoints})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (lt == PathType.Ellipse && canvasPoints.Length > 2)
                            return;

                        setUndo();

                        int eX = e.X, eY = e.Y;
                        if (Snap.Checked)
                        {
                            eX = (int)(Math.Floor((double)(5 + e.X) / 10) * 10);
                            eY = (int)(Math.Floor((double)(5 + e.Y) / 10) * 10);
                        }
                        StatusBarNubLocation(eX, eY);
                        PointF clickedPoint = PointToCanvasCoord(eX, eY);
                        if (len == 0)//first point
                        {
                            Array.Resize(ref canvasPoints, len + 1);
                            canvasPoints[0] = clickedPoint;
                            countflag = true;
                        }
                        else//not first point
                        {
                            switch (lt)
                            {
                                case PathType.Straight:
                                    Array.Resize(ref canvasPoints, len + 1);
                                    canvasPoints[len] = clickedPoint;

                                    break;
                                case PathType.Ellipse:
                                    Array.Resize(ref canvasPoints, 5);
                                    canvasPoints[4] = clickedPoint;
                                    PointF mid = pointAverage(canvasPoints[0], canvasPoints[4]);
                                    PointF mid2 = ThirdPoint(canvasPoints[0], mid, true, 1f);
                                    canvasPoints[1] = pointAverage(canvasPoints[0], mid2);
                                    canvasPoints[2] = pointAverage(canvasPoints[4], mid2);
                                    canvasPoints[3] = ThirdPoint(canvasPoints[0], mid, false, 1f);
                                    break;

                                case PathType.Cubic:
                                    Array.Resize(ref canvasPoints, len + 3);

                                    canvasPoints[len + 2] = clickedPoint;
                                    if (MacroCubic.Checked)
                                    {
                                        CubicAdjust();
                                    }
                                    else
                                    {
                                        PointF mid4 = new PointF();
                                        if (len > 1)
                                        {
                                            PointF mid3 = reverseAverage(canvasPoints[len - 1], canvasPoints[len - 2]);
                                            mid4 = AsymRevAverage(canvasPoints[len - 4], canvasPoints[len - 1], canvasPoints[len + 2], mid3);
                                        }
                                        else
                                        {
                                            PointF mid3 = pointAverage(canvasPoints[len - 1], canvasPoints[len + 2]);
                                            mid4 = ThirdPoint(canvasPoints[len - 1], mid3, true, 1f);
                                        }
                                        canvasPoints[len] = pointAverage(canvasPoints[len - 1], mid4);
                                        canvasPoints[len + 1] = pointAverage(canvasPoints[len + 2], mid4);
                                    }

                                    break;
                                case PathType.Quadratic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = clickedPoint;
                                    PointF tmp = new PointF();
                                    //add
                                    if (len > 1)
                                    {
                                        tmp = AsymRevAverage(canvasPoints[len - 4], canvasPoints[len - 1], canvasPoints[len + 2], canvasPoints[len - 2]);
                                    }
                                    else
                                    {
                                        //add end
                                        canvasPoints[len + 1] = ThirdPoint(canvasPoints[len - 1], canvasPoints[len + 2], true, .5f);
                                        canvasPoints[len] = ThirdPoint(canvasPoints[len + 2], canvasPoints[len - 1], false, .5f);
                                        tmp = pointAverage(canvasPoints[len + 1], canvasPoints[len]);
                                    }
                                    canvasPoints[len + 1] = tmp;
                                    canvasPoints[len] = tmp;
                                    break;

                                case PathType.SmoothCubic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = clickedPoint;
                                    //startchange
                                    PointF mid6 = new PointF();
                                    if (len > 1)
                                    {
                                        PointF mid5 = reverseAverage(canvasPoints[len - 1], canvasPoints[len - 2]);
                                        mid6 = AsymRevAverage(canvasPoints[len - 4], canvasPoints[len - 1], canvasPoints[len + 2], mid5);
                                    }
                                    else
                                    {
                                        PointF mid5 = pointAverage(canvasPoints[len - 1], canvasPoints[len + 2]);
                                        mid6 = ThirdPoint(canvasPoints[len - 1], mid5, true, 1f);
                                    }

                                    canvasPoints[len + 1] = pointAverage(mid6, canvasPoints[len + 2]);
                                    if (len > 1)
                                    {
                                        canvasPoints[len] = reverseAverage(canvasPoints[len - 2], canvasPoints[len - 1]);
                                    }
                                    else
                                    {
                                        canvasPoints[1] = canvasPoints[0];
                                    }

                                    break;
                                case PathType.SmoothQuadratic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = clickedPoint;
                                    if (len > 1)
                                    {
                                        canvasPoints[len] = reverseAverage(canvasPoints[len - 2], canvasPoints[len - 1]);
                                        canvasPoints[len + 1] = canvasPoints[len];
                                    }
                                    else
                                    {
                                        canvasPoints[1] = canvasPoints[0];
                                        canvasPoints[2] = canvasPoints[0];
                                    }
                                    break;
                            }
                        }

                        canvas.Refresh();
                        #endregion //add
                    }

                    if (LineList.SelectedIndex != -1 && clickedNub != 0)
                        UpdateExistingPath();
                }
                else if (Control.ModifierKeys == Keys.Shift && e.Button == MouseButtons.Left)
                {
                    if (canvasPoints.Length != 0)
                    {
                        if (clickedNub != -1)
                        {
                            setUndo();
                            MoveFlag = true;
                            canvas.Cursor = Cursors.SizeAll;
                        }
                    }
                    else if (LineList.Items.Count > 0)
                    {
                        setUndo();
                        MoveFlag = true;
                        canvas.Cursor = Cursors.SizeAll;
                    }
                }
                else if (e.Button == MouseButtons.Left)
                {
                    if (clickedNub == -1)
                    {
                        int clickedPath = getNearestPath(bhit);
                        if (clickedPath != -1)
                        {
                            LineList.SelectedIndex = clickedPath;

                            for (int i = 0; i < canvasPoints.Length; i++)
                            {
                                PointF nub = CanvasCoordToPoint(canvasPoints[i].X, canvasPoints[i].Y);
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
                        PointF nub = CanvasCoordToPoint(canvasPoints[clickedNub].X, canvasPoints[clickedNub].Y);
                        StatusBarNubLocation((int)Math.Round(nub.X), (int)Math.Round(nub.Y));
                    }
                }
                else if (e.Button == MouseButtons.Middle)
                {
                    PanFlag = true;

                    if (canvas.Width > viewport.ClientSize.Width && canvas.Height > viewport.ClientSize.Height)
                        canvas.Cursor = Cursors.NoMove2D;
                    else if (canvas.Width > viewport.ClientSize.Width)
                        canvas.Cursor = Cursors.NoMoveHoriz;
                    else if (canvas.Height > viewport.ClientSize.Height)
                        canvas.Cursor = Cursors.NoMoveVert;
                    else
                        PanFlag = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            PanFlag = false;
            MoveFlag = false;
            clickedNub = -1;
            canvas.Refresh();
            canvas.Cursor = Cursors.Default;
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            StatusBarMouseLocation(e.X, e.Y);

            PictureBox s = (PictureBox)sender;

            int i = clickedNub;
            int nubType = getNubType(clickedNub);

            int eX = e.X,
                eY = e.Y;
            if (Snap.Checked)
            {
                eX = (int)(Math.Floor((double)(5 + eX) / 10) * 10);
                eY = (int)(Math.Floor((double)(5 + eY) / 10) * 10);
            }

            if (!s.ClientRectangle.Contains(eX, eY))
            {
                eX = eX.Clamp(s.ClientRectangle.Left, s.ClientRectangle.Right);
                eY = eY.Clamp(s.ClientRectangle.Top, s.ClientRectangle.Bottom);
            }

            PointF mapPoint = new PointF((float)eX / s.ClientSize.Width, (float)eY / s.ClientSize.Height);
            PathType lt = getPathType();

            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    //left shift move line or path
                    if (MoveFlag && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        if (canvasPoints.Length != 0 && i > -1 && i < canvasPoints.Length)
                        {
                            StatusBarNubLocation(eX, eY);

                            PointF oldp = canvasPoints[i];

                            switch (lt)
                            {
                                case PathType.Straight:
                                case PathType.Cubic:
                                case PathType.Quadratic:
                                case PathType.SmoothCubic:
                                case PathType.SmoothQuadratic:
                                case PathType.Ellipse:
                                    for (int j = 0; j < canvasPoints.Length; j++)
                                    {
                                        canvasPoints[j] = movePoint(oldp, mapPoint, canvasPoints[j]);
                                    }
                                    break;
                            }
                        }
                        else if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
                        {
                            StatusBarNubLocation(eX, eY);

                            for (int k = 0; k < Lines.Count; k++)
                            {
                                int t = Lines[k].LineType;
                                PointF[] pl = Lines[k].Lines;
                                switch (t)
                                {
                                    case (int)PathType.Straight:
                                    case (int)PathType.Cubic:
                                    case (int)PathType.Quadratic:
                                    case (int)PathType.SmoothCubic:
                                    case (int)PathType.SmoothQuadratic:
                                    case (int)PathType.Ellipse:
                                        for (int j = 0; j < pl.Length; j++)
                                        {
                                            pl[j] = movePoint(MoveStart, mapPoint, pl[j]);
                                        }
                                        break;
                                }
                            }
                            MoveStart = mapPoint;
                        }
                    } //no shift movepoint
                    else if (canvasPoints.Length != 0 && i > 0 && i < canvasPoints.Length)
                    {
                        StatusBarNubLocation(eX, eY);

                        PointF oldp = canvasPoints[i];
                        switch (lt)
                        {
                            case PathType.Straight:
                                canvasPoints[i] = mapPoint;
                                break;
                            case PathType.Ellipse:
                                canvasPoints[i] = mapPoint;
                                break;
                            case PathType.Cubic:

                                #region cubic

                                oldp = canvasPoints[i];
                                if (nubType == 0)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if (canvasPoints.Length > 1)
                                        canvasPoints[i + 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                }
                                else if (nubType == 1 || nubType == 2)
                                {
                                    canvasPoints[i] = mapPoint;
                                }
                                else if (nubType == 3)
                                {
                                    canvasPoints[i] = mapPoint;
                                    canvasPoints[i - 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i - 1]);
                                    if ((i + 1) < canvasPoints.Length)
                                        canvasPoints[i + 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                }
                                if (MacroCubic.Checked)
                                    CubicAdjust();

                                #endregion

                                break;
                            case PathType.Quadratic:

                                #region Quadratic

                                oldp = canvasPoints[i];
                                if (nubType == 0)
                                {
                                    canvasPoints[i] = mapPoint;
                                }
                                else if (nubType == 1)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if ((i + 1) < canvasPoints.Length)
                                        canvasPoints[i + 1] = canvasPoints[i];
                                }
                                else if (nubType == 2)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if ((i - 1) > 0)
                                        canvasPoints[i - 1] = canvasPoints[i];
                                }
                                else if (nubType == 3)
                                {
                                    if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                    {
                                        //online
                                        if (i == canvasPoints.Length - 1)
                                        {
                                            PointF rtmp = reverseAverage(canvasPoints[i - 1], canvasPoints[i]);
                                            canvasPoints[i] = onLinePoint(canvasPoints[i - 1], rtmp, mapPoint);
                                        }
                                        else
                                        {
                                            canvasPoints[i] =
                                                onLinePoint(canvasPoints[i - 1], canvasPoints[i + 1], mapPoint);
                                        }
                                    }
                                    else
                                    {
                                        canvasPoints[i] = mapPoint;
                                    }
                                }

                                #endregion

                                break;
                            case PathType.SmoothCubic:

                                #region smooth Cubic

                                oldp = canvasPoints[i];
                                if (nubType == 0)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if (canvasPoints.Length > 1)
                                        canvasPoints[i + 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                    canvasPoints[1] = canvasPoints[0];
                                }
                                else if (nubType == 1)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if (i > 1)
                                    {
                                        canvasPoints[i - 2] = reverseAverage(canvasPoints[i], canvasPoints[i - 1]);
                                    }
                                    else
                                    {
                                        canvasPoints[1] = canvasPoints[0];
                                    }
                                }
                                else if (nubType == 2)
                                {
                                    canvasPoints[i] = mapPoint;
                                    if (i < canvasPoints.Length - 2)
                                    {
                                        canvasPoints[i + 2] = reverseAverage(canvasPoints[i], canvasPoints[i + 1]);
                                    }
                                }
                                else if (nubType == 3)
                                {
                                    canvasPoints[i] = mapPoint;
                                    canvasPoints[i - 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i - 1]);
                                    if ((i + 1) < canvasPoints.Length)
                                        canvasPoints[i + 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                }

                                #endregion

                                break;
                            case PathType.SmoothQuadratic:

                                #region Smooth Quadratic

                                oldp = canvasPoints[i];
                                if (nubType == 0)
                                {
                                    canvasPoints[i] = mapPoint;
                                }
                                else if (nubType == 3)
                                {
                                    canvasPoints[i] = mapPoint;
                                }
                                for (int j = 0; j < canvasPoints.Length; j++)
                                {
                                    if (getNubType(j) == 1 && j > 1)
                                    {
                                        canvasPoints[j] = reverseAverage(canvasPoints[j - 3], canvasPoints[j - 1]);
                                        canvasPoints[j + 1] = canvasPoints[j];
                                    }
                                }

                                #endregion

                                break;
                        }
                    } //move first point
                    else if (canvasPoints.Length != 0 && i == 0)
                    {
                        StatusBarNubLocation(eX, eY);

                        PointF oldp = canvasPoints[i];

                        if (nubType == 0) //special quadratic
                        {
                            switch (lt)
                            {
                                case PathType.Straight:
                                    canvasPoints[i] = mapPoint;
                                    break;
                                case PathType.Ellipse:
                                    canvasPoints[i] = mapPoint;
                                    break;
                                case PathType.Cubic:
                                case PathType.SmoothCubic:
                                    canvasPoints[i] = mapPoint;
                                    if (canvasPoints.Length > 1)
                                        canvasPoints[i + 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                    break;
                                case PathType.Quadratic:
                                    if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                    {
                                        if (canvasPoints.Length == 1)
                                        {
                                            canvasPoints[i] = mapPoint;
                                        }
                                        else
                                        {
                                            PointF rtmp = reverseAverage(canvasPoints[i + 1], canvasPoints[i]);
                                            canvasPoints[i] = onLinePoint(canvasPoints[i + 1], rtmp, mapPoint);
                                        }
                                    }
                                    else
                                    {
                                        canvasPoints[i] = mapPoint;
                                    }
                                    break;
                                case PathType.SmoothQuadratic:
                                    canvasPoints[0] = mapPoint;
                                    if (canvasPoints.Length > 1)
                                        canvasPoints[1] = mapPoint;
                                    for (int j = 0; j < canvasPoints.Length; j++)
                                    {
                                        if (getNubType(j) == 1 && j > 1)
                                        {
                                            canvasPoints[j] =
                                                reverseAverage(canvasPoints[j - 3], canvasPoints[j - 1]);
                                            canvasPoints[j + 1] = canvasPoints[j];
                                        }
                                    }
                                    break;
                            }
                        }
                    } //Pan zoomed
                    else if (PanFlag)
                    {
                        Pan();
                    }

                    canvas.Refresh();
                }
                else if (e.Button == MouseButtons.Middle && PanFlag)
                {
                    Pan();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            void Pan()
            {
                int mpx = (int)(mapPoint.X * 100);
                int msx = (int)(MoveStart.X * 100);
                int mpy = (int)(mapPoint.Y * 100);
                int msy = (int)(MoveStart.Y * 100);
                int tx = 10 * (mpx - msx);
                int ty = 10 * (mpy - msy);

                int maxMoveX = canvas.Width - viewport.ClientSize.Width;
                int maxMoveY = canvas.Height - viewport.ClientSize.Height;

                Point pannedCanvasPos = canvas.Location;
                if (canvas.Width > viewport.ClientSize.Width)
                    pannedCanvasPos.X = (canvas.Location.X + tx < -maxMoveX) ? -maxMoveX : (canvas.Location.X + tx > 0) ? 0 : canvas.Location.X + tx;
                if (canvas.Height > viewport.ClientSize.Height)
                    pannedCanvasPos.Y = (canvas.Location.Y + ty < -maxMoveY) ? -maxMoveY : (canvas.Location.Y + ty > 0) ? 0 : canvas.Location.Y + ty;
                canvas.Location = pannedCanvasPos;

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

        private static PointF[] RemoveAt(PointF[] source, int index)
        {
            PointF[] dest = new PointF[source.Length - 1];
            if (index > 0)
                Array.Copy(source, 0, dest, 0, index);

            if (index < source.Length - 1)
                Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

            return dest;
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

        private static PointF PathAverage(PointF[] p)
        {
            if (p.Length == 0)
                return Point.Empty;

            float x = 0, y = 0;
            foreach (PointF pt in p)
            {
                x += pt.X;
                y += pt.Y;
            }
            return new PointF(x / p.Length, y / p.Length);
        }

        private static float pythag(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
        #endregion

        #region Rotation Knob functions
        private void resetRotation()
        {
            RotationKnob.Value = 180;
            toolTip1.SetToolTip(RotationKnob, "0.0\u00B0");
            lastRot = 180;
        }

        private void RotationKnob_ValueChanged(object sender, float e)
        {
            toolTip1.SetToolTip(RotationKnob, $"{e - 180f:0.0}\u00B0");

            double rad = (lastRot - e) * Math.PI / 180;
            lastRot = e;


            if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
            {
                AveragePoint = new PointF(.5f, .5f);
                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] tmp = Lines[k].Lines;

                    for (int i = 0; i < tmp.Length; i++)
                    {
                        double x = tmp[i].X - AveragePoint.X;
                        double y = tmp[i].Y - AveragePoint.Y;
                        double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + AveragePoint.X;
                        double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + AveragePoint.Y;

                        tmp[i] = new PointF((float)nx, (float)ny);
                    }
                }
            }
            else if (canvasPoints.Length > 1)
            {
                PointF[] tmp = new PointF[canvasPoints.Length];
                Array.Copy(canvasPoints, tmp, canvasPoints.Length);
                AveragePoint = PathAverage(tmp);

                for (int i = 0; i < tmp.Length; i++)
                {
                    double x = tmp[i].X - AveragePoint.X;
                    double y = tmp[i].Y - AveragePoint.Y;
                    double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + AveragePoint.X;
                    double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + AveragePoint.Y;

                    tmp[i] = new PointF((float)nx, (float)ny);
                }
                canvasPoints = tmp;

                if (LineList.SelectedIndex != -1)
                    UpdateExistingPath();
            }

            canvas.Refresh();
        }

        private void RotationKnob_MouseDown(object sender, MouseEventArgs e)
        {
            DrawAverage = true;
            setUndo();
            toolTip1.Show($"{RotationKnob.Value:0.0}\u00B0", RotationKnob);
        }

        private void RotationKnob_MouseUp(object sender, MouseEventArgs e)
        {
            DrawAverage = false;
            canvas.Refresh();
            toolTip1.Hide(RotationKnob);
        }
        #endregion

        #region Misc Helper functions
        private void UpdateExistingPath()
        {
            Lines[LineList.SelectedIndex] = new PData(canvasPoints, ClosePath.Checked, (int)getPathType(), (Arc.CheckState == CheckState.Checked),
                (Sweep.CheckState == CheckState.Checked), Lines[LineList.SelectedIndex].Alias, CloseContPaths.Checked);
            LineList.Items[LineList.SelectedIndex] = LineNames[(int)getPathType()];
        }

        private void AddNewPath(bool deSelected = false)
        {
            if (canvasPoints.Length <= 1)
                return;

            if (Lines.Count < maxPaths)
            {
                setUndo(deSelected);
                if (MacroCircle.Checked && getPathType() == PathType.Ellipse)
                {
                    if (canvasPoints.Length < 5)
                        return;
                    PointF mid = pointAverage(canvasPoints[0], canvasPoints[4]);
                    canvasPoints[1] = canvasPoints[0];
                    canvasPoints[2] = canvasPoints[4];
                    canvasPoints[3] = mid;
                    Lines.Add(new PData(canvasPoints, false, (int)getPathType(), (Arc.CheckState == CheckState.Checked), (Sweep.CheckState == CheckState.Checked), string.Empty, false));
                    LineList.Items.Add(LineNames[(int)PathType.Ellipse]);
                    PointF[] tmp = new PointF[canvasPoints.Length];
                    //fix
                    tmp[0] = canvasPoints[4];
                    tmp[4] = canvasPoints[0];
                    tmp[3] = canvasPoints[3];
                    tmp[1] = tmp[0];
                    tmp[2] = tmp[4];
                    //test below
                    Lines.Add(new PData(tmp, false, (int)getPathType(), (Arc.CheckState == CheckState.Checked), (Sweep.CheckState == CheckState.Checked), string.Empty, true));
                    LineList.Items.Add(LineNames[(int)PathType.Ellipse]);
                }
                else if (MacroRect.Checked && getPathType() == PathType.Straight)
                {
                    for (int i = 1; i < canvasPoints.Length; i++)
                    {
                        PointF[] tmp = new PointF[5];
                        tmp[0] = new PointF(canvasPoints[i - 1].X, canvasPoints[i - 1].Y);
                        tmp[1] = new PointF(canvasPoints[i].X, canvasPoints[i - 1].Y);
                        tmp[2] = new PointF(canvasPoints[i].X, canvasPoints[i].Y);
                        tmp[3] = new PointF(canvasPoints[i - 1].X, canvasPoints[i].Y);
                        tmp[4] = new PointF(canvasPoints[i - 1].X, canvasPoints[i - 1].Y);
                        Lines.Add(new PData(tmp, false, (int)getPathType(), (Arc.CheckState == CheckState.Checked), (Sweep.CheckState == CheckState.Checked), string.Empty, false));
                        LineList.Items.Add(LineNames[(int)getPathType()]);
                    }
                }
                else
                {
                    Lines.Add(new PData(canvasPoints, ClosePath.Checked, (int)getPathType(), (Arc.CheckState == CheckState.Checked), (Sweep.CheckState == CheckState.Checked), string.Empty, CloseContPaths.Checked));
                    LineList.Items.Add(LineNames[(int)getPathType()]);
                }
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            resetRotation();

            if (LinkedPaths.Checked)
            {
                PointF hold = canvasPoints[canvasPoints.Length - 1];
                Array.Resize(ref canvasPoints, 1);
                canvasPoints[0] = hold;
            }
            else
            {
                Array.Resize(ref canvasPoints, 0);
            }

            canvas.Refresh();
        }

        private void CubicAdjust()
        {
            PointF mid4 = new PointF();
            PointF mid3 = new PointF();

            PointF[] knots = new PointF[(int)Math.Ceiling((decimal)canvasPoints.Length / 3)];
            for (int ri = 0; ri < knots.Length; ri++) knots[ri] = canvasPoints[ri * 3];

            int n = knots.Length - 1;

            if (n == 1)
            {
                mid3.X = (2 * knots[0].X + knots[1].X) / 3;
                mid3.Y = (2 * knots[0].Y + knots[1].Y) / 3;

                mid4.X = 2 * mid3.X - knots[0].X;
                mid4.Y = 2 * mid3.Y - knots[0].Y;
                canvasPoints[1] = mid3;
                canvasPoints[2] = mid4;
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
                    canvasPoints[ri * 3 + 1] = xy[ri];
                    if (ri < (n - 1))
                    {
                        canvasPoints[ri * 3 + 2] =
                            new PointF(2f * knots[1 + ri].X - xy[1 + ri].X,
                                2f * knots[1 + ri].Y - xy[1 + ri].Y);
                    }
                    else
                    {
                        canvasPoints[ri * 3 + 2] =
                            new PointF((knots[n].X + xy[n - 1].X) / 2,
                                (knots[n].Y + xy[n - 1].Y) / 2);
                    }
                }
            }
        }

        private int getNubType(int nubIndex)
        {
            if (nubIndex == 0)
                return 0; //base

            return ((nubIndex - 1) % 3) + 1; //1 =ctl1,2=ctl2, 3= end point;
        }

        private PathType getPathType()
        {
            return activeType;
        }

        private void setUiForPath(PathType pathType, bool closedPath, bool largeArc, bool revSweep, bool multiClosedPath)
        {
            SuspendLayout();
            MacroCubic.Checked = false;
            MacroCircle.Checked = false;
            MacroRect.Checked = false;
            if (pathType != activeType)
            {
                activeType = pathType;
                PathTypeToggle();
            }
            ClosePath.Checked = closedPath;
            ClosePath.Image = (ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            CloseContPaths.Checked = multiClosedPath;
            CloseContPaths.Image = (CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;
            if (pathType == PathType.Ellipse)
            {
                Arc.CheckState = largeArc ? CheckState.Checked : CheckState.Indeterminate;
                Arc.Image = (Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcSmall : Properties.Resources.ArcLarge;

                Sweep.CheckState = revSweep ? CheckState.Checked : CheckState.Indeterminate;
                Sweep.Image = (Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;
            }
            ResumeLayout();
        }

        private int getNearestPath(RectangleF hit)
        {
            int result = -1;
            if (LineList.Items.Count == 0)
                return -1;
            for (int i = 0; i < LineList.Items.Count; i++)
            {
                PointF[] tmp = Lines[i].Lines;

                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddLines(tmp);

                    gp.Flatten(null, .1f);
                    tmp = gp.PathPoints;
                    for (int j = 0; j < tmp.Length; j++)
                    {
                        // exclude 'control' nubs.
                        switch (Lines[i].LineType)
                        {
                            case 1: // Ellipse (Red)
                                if (j % 4 != 0)
                                    continue;
                                break;
                            case 2: // Cubic (Blue)
                            case 3: // Smooth Cubic (Green)
                            case 4: // Quadratic (Goldenrod)
                                if (j % 3 != 0)
                                    continue;
                                break;
                        }

                        PointF p = CanvasCoordToPoint(tmp[j].X, tmp[j].Y);
                        if (hit.Contains(p))
                        {
                            result = i;
                            break;
                        }
                    }
                    if (result > -1)
                        break;
                }
            }
            return result;
        }

        private void StatusBarMouseLocation(int x, int y)
        {
            int zoomFactor = canvas.Width / canvasBaseSize;
            statusLabelMousePos.Text = $"{Math.Round(x / (float)zoomFactor / DPI)}, {Math.Round(y / (float)zoomFactor / DPI)}";
            statusStrip1.Refresh();
        }

        private void StatusBarNubLocation(int x, int y)
        {
            int zoomFactor = canvas.Width / canvasBaseSize;
            statusLabelNubPos.Text = $"{Math.Round(x / (float)zoomFactor / DPI)}, {Math.Round(y / (float)zoomFactor / DPI)}";
            statusStrip1.Refresh();
        }

        private bool getPathData(float width, float height, out string output)
        {
            string strPath = (SolidFillMenuItem.Checked) ? "F1 " : string.Empty;
            if (Lines.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;


            for (int index = 0; index < Lines.Count; index++)
            {
                PData currentPath = Lines[index];
                int lt = currentPath.LineType;
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
                        strPath += " ";
                    strPath += "M ";
                    strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                    strPath += ",";
                    strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                }
                switch (lt)
                {
                    case (int)PathType.Straight:
                        strPath += " L ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                            strPath += ",";
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                            if (i < line.Length - 1)
                                strPath += ",";
                        }
                        oldx = x; oldy = y;
                        break;
                    case (int)PathType.Ellipse:
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
                    case (int)PathType.Cubic:
                        strPath += " C ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = width * line[i].X;
                            y = height * line[i].Y;
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                            strPath += ",";
                            strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                            if (i < line.Length - 1)
                                strPath += ",";
                            oldx = x; oldy = y;
                        }

                        break;
                    case (int)PathType.Quadratic:
                        strPath += " Q ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 2)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                    strPath += ",";
                                oldx = x; oldy = y;
                            }
                        }

                        break;
                    case (int)PathType.SmoothCubic:
                        strPath += " S ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                    strPath += ",";
                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case (int)PathType.SmoothQuadratic:
                        strPath += " T ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 2 && getNubType(i) != 1)
                            {
                                x = width * line[i].X;
                                y = height * line[i].Y;
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", x);
                                strPath += ",";
                                strPath += string.Format(CultureInfo.InvariantCulture, "{0:0.##}", y);
                                if (i < line.Length - 1)
                                    strPath += ",";
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
            if (Lines.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;
            string[] repstr = { "~1", "~2", "~3" };
            string tmpstr = string.Empty;
            for (int index = 0; index < Lines.Count; index++)
            {
                Application.DoEvents();

                PData currentPath = Lines[index];
                int lt = currentPath.LineType;
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
                switch (lt)
                {
                    case (int)PathType.Straight:

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
                    case (int)PathType.Ellipse:
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
                    case (int)PathType.SmoothCubic:
                    case (int)PathType.Cubic:

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
                    case (int)PathType.SmoothQuadratic:
                    case (int)PathType.Quadratic:

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

        private string scrubNums(string strPath)
        {
            strPath = strPath.ToLower();
            strPath = strPath.Replace(',', ' ');
            string command = "fmlacsqthvz";
            string number = "e.-0123456789";
            string TMP = string.Empty;
            bool alpha = false;
            bool blank = false;

            for (int i = 0; i < strPath.Length; i++)
            {
                string mychar = strPath.Substring(i, 1);
                int isnumber = number.IndexOf(mychar, StringComparison.Ordinal);
                int iscommand = command.IndexOf(mychar, StringComparison.Ordinal);


                if (TMP.Equals(string.Empty))
                {
                    TMP += mychar;
                    alpha = true;
                    blank = false;
                }
                else if (mychar.Equals(" "))
                {
                    alpha = true;
                    blank = true;
                }
                else if (iscommand > -1 && (!alpha || blank))
                {
                    TMP += "," + mychar;
                    alpha = true;
                    blank = false;
                }
                else if (iscommand > -1)
                {
                    TMP += mychar;
                    alpha = true;
                    blank = false;
                }
                else if (isnumber > -1 && (alpha || blank))
                {
                    TMP += "," + mychar;
                    alpha = false;
                    blank = false;
                }
                else if (isnumber > -1)
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
                return;
            PointF[] pts = new PointF[0];
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
            string match = "fmlacsqthvz";
            bool errorflagx = false;
            bool errorflagy = false;
            int errornum = 0;
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
                        if (pts.Length > 1 && LineList.Items.Count < maxPaths)
                            addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);

                        Array.Resize(ref pts, 1);
                        pts[0] = LastPos;

                        lineType = tmpline;
                        closedType = false;
                    }
                    if (strMode != "z")
                        continue;
                }
                int ptype, len = 0;
                switch (strMode)
                {
                    case "n":
                    case "z":
                        Array.Resize(ref pts, pts.Length + 1);
                        pts[pts.Length - 1] = HomePos;
                        break;
                    case "f":
                        //ignore F0 and F1
                        errornum = 12;
                        errorflagx = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        SolidFillMenuItem.Checked = (x == 1);
                        break;
                    case "m":
                        errornum = 1;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        LastPos = PointToCanvasCoord(x, y);
                        HomePos = LastPos;
                        break;

                    case "c":
                    case "l":
                        Array.Resize(ref pts, pts.Length + 1);
                        errornum = 2;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "s":
                        errornum = 9;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        LastPos = PointToCanvasCoord(x, y);
                        len = pts.Length;
                        Array.Resize(ref pts, len + 1);
                        ptype = getNubType(len);
                        if (len > 1)
                        {
                            if (ptype == 1)
                            {
                                Array.Resize(ref pts, len + 2);
                                pts[len + 1] = LastPos;
                                pts[len] = reverseAverage(pts[len - 2], pts[len - 1]);
                            }
                            else if (ptype == 3)
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
                        errornum = 10;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
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
                        errornum = 11;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        //
                        ptype = getNubType(pts.Length - 1);
                        if (ptype == 1)
                        {
                            Array.Resize(ref pts, pts.Length + 1);
                            pts[pts.Length - 1] = LastPos;
                        }

                        break;
                    case "h":
                        Array.Resize(ref pts, pts.Length + 1);
                        y = LastPos.Y;
                        errornum = 3;
                        errorflagx = float.TryParse(str[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        x = x / canvas.ClientSize.Height;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "v":
                        Array.Resize(ref pts, pts.Length + 1);
                        x = LastPos.X;
                        errornum = 4;
                        errorflagy = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        y = y / canvas.ClientSize.Height;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "a":
                        int ptbase = 0;
                        Array.Resize(ref pts, pts.Length + 4);
                        //to
                        errornum = 5;
                        errorflagx = float.TryParse(str[i + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i + 6], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                        if (!errorflagy) break;
                        LastPos = PointToCanvasCoord(x, y);
                        pts[ptbase + 4] = LastPos; //ENDPOINT

                        PointF From = CanvasCoordToPoint(pts[ptbase].X, pts[ptbase].Y);
                        PointF To = new PointF(x, y);

                        PointF mid = pointAverage(From, To);
                        PointF mid2 = ThirdPoint(From, mid, true, 1f);
                        float far = pythag(From, mid);
                        float atan = (float)Math.Atan2(mid2.Y - mid.Y, mid2.X - mid.X);

                        float dist, dist2;
                        errornum = 6;
                        errorflagx = float.TryParse(str[i], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //W
                        if (!errorflagx) break;
                        pts[ptbase + 1] = pointOrbit(mid, atan - (float)Math.PI / 4f, dist);

                        errorflagx = float.TryParse(str[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out dist); //H
                        if (!errorflagx) break;
                        pts[ptbase + 2] = pointOrbit(mid, atan + (float)Math.PI / 4f, dist);

                        errornum = 7;
                        errorflagx = float.TryParse(str[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                        float rot = dist * (float)Math.PI / 180f; //ROT
                        pts[ptbase + 3] = pointOrbit(mid, rot, far);

                        errornum = 8;
                        errorflagx = float.TryParse(str[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out dist2);
                        if (!errorflagy) break;
                        islarge = Convert.ToBoolean(dist);
                        revsweep = Convert.ToBoolean(dist2);

                        i += 6;
                        strMode = "n";
                        break;
                }
                if (!errorflagx || !errorflagy)
                    break;
            }
            if (!errorflagx || !errorflagy || lineType < 0)
            {
                MessageBox.Show("No Line Type, or is not in the StreamGeometry Format", "Not a valid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (pts.Length > 1 && LineList.Items.Count < maxPaths)
                addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);
            canvas.Refresh();
        }

        private PointF pointOrbit(PointF center, float rotation, float distance)
        {
            float x = (float)Math.Cos(rotation) * distance;
            float y = (float)Math.Sin(rotation) * distance;
            return PointToCanvasCoord(center.X + x, center.Y + y);
        }

        private PointF PointToCanvasCoord(float x, float y)
        {
            return new PointF(x / canvas.ClientSize.Width, y / canvas.ClientSize.Height);
        }

        private PointF CanvasCoordToPoint(float x, float y)
        {
            return new PointF(x * canvas.ClientSize.Width, y * canvas.ClientSize.Height);
        }

        private void addPathtoList(PointF[] pbpoint, int lineType, bool closedType, bool islarge, bool revsweep, bool mpmtype)
        {
            if (Lines.Count < maxPaths)
            {
                Lines.Add(new PData(pbpoint, closedType, lineType, islarge, revsweep, string.Empty, mpmtype));
                LineList.Items.Add(LineNames[lineType]);
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearAllPaths()
        {
            Array.Resize(ref canvasPoints, 0);
            statusLabelNubsUsed.Text = $"{canvasPoints.Length}/{maxPoints} Nubs used";
            statusLabelNubPos.Text = "0, 0";

            Lines.Clear();
            LineList.Items.Clear();
            statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";

            canvas.Refresh();
        }

        private string getMyFolder()
        {
            string fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("Software")?.OpenSubKey("PdnDwarves")?.OpenSubKey("ShapeMaker");

            if (rk != null)
            {
                try
                {
                    fp = rk.GetValue("PdnShapeDir").ToString();
                    if (!Directory.Exists(fp))
                        fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }
                catch
                {
                    fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }
            }
            return fp;
        }

        private string getMyProjectFolder()
        {
            string fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("Software")?.OpenSubKey("PdnDwarves")?.OpenSubKey("ShapeMaker");

            if (rk != null)
            {
                try
                {
                    fp = rk.GetValue("ProjectDir").ToString();
                    if (!Directory.Exists(fp))
                        fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }
                catch
                {
                    fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }
            }
            return fp;
        }

        private void saveMyFolder(string filePath)
        {
            RegistryKey key;
            key = Registry.CurrentUser.OpenSubKey("Software", true).CreateSubKey("PdnDwarves").CreateSubKey("ShapeMaker");
            key.SetValue("PdnShapeDir", Path.GetDirectoryName(filePath));
            key.Close();
        }

        private void saveMyProjectFolder(string filePath)
        {
            RegistryKey key;
            key = Registry.CurrentUser.OpenSubKey("Software", true).CreateSubKey("PdnDwarves").CreateSubKey("ShapeMaker");
            key.SetValue("ProjectDir", Path.GetDirectoryName(filePath));
            key.Close();
        }

        private void MakePath()
        {
            int ltype = 0;
            bool ctype = false;
            bool mpmode = false;
            bool islarge = false;
            bool revsweep = false;
            PointF loopBack = new PointF(-9999, -9999);
            PointF Oldxy = new PointF(-9999, -9999);

            Array.Resize(ref PGP, Lines.Count);

            for (int j = 0; j < Lines.Count; j++)
            {
                PointF[] line;
                try
                {
                    PGP[j] = new GraphicsPath();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

                PData currentPath = Lines[j];
                line = currentPath.Lines;
                ltype = currentPath.LineType;
                ctype = currentPath.ClosedType;
                mpmode = currentPath.LoopBack;
                islarge = currentPath.IsLarge;
                revsweep = currentPath.RevSweep;


                PointF[] pts = new PointF[line.Length];
                PointF[] Qpts = new PointF[line.Length];

                Rectangle selection = Selection.GetBoundsInt();
                int selMinDim = Math.Min(selection.Width, selection.Height);
                for (int i = 0; i < line.Length; i++)
                {
                    pts[i].X = (float)OutputScale.Value * selMinDim / 100f * line[i].X + selection.Left;
                    pts[i].Y = (float)OutputScale.Value * selMinDim / 100f * line[i].Y + selection.Top;
                }
                #region cube to quad
                if (ltype == (int)PathType.Quadratic || ltype == (int)PathType.SmoothQuadratic)
                {
                    for (int i = 0; i < line.Length; i++)
                    {
                        int PT = getNubType(i);
                        if (PT == 0)
                        {
                            Qpts[i] = pts[i];
                        }
                        else if (PT == 1)
                        {
                            Qpts[i] = new PointF(pts[i].X * 2f / 3f + pts[i - 1].X * 1f / 3f,
                                pts[i].Y * 2f / 3f + pts[i - 1].Y * 1f / 3f);
                        }
                        else if (PT == 2)
                        {
                            Qpts[i] = new PointF(pts[i - 1].X * 2f / 3f + pts[i + 1].X * 1f / 3f,
                                pts[i - 1].Y * 2f / 3f + pts[i + 1].Y * 1f / 3f);
                        }
                        else if (PT == 3)
                        {
                            Qpts[i] = pts[i];
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

                if (line.Length > 3 && (ltype == (int)PathType.Quadratic || ltype == (int)PathType.SmoothQuadratic))
                {
                    try
                    {
                        PGP[j].AddBeziers(Qpts);
                    }
                    catch
                    {
                    }
                }
                else if (line.Length > 3 && (ltype == (int)PathType.Cubic || ltype == (int)PathType.SmoothCubic))
                {
                    try
                    {
                        PGP[j].AddBeziers(pts);
                    }
                    catch
                    {
                    }
                }
                else if (line.Length > 1 && ltype == (int)PathType.Straight)
                {
                    PGP[j].AddLines(pts);
                }
                else if (line.Length == 5 && ltype == (int)PathType.Ellipse)
                {
                    PointF mid = pointAverage(pts[0], pts[4]);

                    float l = pythag(mid, pts[1]);
                    float h = pythag(mid, pts[2]);
                    float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                    if ((int)h == 0 || (int)l == 0)
                    {
                        PointF[] nullLine = { pts[0], pts[4] };
                        PGP[j].AddLines(nullLine);
                    }
                    else
                    {
                        AddToGraphicsPath(PGP[j], pts[0], l, h, a, (islarge) ? 1 : 0, (revsweep) ? 1 : 0, pts[4]);
                    }
                }

                if (!mpmode)
                {
                    if (ctype && pts.Length > 1)
                    {
                        PointF[] points = { pts[pts.Length - 1], pts[0] };
                        PGP[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                else
                {
                    if (pts.Length > 1)
                    {
                        PointF[] points = { pts[pts.Length - 1], loopBack };
                        PGP[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                #endregion
                Oldxy = pts[pts.Length - 1];
            }
        }

        private double VectorAngle(double ux, double uy, double vx, double vy)
        {
            double ta = Math.Atan2(uy, ux);
            double tb = Math.Atan2(vy, vx);

            if (tb >= ta)
            {
                return tb - ta;
            }

            return twoPI - (ta - tb);
        }

        private void AddToGraphicsPath(GraphicsPath graphicsPath, PointF start, float radiusX, float radiusY, float angle, int size, int sweep, PointF end)
        {
            if (start == end)
                return;

            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);

            if (radiusX == 0.0f && radiusY == 0.0f)
            {
                graphicsPath.AddLine(start, end);
                return;
            }

            double sinPhi = Math.Sin(angle * RadPerDeg);
            double cosPhi = Math.Cos(angle * RadPerDeg);

            double x1dash = cosPhi * (start.X - end.X) / 2.0 + sinPhi * (start.Y - end.Y) / 2.0;
            double y1dash = -sinPhi * (start.X - end.X) / 2.0 + cosPhi * (start.Y - end.Y) / 2.0;

            double root;
            double numerator = radiusX * radiusX * radiusY * radiusY - radiusX * radiusX * y1dash * y1dash - radiusY * radiusY * x1dash * x1dash;

            float rx = radiusX;
            float ry = radiusY;

            if (numerator < 0.0)
            {
                float s = (float)Math.Sqrt(1.0 - numerator / (radiusX * radiusX * radiusY * radiusY));

                rx *= s;
                ry *= s;
                root = 0.0;
            }
            else
            {
                root = ((size == 1 && sweep == 1) || (size == 0 && sweep == 0) ? -1.0 : 1.0) * Math.Sqrt(numerator / (radiusX * radiusX * y1dash * y1dash + radiusY * radiusY * x1dash * x1dash));
            }

            double cxdash = root * rx * y1dash / ry;
            double cydash = -root * ry * x1dash / rx;

            double cx = cosPhi * cxdash - sinPhi * cydash + (start.X + end.X) / 2.0;
            double cy = sinPhi * cxdash + cosPhi * cydash + (start.Y + end.Y) / 2.0;

            double theta1 = VectorAngle(1.0, 0.0, (x1dash - cxdash) / rx, (y1dash - cydash) / ry);
            double dtheta = VectorAngle((x1dash - cxdash) / rx, (y1dash - cydash) / ry, (-x1dash - cxdash) / rx, (-y1dash - cydash) / ry);

            if (sweep == 0 && dtheta > 0)
            {
                dtheta -= 2.0 * Math.PI;
            }
            else if (sweep == 1 && dtheta < 0)
            {
                dtheta += 2.0 * Math.PI;
            }

            int segments = (int)Math.Ceiling((double)Math.Abs(dtheta / (Math.PI / 2.0)));
            double delta = dtheta / segments;
            double t = 8.0 / 3.0 * Math.Sin(delta / 4.0) * Math.Sin(delta / 4.0) / Math.Sin(delta / 2.0);

            double startX = start.X;
            double startY = start.Y;

            for (int i = 0; i < segments; ++i)
            {
                double cosTheta1 = Math.Cos(theta1);
                double sinTheta1 = Math.Sin(theta1);
                double theta2 = theta1 + delta;
                double cosTheta2 = Math.Cos(theta2);
                double sinTheta2 = Math.Sin(theta2);

                double endpointX = cosPhi * rx * cosTheta2 - sinPhi * ry * sinTheta2 + cx;
                double endpointY = sinPhi * rx * cosTheta2 + cosPhi * ry * sinTheta2 + cy;

                double dx1 = t * (-cosPhi * rx * sinTheta1 - sinPhi * ry * cosTheta1);
                double dy1 = t * (-sinPhi * rx * sinTheta1 + cosPhi * ry * cosTheta1);

                double dxe = t * (cosPhi * rx * sinTheta2 + sinPhi * ry * cosTheta2);
                double dye = t * (sinPhi * rx * sinTheta2 - cosPhi * ry * cosTheta2);

                graphicsPath.AddBezier((float)startX, (float)startY, (float)(startX + dx1), (float)(startY + dy1),
                    (float)(endpointX + dxe), (float)(endpointY + dye), (float)endpointX, (float)endpointY);

                theta1 = theta2;
                startX = (float)endpointX;
                startY = (float)endpointY;
            }
        }

        private bool InView()
        {
            if (canvasPoints.Length > 0)
            {
                for (int j = 0; j < canvasPoints.Length; j++)
                {
                    if (canvasPoints[j].X > 1.5f || canvasPoints[j].Y > 1.5f)
                        return false;
                }
            }

            if (Lines.Count > 0)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] pl = Lines[k].Lines;
                    for (int j = 0; j < pl.Length; j++)
                    {
                        if (pl[j].X > 1.5f || pl[j].Y > 1.5f)
                            return false;
                    }
                }
            }
            return true;
        }
        #endregion

        #region Path List functions
        private void LineList_DoubleClick(object sender, EventArgs e)
        {
            if (LineList.Items.Count == 0 || LineList.SelectedItem == null)
                return;
            string s = Microsoft.VisualBasic.Interaction.InputBox("Please enter a name for this path.", "Path Name", LineList.SelectedItem.ToString(), -1, -1).Trim();
            if (!s.IsNullOrEmpty())
                Lines[LineList.SelectedIndex].Alias = s;
        }

        private void LineList_SelectedValueChanged(object sender, EventArgs e)
        {
            if (isNewPath && canvasPoints.Length > 1)
                AddNewPath(true);
            isNewPath = false;


            if (LineList.SelectedIndex == -1)
                return;

            if ((LineList.Items.Count > 0) && (LineList.SelectedIndex < Lines.Count))
            {
                PData selectedPath = Lines[LineList.SelectedIndex];
                setUiForPath((PathType)selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
                canvasPoints = selectedPath.Lines;
            }
            canvas.Refresh();
        }

        private void LineList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            int itemIndex = e.Index;
            if (itemIndex >= 0 && itemIndex < LineList.Items.Count)
            {
                PData itemPath = Lines[itemIndex];

                string itemText;
                if (!itemPath.Alias.IsNullOrEmpty())
                {
                    itemText = itemPath.Alias;
                }
                else
                {
                    itemText = LineList.Items[itemIndex].ToString();
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
                    using (SolidBrush backgroundColorBrush = new SolidBrush(LineColorsLight[itemPath.LineType]))
                        e.Graphics.FillRectangle(backgroundColorBrush, e.Bounds);
                }

                using (StringFormat vCenter = new StringFormat { LineAlignment = StringAlignment.Center })
                using (SolidBrush itemTextColorBrush = new SolidBrush(LineColors[itemPath.LineType]))
                    e.Graphics.DrawString(itemText, e.Font, itemTextColorBrush, e.Bounds, vCenter);
            }

            e.DrawFocusRectangle();
        }

        private void removebtn_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex == -1 || LineList.Items.Count == 0 || LineList.SelectedIndex >= Lines.Count)
                return;

            setUndo();

            int spi = LineList.SelectedIndex;
            Lines.RemoveAt(spi);
            LineList.Items.RemoveAt(spi);
            canvasPoints = new PointF[0];
            LineList.SelectedIndex = -1;

            canvas.Refresh();
        }

        private void Clonebtn_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex == -1 || canvasPoints.Length == 0)
                return;

            if (Lines.Count < maxPaths)
            {
                setUndo();

                PointF[] tmp = new PointF[canvasPoints.Length];
                Array.Copy(canvasPoints, tmp, canvasPoints.Length);
                Lines.Add(new PData(tmp, ClosePath.Checked, (int)getPathType(), (Arc.CheckState == CheckState.Checked), (Sweep.CheckState == CheckState.Checked), string.Empty, CloseContPaths.Checked));
                LineList.Items.Add(LineNames[(int)getPathType()]);
                LineList.SelectedIndex = LineList.Items.Count - 1;

                canvas.Refresh();
            }
            else
            {
                MessageBox.Show($"Too many Paths in Shape (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DNList_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex > -1 && LineList.SelectedIndex < LineList.Items.Count - 1)
            {
                LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                ReOrderPath(LineList.SelectedIndex);
                LineList.SelectedValueChanged += LineList_SelectedValueChanged;
                LineList.SelectedIndex++;
            }
        }

        private void upList_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex > 0)
            {
                LineList.SelectedValueChanged -= LineList_SelectedValueChanged;
                ReOrderPath(LineList.SelectedIndex - 1);
                LineList.SelectedValueChanged += LineList_SelectedValueChanged;
                LineList.SelectedIndex--;
            }
        }

        private void ReOrderPath(int index)
        {
            if (index == -1)
                return;

            PData pd1 = Lines[index];
            string LineTxt1 = LineList.Items[index].ToString();

            PData pd2 = Lines[index + 1];
            string LineTxt2 = LineList.Items[index + 1].ToString();

            Lines[index] = pd2;
            LineList.Items[index] = LineTxt2;

            Lines[index + 1] = pd1;
            LineList.Items[index + 1] = LineTxt1;
        }

        private void ToggleUpDownButtons()
        {
            if (LineList.Items.Count < 2 || LineList.SelectedIndex == -1)
            {
                upList.Enabled = false;
                DNList.Enabled = false;
            }
            else if (LineList.SelectedIndex == 0)
            {
                upList.Enabled = false;
                DNList.Enabled = true;
            }
            else if (LineList.SelectedIndex == LineList.Items.Count - 1)
            {
                upList.Enabled = true;
                DNList.Enabled = false;
            }
            else
            {
                upList.Enabled = true;
                DNList.Enabled = true;
            }
        }
        #endregion

        #region Zoom functions
        private void splitButtonZoom_ButtonClick(object sender, EventArgs e)
        {
            PanFlag = false;

            int zoomFactor = canvas.Width / canvasBaseSize;
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
            Point viewportCenter = new Point(viewport.ClientSize.Width / 2, viewport.ClientSize.Height / 2);
            ZoomToFactor(zoomFactor, viewportCenter);
        }

        private void ZoomToFactor(int zoomFactor, Point zoomPoint)
        {
            int oldZoomFactor = canvas.Width / canvasBaseSize;
            if (oldZoomFactor == zoomFactor)
                return;

            int newDimension = canvasBaseSize * zoomFactor;

            Point zoomedCanvasPos = new Point
            {
                X = (canvas.Location.X - zoomPoint.X) * newDimension / canvas.Width + zoomPoint.X,
                Y = (canvas.Location.Y - zoomPoint.Y) * newDimension / canvas.Height + zoomPoint.Y
            };

            // Clamp the canvas location; we're not overscrolling... yet
            int minX = (viewport.ClientSize.Width > newDimension) ? (viewport.ClientSize.Width - newDimension) / 2 : viewport.ClientSize.Width - newDimension;
            int maxX = (viewport.ClientSize.Width > newDimension) ? (viewport.ClientSize.Width - newDimension) / 2 : 0;
            zoomedCanvasPos.X = zoomedCanvasPos.X.Clamp(minX, maxX);

            int minY = (viewport.ClientSize.Height > newDimension) ? (viewport.ClientSize.Height - newDimension) / 2 : viewport.ClientSize.Height - newDimension;
            int maxY = (viewport.ClientSize.Height > newDimension) ? (viewport.ClientSize.Height - newDimension) / 2 : 0;
            zoomedCanvasPos.Y = zoomedCanvasPos.Y.Clamp(minY, maxY);

            // to avoid flicker, the order of execution is important
            if (oldZoomFactor > zoomFactor) // Zooming Out
            {
                canvas.Location = zoomedCanvasPos;
                canvas.Width = newDimension;
                canvas.Height = newDimension;
            }
            else // Zooming In
            {
                canvas.Width = newDimension;
                canvas.Height = newDimension;
                canvas.Location = zoomedCanvasPos;
            }
            canvas.Refresh();

            splitButtonZoom.Text = $"Zoom {zoomFactor}x";

            UpdateScrollBars();
        }

        private void canvas_MouseEnter(object sender, EventArgs e)
        {
            CanScrollZoom = true;
            hadFocus = this.ActiveControl;
            canvas.Focus();
        }

        private void canvas_MouseLeave(object sender, EventArgs e)
        {
            CanScrollZoom = false;
            if (hadFocus != null)
                hadFocus.Focus();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!CanScrollZoom)
                return;

            int delta = Math.Sign(e.Delta);
            int oldZoomFactor = canvas.Width / canvasBaseSize;
            if ((delta > 0 && oldZoomFactor == 8) || (delta < 0 && oldZoomFactor == 1))
                return;

            int zoomFactor = (delta > 0) ? oldZoomFactor * 2 : oldZoomFactor / 2;
            Point mousePosition = new Point(e.X - viewport.Location.X, e.Y - viewport.Location.Y);
            ZoomToFactor(zoomFactor, mousePosition);

            base.OnMouseWheel(e);
        }
        #endregion

        #region Position Bar functions
        private void horScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            canvas.Left = -horScrollBar.Value;
        }

        private void verScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            canvas.Top = -verScrollBar.Value;
        }

        private void UpdateScrollBars()
        {
            horScrollBar.Visible = canvas.Width > viewport.ClientSize.Width;
            if (horScrollBar.Visible)
            {
                horScrollBar.Maximum = canvas.Width;
                horScrollBar.Value = Math.Abs(canvas.Location.X);
                horScrollBar.LargeChange = viewport.ClientSize.Width;
            }

            verScrollBar.Visible = canvas.Height > viewport.ClientSize.Height;
            if (verScrollBar.Visible)
            {
                verScrollBar.Maximum = canvas.Height;
                verScrollBar.Value = Math.Abs(canvas.Location.Y);
                verScrollBar.LargeChange = viewport.ClientSize.Height;
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
                FigureName.Text = "Untitled";
            }
        }

        private void openProject_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog OFD = new OpenFileDialog())
            {
                OFD.InitialDirectory = getMyProjectFolder();
                OFD.Filter = "Project Files (.dhp)|*.dhp|All Files (*.*)|*.*";
                OFD.FilterIndex = 1;
                OFD.RestoreDirectory = false;

                if (OFD.ShowDialog() != DialogResult.OK)
                    return;

                if (!File.Exists(OFD.FileName))
                {
                    MessageBox.Show("Specified file not found", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                saveMyProjectFolder(OFD.FileName);

                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                try
                {
                    using (FileStream stream = File.OpenRead(OFD.FileName))
                    {
                        ArrayList projectPaths = (ArrayList)ser.Deserialize(stream);

                        if (projectPaths.Count == 0)
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

                        PData documentProps = projectPaths[projectPaths.Count - 1] as PData;
                        FigureName.Text = documentProps.Meta;
                        SolidFillMenuItem.Checked = documentProps.SolidFill;
                        foreach (PData path in projectPaths)
                        {
                            Lines.Add(path);
                            LineList.Items.Add(LineNames[path.LineType]);
                        }

                        ZoomToFactor(1);
                        resetRotation();
                        resetHistory();
                        canvas.Refresh();
                        AddToRecents(OFD.FileName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Incorrect Format\r\n" + ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void saveProject_Click(object sender, EventArgs e)
        {
            if (Lines.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string TMP = string.Empty;
            bool r = getPathData((int)(OutputScale.Value * canvas.ClientSize.Width / 100), (int)(OutputScale.Value * canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string figure = FigureName.Text;
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            figure = rgx.Replace(figure, string.Empty);
            figure = (figure.IsNullOrEmpty()) ? "Untitled" : figure;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = figure;
                sfd.InitialDirectory = getMyProjectFolder();
                sfd.Filter = "Project Files (.dhp)|*.dhp|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                ArrayList paths = new ArrayList(Lines);
                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                (paths[paths.Count - 1] as PData).Meta = FigureName.Text;
                (paths[paths.Count - 1] as PData).SolidFill = SolidFillMenuItem.Checked;
                using (FileStream stream = File.Open(sfd.FileName, FileMode.Create))
                    ser.Serialize(stream, paths);

                AddToRecents(sfd.FileName);
            }
        }

        private void exportPndShape_Click(object sender, EventArgs e)
        {
            if (Lines.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ZoomToFactor(1);
            string TMP = string.Empty;
            bool r = getPathData((int)(OutputScale.Value * canvas.ClientSize.Width / 100), (int)(OutputScale.Value * canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string output = Properties.Resources.BaseString;
            string figure = FigureName.Text;
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            figure = rgx.Replace(figure, string.Empty);
            figure = (figure.IsNullOrEmpty()) ? "Untitled" : figure;
            output = output.Replace("~1", figure);
            output = output.Replace("~2", TMP);
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = figure;
                sfd.InitialDirectory = getMyFolder();
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                saveMyFolder(sfd.FileName);

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("The shape has been exported as a XAML file for use in paint.net.\r\n\r\nPlease note that paint.net needs to be restarted to use the shape.", "Paint.net Shape Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportPG_Click(object sender, EventArgs e)
        {
            if (Lines.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string TMP = string.Empty;
            bool r = getPGPathData((int)(OutputScale.Value * canvas.ClientSize.Width / 100), (int)(OutputScale.Value * canvas.ClientSize.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ZoomToFactor(1);
            string output = Properties.Resources.PGBaseString;
            string figure = FigureName.Text;
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            figure = rgx.Replace(figure, string.Empty);
            figure = (figure.IsNullOrEmpty()) ? "Untitled" : figure;
            output = output.Replace("~1", figure);
            output = output.Replace("~2", TMP);
            if (SolidFillMenuItem.Checked)
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
                sfd.InitialDirectory = getMyFolder();
                sfd.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                sfd.FilterIndex = 1;
                sfd.AddExtension = true;

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                saveMyFolder(sfd.FileName);

                File.WriteAllText(sfd.FileName, output);
                MessageBox.Show("PathGeometry XAML Saved", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void importPdnShape_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog OFD = new OpenFileDialog())
            {
                OFD.InitialDirectory = getMyFolder();
                OFD.Filter = "XAML Files (.xaml)|*.xaml|All Files (*.*)|*.*";
                OFD.FilterIndex = 1;
                OFD.RestoreDirectory = false;

                if (OFD.ShowDialog() != DialogResult.OK)
                    return;

                if (!File.Exists(OFD.FileName))
                {
                    MessageBox.Show("Specified file not found", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                saveMyFolder(OFD.FileName);
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
                        FigureName.Text = data;
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
                    MessageBox.Show("Incorrect Format", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void clearAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete All Paths?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                setUndo();
                ClearAllPaths();
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
            if (Lines.Count == 0)
            {
                MessageBox.Show("Nothing to Copy", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ZoomToFactor(1);
            string TMP = string.Empty;
            bool r = getPathData((int)(OutputScale.Value * canvas.ClientSize.Width / 100), (int)(OutputScale.Value * canvas.ClientSize.Height / 100), out TMP);
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
            undoMenuItem.Enabled = (UDCount > 0);
            redoMenuItem.Enabled = (RDCount > 0);
            removePathToolStripMenuItem.Enabled = (LineList.SelectedIndex > -1);
            clonePathToolStripMenuItem.Enabled = (LineList.SelectedIndex > -1);
            loopPathToolStripMenuItem.Enabled = (canvasPoints.Length > 1);
            flipHorizontalToolStripMenuItem.Enabled = (canvasPoints.Length > 1 || LineList.Items.Count > 0);
            flipVerticalToolStripMenuItem.Enabled = (canvasPoints.Length > 1 || LineList.Items.Count > 0);
            clearAllToolStripMenuItem.Enabled = (canvasPoints.Length > 0 || LineList.Items.Count > 0);
        }

        private void editToolStripMenuItem_DropDownClosed(object sender, EventArgs e)
        {
            undoMenuItem.Enabled = true;
            redoMenuItem.Enabled = true;
            removePathToolStripMenuItem.Enabled = true;
            loopPathToolStripMenuItem.Enabled = true;
        }

        private void Flip_Click(object sender, EventArgs e)
        {
            setUndo();

            if (canvasPoints.Length == 0)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    PData currentPath = Lines[k];
                    int t = currentPath.LineType;
                    PointF[] pl = currentPath.Lines;

                    if ((sender as ToolStripMenuItem).Tag.ToString() == "H")
                    {
                        for (int j = 0; j < pl.Length; j++)
                        {
                            pl[j] = new PointF(1 - pl[j].X, pl[j].Y);
                        }

                    }
                    else
                    {
                        for (int j = 0; j < pl.Length; j++)
                        {
                            pl[j] = new PointF(pl[j].X, 1 - pl[j].Y);
                        }
                    }
                    if (currentPath.LineType == (int)PathType.Ellipse)
                    {
                        currentPath.RevSweep = !currentPath.RevSweep;
                    }
                }

            }
            else
            {
                PointF[] tmp = new PointF[canvasPoints.Length];
                Array.Copy(canvasPoints, tmp, canvasPoints.Length);
                PointF mid = PathAverage(tmp);
                if ((sender as ToolStripMenuItem).Tag.ToString() == "H")
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
                if (Elliptical.Checked)
                    Sweep.CheckState = (Sweep.CheckState == CheckState.Checked) ? CheckState.Indeterminate : CheckState.Checked;
                canvasPoints = tmp;

                if (LineList.SelectedIndex != -1)
                    UpdateExistingPath();
            }
            canvas.Refresh();
        }

        private void LineLoop_Click(object sender, EventArgs e)
        {
            if (canvasPoints.Length > 2 && !Elliptical.Checked)
            {
                setUndo();
                canvasPoints[canvasPoints.Length - 1] = canvasPoints[0];
                canvas.Refresh();
            }
        }

        private void HelpMenu_Click(object sender, EventArgs e)
        {
            string dest = Application.StartupPath + @"\Effects\";

            if (!(sender as ToolStripMenuItem).Name.Equals("QuickStartStripMenuItem"))
            {
                dest += "ShapeMaker User Guide.pdf";
            }
            else
            {
                dest += "ShapeMaker QuickStart.pdf";
            }
            try
            {
                if (File.Exists(dest))
                {
                    Process.Start(dest);
                }
                else
                {
                    MessageBox.Show("Help File Not Found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Cursor.Current = Cursors.Default;
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open the Help Page\r\n{ex.Message}\r\n{dest}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            float scale = scaleSlider.Value / 100f;
            toolTip1.SetToolTip(scaleSlider, $"{scale:0.00}x");

            if (scale > 1 && !InView())
                return;

            if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
            {
                AveragePoint = new PointF(.5f, .5f);
                int undoIndex = (UDPointer - 1 + UndoMax) % UndoMax;
                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] tmp = Lines[k].Lines;
                    PointF[] tmp2 = UDLines[undoIndex][k].Lines;
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i].X = (tmp2[i].X - AveragePoint.X) * scale + AveragePoint.X;
                        tmp[i].Y = (tmp2[i].Y - AveragePoint.Y) * scale + AveragePoint.Y;
                    }
                }
            }
            else if (canvasPoints.Length > 1)
            {
                AveragePoint = PathAverage(canvasPoints);
                int undoIndex = (UDPointer - 1 + UndoMax) % UndoMax;
                for (int idx = 0; idx < canvasPoints.Length; idx++)
                {
                    canvasPoints[idx].X = (UDPoints[undoIndex][idx].X - AveragePoint.X) * scale + AveragePoint.X;
                    canvasPoints[idx].Y = (UDPoints[undoIndex][idx].Y - AveragePoint.Y) * scale + AveragePoint.Y;
                }
            }
            canvas.Refresh();
        }

        private void scaleSlider_MouseDown(object sender, MouseEventArgs e)
        {
            WheelTimer.Stop();
            DrawAverage = true;
            setUndo();
            float scale = scaleSlider.Value / 100f;
            toolTip1.SetToolTip(scaleSlider, $"{scale:0.00}x");
        }

        private void scaleSlider_MouseUp(object sender, MouseEventArgs e)
        {
            DrawAverage = false;
            scaleSlider.Value = 100;
            float scale = scaleSlider.Value / 100f;
            toolTip1.SetToolTip(scaleSlider, $"{scale:0.00}x");
            canvas.Refresh();
        }
        #endregion

        #region Toolbar functions
        private void OptionToggle(object sender, EventArgs e)
        {
            (sender as ToolStripButton).Checked = !(sender as ToolStripButton).Checked;

            if (sender == Snap)
                Snap.Image = (Snap.Checked) ? Properties.Resources.SnapOn : Properties.Resources.SnapOff;
            else if (sender == LinkedPaths)
                LinkedPaths.Image = (LinkedPaths.Checked) ? Properties.Resources.LinkOn : Properties.Resources.LinkOff;
        }

        private void PathTypeToggle(object sender, EventArgs e)
        {
            if ((sender as ToolStripButton).Checked)
                return;

            activeType = (sender as ToolStripButtonWithKeys).PathType;
            PathTypeToggle();
        }

        private void PathTypeToggle()
        {
            foreach (ToolStripButtonWithKeys button in typeButtons)
            {
                button.Checked = (button.PathType == activeType);
                if (button.Checked)
                {
                    if (canvasPoints.Length > 1)
                    {
                        PointF hold = canvasPoints[0];
                        Array.Resize(ref canvasPoints, 1);
                        canvasPoints[0] = hold;
                        canvas.Refresh();
                    }
                }
            }

            Arc.Enabled = (Elliptical.Checked && !MacroCircle.Checked);
            Sweep.Enabled = (Elliptical.Checked && !MacroCircle.Checked);
        }

        private void MacroToggle(object sender, EventArgs e)
        {
            bool state;
            if (sender == MacroRect)
            {
                state = !MacroRect.Checked;
                StraightLine.PerformClick();
                MacroRect.Checked = state;
            }
            else if (sender == MacroCubic)
            {
                state = !MacroCubic.Checked;
                CubicBezier.PerformClick();
                MacroCubic.Checked = state;
            }
            else if (sender == MacroCircle)
            {
                state = !MacroCircle.Checked;
                Elliptical.PerformClick();
                MacroCircle.Checked = state;
            }

            Arc.Enabled = (Elliptical.Checked && !MacroCircle.Checked);
            Sweep.Enabled = (Elliptical.Checked && !MacroCircle.Checked);

            canvas.Refresh();
        }

        private void Loops_Click(object sender, EventArgs e)
        {
            setUndo();

            if (CloseContPaths.Equals(sender))
            {
                if (!CloseContPaths.Checked)
                {
                    ClosePath.Checked = false;
                    CloseContPaths.Checked = true;
                }
                else
                {
                    CloseContPaths.Checked = false;
                }
            }
            else
            {
                if (!ClosePath.Checked)
                {
                    ClosePath.Checked = true;
                    CloseContPaths.Checked = false;
                }
                else
                {
                    ClosePath.Checked = false;
                }
            }

            ClosePath.Image = (ClosePath.Checked) ? Properties.Resources.ClosePathOn : Properties.Resources.ClosePathOff;
            CloseContPaths.Image = (CloseContPaths.Checked) ? Properties.Resources.ClosePathsOn : Properties.Resources.ClosePathsOff;

            canvas.Refresh();

            if (LineList.SelectedIndex != -1)
                UpdateExistingPath();
        }

        private void Property_Click(object sender, EventArgs e)
        {
            setUndo();

            (sender as ToolStripButton).CheckState = (sender as ToolStripButton).CheckState == CheckState.Checked ? CheckState.Indeterminate : CheckState.Checked;

            if (sender == Arc)
                Arc.Image = (Arc.CheckState == CheckState.Checked) ? Properties.Resources.ArcSmall : Properties.Resources.ArcLarge;
            else if (sender == Sweep)
                Sweep.Image = (Sweep.CheckState == CheckState.Checked) ? Properties.Resources.SweepLeft : Properties.Resources.SweepRight;

            canvas.Refresh();

            if (LineList.SelectedIndex != -1)
                UpdateExistingPath();
        }
        #endregion

        #region Image Tracing
        private void setTraceImage()
        {
            if (traceLayer.Checked)
            {
                Rectangle selection = Selection.GetBoundsInt();
                canvas.BackgroundImage = EffectSourceSurface.CreateAliasedBitmap(selection);
            }
            else
            {
                Thread t = new Thread(new ThreadStart(GetImageFromClipboard));
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();

                if (clipboardImage == null)
                {
                    traceLayer.Focus();
                    MessageBox.Show("Couldn't load an image from the clipboard.", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                canvas.BackgroundImage = clipboardImage;
            }
        }

        private void GetImageFromClipboard()
        {
            clipboardImage?.Dispose();
            clipboardImage = null;
            try
            {
                IDataObject clippy = Clipboard.GetDataObject();
                if (clippy == null)
                    return;

                if (Clipboard.ContainsData("PNG"))
                {
                    Object pngObject = Clipboard.GetData("PNG");
                    if (pngObject is MemoryStream pngStream)
                        clipboardImage = (Bitmap)Image.FromStream(pngStream);
                }
                else if (clippy.GetDataPresent(DataFormats.Bitmap))
                {
                    clipboardImage = (Bitmap)clippy.GetData(typeof(Bitmap));
                }
            }
            catch
            {
            }
        }

        private void traceSource_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender as RadioButton).Checked)
                return;

            setTraceImage();
        }

        private void opacitySlider_Scroll(object sender, EventArgs e)
        {
            toolTip1.SetToolTip(opacitySlider, $"{opacitySlider.Value}%");
            canvas.Refresh();
        }

        private void FitBG_CheckedChanged(object sender, EventArgs e)
        {
            canvas.BackgroundImageLayout = (FitBG.Checked) ? ImageLayout.Zoom : ImageLayout.Center;
            canvas.Refresh();
        }
        #endregion

        #region Misc Form Controls' event functions
        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (canvasPoints.Length > 1 && LineList.SelectedIndex == -1)
                AddNewPath();
            MakePath();
            FinishTokenUpdate();
        }

        private void Deselect_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex == -1 && canvasPoints.Length > 1)
                setUndo();

            canvasPoints = new PointF[0];
            LineList.SelectedIndex = -1;
            canvas.Refresh();
        }

        private void ApplyBtn_Click(object sender, EventArgs e)
        {
            AddNewPath();
        }

        private void FigureName_Enter(object sender, EventArgs e)
        {
            if (FigureName.Text == "Untitled")
                FigureName.Text = string.Empty;
        }

        private void FigureName_Leave(object sender, EventArgs e)
        {
            if (FigureName.Text == string.Empty)
                FigureName.Text = "Untitled";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!Elliptical.Checked)
                MacroCircle.Checked = false;
            if (!StraightLine.Checked)
                MacroRect.Checked = false;
            if (!CubicBezier.Checked)
                MacroCubic.Checked = false;

            ToggleUpDownButtons();
            clonePathButton.Enabled = (LineList.SelectedIndex > -1);
            removePathButton.Enabled = (LineList.SelectedIndex > -1);
            MacroCircle.Enabled = (LineList.SelectedIndex == -1);
            MacroRect.Enabled = (LineList.SelectedIndex == -1);
            MacroCubic.Enabled = (LineList.SelectedIndex == -1);
            ClosePath.Enabled = !((MacroCircle.Checked && MacroCircle.Enabled) || (MacroRect.Checked && MacroRect.Enabled));
            CloseContPaths.Enabled = !((MacroCircle.Checked && MacroCircle.Enabled) || (MacroRect.Checked && MacroRect.Enabled));
            DeselectBtn.Enabled = (LineList.SelectedIndex != -1 && canvasPoints.Length != 0);
            AddBtn.Enabled = (LineList.SelectedIndex == -1 && canvasPoints.Length > 1);
            DiscardBtn.Enabled = (LineList.SelectedIndex == -1 && canvasPoints.Length > 1);
            scaleSlider.Enabled = (canvasPoints.Length > 1 || (canvasPoints.Length == 0 && LineList.Items.Count > 0));
            RotationKnob.Enabled = (canvasPoints.Length > 1 || (canvasPoints.Length == 0 && LineList.Items.Count > 0));

            if (Control.ModifierKeys == Keys.Control)
            {
                KeyTrak = true;
            }
            else if (KeyTrak)
            {
                KeyTrak = false;
                canvas.Refresh();
            }
            else
            {
                KeyTrak = false;
            }

            if (countflag || canvasPoints.Length > 0 || LineList.Items.Count > 0)
            {
                statusLabelNubsUsed.Text = $"{canvasPoints.Length}/{maxPoints} Nubs used";
                statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";
            }

            if (LineList.SelectedIndex == -1)
                isNewPath = true;
        }

        private void generic_MouseWheel(object sender, MouseEventArgs e)
        {
            WheelTimer.Stop();

            if (!WheelScaleOrRotate)
            {
                WheelScaleOrRotate = true;
                setUndo();
            }

            if (!DrawAverage)
                DrawAverage = true;

            WheelTimer.Start();
        }

        private void EndWheeling(object sender, EventArgs e)
        {
            WheelTimer.Stop();
            WheelScaleOrRotate = false;

            if (scaleSlider.Value != 100)
                scaleSlider.Value = 100;

            if (DrawAverage)
            {
                DrawAverage = false;
                canvas.Refresh();
            }
        }
        #endregion

        #region Recent Items functions
        private void AddToRecents(string filePath)
        {
            RegistryKey settings = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            if (settings == null)
            {
                Registry.CurrentUser.CreateSubKey(@"Software\PdnDwarves\ShapeMaker").Flush();
                settings = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            }
            string recents = (string)settings.GetValue("RecentProjects", string.Empty);

            if (recents == string.Empty)
            {
                recents = filePath;
            }
            else
            {
                recents = filePath + "|" + recents;

                var paths = recents.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> recentsList = new List<string>();
                foreach (string itemPath in paths)
                {
                    bool contains = false;
                    foreach (string listItem in recentsList)
                    {
                        if (listItem.ToLowerInvariant() == itemPath.ToLowerInvariant())
                        {
                            contains = true;
                            break;
                        }
                    }

                    if (!contains)
                    {
                        recentsList.Add(itemPath);
                    }
                }

                int length = Math.Min(8, recentsList.Count);
                recents = string.Join("|", recentsList.ToArray(), 0, length);
            }

            settings.SetValue("RecentProjects", recents);
            settings.Close();
        }

        private void openRecentProject_DropDownOpening(object sender, EventArgs e)
        {
            this.openRecentProject.DropDownItems.Clear();

            RegistryKey settings = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            if (settings == null)
            {
                Registry.CurrentUser.CreateSubKey(@"Software\PdnDwarves\ShapeMaker").Flush();
                settings = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true);
            }
            string recents = (string)settings.GetValue("RecentProjects", string.Empty);
            settings.Close();

            List<ToolStripItem> recentsList = new List<ToolStripItem>();
            string[] paths = recents.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 1;
            foreach (string projectPath in paths)
            {
                if (!File.Exists(projectPath))
                    continue;

                ToolStripMenuItem recentItem = new ToolStripMenuItem();

                string menuText = $"&{count} {Path.GetFileName(projectPath)}";
                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                try
                {
                    ArrayList projectPaths = (ArrayList)ser.Deserialize(File.OpenRead(projectPath));

                    menuText = $"&{count} {(projectPaths[projectPaths.Count - 1] as PData).Meta} ({Path.GetFileName(projectPath)})";
                }
                catch
                {
                }

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

                ToolStripMenuItem clearRecents = new ToolStripMenuItem();
                clearRecents.Text = "&Clear List";
                clearRecents.Click += ClearRecents_Click;
                recentsList.Add(clearRecents);

                this.openRecentProject.DropDownItems.AddRange(recentsList.ToArray());
            }
            else
            {
                ToolStripMenuItem noRecents = new ToolStripMenuItem();
                noRecents.Text = "No Recent Projects";
                noRecents.Enabled = false;

                this.openRecentProject.DropDownItems.Add(noRecents);
            }
        }

        private void ClearRecents_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear the Open Recent Project list?", "ShapeMaker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            using (RegistryKey settings = Registry.CurrentUser.OpenSubKey(@"Software\PdnDwarves\ShapeMaker", true))
            {
                if (settings != null)
                    settings.SetValue("RecentProjects", string.Empty);
            }
        }

        private void RecentItem_Click(object sender, EventArgs e)
        {
            string projectPath = (sender as ToolStripMenuItem)?.ToolTipText;
            if (!File.Exists(projectPath))
            {
                MessageBox.Show("File not found.\n" + projectPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
            try
            {
                using (FileStream stream = File.OpenRead(projectPath))
                {
                    ArrayList projectPaths = (ArrayList)ser.Deserialize(stream);

                    if (projectPaths.Count == 0)
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

                    PData documentProps = projectPaths[projectPaths.Count - 1] as PData;
                    FigureName.Text = documentProps.Meta;
                    SolidFillMenuItem.Checked = documentProps.SolidFill;
                    foreach (PData path in projectPaths)
                    {
                        Lines.Add(path);
                        LineList.Items.Add(LineNames[path.LineType]);
                    }

                    ZoomToFactor(1);
                    resetRotation();
                    resetHistory();
                    canvas.Refresh();
                    AddToRecents(projectPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Incorrect Format\r\n" + ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
    }

    public enum PathType
    {
        Straight,
        Ellipse,
        Cubic,
        SmoothCubic,
        Quadratic,
        SmoothQuadratic,
        None = -1
    }
}