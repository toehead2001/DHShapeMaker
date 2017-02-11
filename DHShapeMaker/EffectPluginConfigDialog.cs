//Elliptical Arc algorithm from svg.codeplex.com
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using PaintDotNet.Effects;
using PaintDotNet;
using System.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections;
using System.Reflection;
using Controlz;
using System.IO;
using Microsoft.Win32;
using System.Xml.Serialization;

namespace ShapeMaker
{
    public partial class EffectPluginConfigDialog : EffectConfigDialog
    {
        enum LineTypes
        {
            Straight,
            Ellipse,
            Cubic,
            SmoothCubic,
            Quadratic,
            SmoothQuadratic
        }

        string[] LineNames =
        {
            "Straight Lines",
            "Ellipse",
            "Cubic Beziers",
            "Smooth Cubic Beziers",
            "Quadratic Beziers",
            "Smooth Quadratic Beziers"
        };

        Color[] LineColors =
        {
            Color.Black,
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.DarkGoldenrod,
            Color.Purple
        };

        Color AnchorColor = Color.FromArgb(0, 128, 128);

        private const int buttonWd = 40;
        private const int buttonHt = 45;
        private const int blankWd = 15;
        private const int blankHt = 45;
        private Color BackColorConst = Color.FromArgb(189, 189, 189);

        private const double RadPerDeg = Math.PI / 180.0;
        private const double twoPI = Math.PI * 2;

        bool countflag = false;
        const int maxPaths = 100;
        const int maxpoint = 255;
        const int minpoint = 0;
        const int basepoint = 0;
        int clickedNub = -1;
        PointF MoveStart;
        PointF[] canvasPoints = new PointF[basepoint];
        private const int UndoMax = 15;
        int keylock = 0;

        ArrayList[] UDLines = new ArrayList[UndoMax];
        PointF[][] UDPoint = new PointF[UndoMax][];
        int[] UDtype = new int[UndoMax];
        int[] UDSelect = new int[UndoMax];
        int UDCount = 0;
        int UDPointer = 0;
        int MDown = -1;

        float lastRot = 180;
        bool KeyTrak = false;
        GraphicsPath[] PGP = new GraphicsPath[0];
        ArrayList Lines = new ArrayList();
        Size SuperSize = new Size();
        bool PanFlag = false;
        bool CanScrollZoom = false;
        Point Zoomed = new Point(0, 0);
        float DPI = 1;
        bool DrawPosBars = false;
        Control hadFocus;
        bool isNewPath = true;
        int canvasBaseSize;

        public EffectPluginConfigDialog()
        {
            InitializeComponent();
        }

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
            foreach (PData p in token.PathData)
            {
                Lines.Add(new PData(p.Lines, p.ClosedType, p.LineType, p.IsLarge, p.RevSweep, p.Alias, p.LoopBack));

            }

            LineList.Items.Clear();
            foreach (PData p in token.PathData)
            {
                LineList.Items.Add(LineNames[p.LineType]);
            }

        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            MakePath();
            FinishTokenUpdate();
        }

        private void EffectPluginConfigDialog_Load(object sender, EventArgs e)
        {
            DPI = this.AutoScaleDimensions.Width / 96f;
            canvasBaseSize = this.canvas.Width;

            canvas.BackgroundImage = EffectSourceSurface.CreateAliasedBitmap();
            SuperSize = EffectSourceSurface.CreateAliasedBitmap().Size;

            if (SuperSize.Width >= SuperSize.Height)
            {
                SuperSize.Width = SuperSize.Height;
            }
            else if (SuperSize.Width < SuperSize.Height)
            {
                SuperSize.Height = SuperSize.Width;
            }

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = EffectPlugin.StaticName + " ver. " + version.Major + "." + version.Minor
                + "." + version.Build + "." + version.Revision;
            loadRadio();
            foreach (ToolStripMenuItem item in menuStrip1.Items)
            {
                if (item.DisplayStyle == ToolStripItemDisplayStyle.None)
                {
                    if (item.Text.Equals("Blank"))
                    {
                        item.Size = new Size((int)(blankWd * DPI), (int)(blankHt * DPI));
                    }
                    else
                    {
                        item.Size = new Size((int)(buttonWd * DPI), (int)(buttonHt * DPI));
                    }
                }
            }
            toolTip1.ReshowDelay = 0;
            toolTip1.AutomaticDelay = 0;
            toolTip1.AutoPopDelay = 0;
            toolTip1.InitialDelay = 0;
            toolTip1.UseFading = false;
            toolTip1.UseAnimation = false;

            timer1.Enabled = true;
            StraightLine.Image = SpriteSheet(0, 1);
            //======

            LineList.ItemHeight = (int)(LineList.ItemHeight * DPI);
            statusLabelNubsUsed.Size = new Size((int)(statusLabelNubsUsed.Size.Width * DPI), (int)(statusLabelNubsUsed.Size.Height * DPI));
            statusLabelPathsUsed.Size = new Size((int)(statusLabelPathsUsed.Size.Width * DPI), (int)(statusLabelPathsUsed.Size.Height * DPI));
            statusLabelLocation.Size = new Size((int)(statusLabelLocation.Size.Width * DPI), (int)(statusLabelLocation.Size.Height * DPI));

            statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";
        }

        private void PosBarsTimer_Tick(object sender, EventArgs e)
        {
            DrawPosBars = false;
            verPosBar.Refresh();
            horPosBar.Refresh();
            posBarsTimer.Stop();
        }

        private void setUndo()
        {
            setUndo(false);
        }

        private void setUndo(bool addingNewPath)
        {
            // set undo
            undoToolStripMenuItem.Enabled = true;

            UDCount++;
            UDCount = (UDCount > UndoMax) ? UndoMax : UDCount;
            UDPointer++;
            UDPointer %= UndoMax;
            UDtype[UDPointer] = getPathType();
            UDSelect[UDPointer] = (addingNewPath) ? -1 : LineList.SelectedIndex;
            UDPoint[UDPointer] = new PointF[canvasPoints.Length];
            Array.Copy(canvasPoints, UDPoint[UDPointer], canvasPoints.Length);
            UDLines[UDPointer] = new ArrayList();
            foreach (PData pd in Lines)
            {
                PointF[] tmp = new PointF[pd.Lines.Length];
                Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                UDLines[UDPointer].Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
            }
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            PictureBox s = (PictureBox)sender;
            RectangleF hit = new RectangleF(e.X - 4, e.Y - 4, 9, 9);
            RectangleF bhit = new RectangleF(e.X - 10, e.Y - 10, 20, 20);


            //identify node selected
            clickedNub = -1;
            MoveStart = new PointF(
                                (float)e.X / s.ClientSize.Width,
                                (float)e.Y / s.ClientSize.Height);

            for (int i = 0; i < canvasPoints.Length; i++)
            {
                PointF p = new PointF(canvasPoints[i].X * s.ClientSize.Width,
                    canvasPoints[i].Y * s.ClientSize.Height);
                if (hit.Contains(p))
                {
                    clickedNub = i;
                    break;
                }
            }
            try
            {
                int lt = getPathType();


                if (Control.ModifierKeys == Keys.Alt)
                {
                    if (clickedNub == -1)
                    {
                        PanFlag = true;

                        if (canvas.Width > viewport.ClientRectangle.Width && canvas.Height > viewport.ClientRectangle.Height)
                            canvas.Cursor = Cursors.NoMove2D;
                        else if (canvas.Width > viewport.ClientRectangle.Width)
                            canvas.Cursor = Cursors.NoMoveHoriz;
                        else if (canvas.Height > viewport.ClientRectangle.Height)
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
                    setUndo();

                    if (clickedNub > -1)//delete
                    {
                        #region delete
                        if (clickedNub == 0) return;//don't delete moveto 

                        switch (lt)
                        {
                            case (int)LineTypes.Straight:
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);

                                break;
                            case (int)LineTypes.Ellipse:
                                if (clickedNub != 4) return;
                                Array.Resize(ref canvasPoints, 1);
                                break;
                            case (int)LineTypes.Cubic:
                                if (getNubType(clickedNub) != 3) return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                if (MacroCubic.Checked) CubicAdjust();
                                break;
                            case (int)LineTypes.Quadratic:
                                if (getNubType(clickedNub) != 3) return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);

                                break;
                            case (int)LineTypes.SmoothCubic:
                                if (getNubType(clickedNub) != 3) return;
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
                            case (int)LineTypes.SmoothQuadratic:
                                if (getNubType(clickedNub) != 3) return;
                                canvasPoints = RemoveAt(canvasPoints, clickedNub);
                                //remove control points
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 1);
                                canvasPoints = RemoveAt(canvasPoints, clickedNub - 2);
                                for (int i = 1; i < canvasPoints.Length; i++)
                                {
                                    if (getNubType(i) == 1 && i > 3)
                                    {
                                        canvasPoints[i] = reverseAverage(canvasPoints[i - 3], canvasPoints[i - 1]);
                                        if (i < canvasPoints.Length - 1) canvasPoints[i + 1] = canvasPoints[i];
                                    }
                                }

                                break;
                        }
                        canvas.Refresh();
                        #endregion //delete
                    }
                    else if (clickedNub == -1)//add new
                    {
                        int eX = e.X, eY = e.Y;
                        if (Snap.Checked)
                        {
                            eX = (int)(Math.Floor((double)(5 + e.X) / 10) * 10);
                            eY = (int)(Math.Floor((double)(5 + e.Y) / 10) * 10);
                        }
                        StatusBarNubLocation(eX, eY);

                        #region add
                        int len = canvasPoints.Length;
                        if (len >= maxpoint) return;
                        if (lt == (int)LineTypes.Ellipse && canvasPoints.Length > 2) return;
                        if (len == 0)//first point
                        {
                            Array.Resize(ref canvasPoints, len + 1);
                            canvasPoints[0] = new PointF(
                                (float)eX / s.ClientSize.Width,
                                (float)eY / s.ClientSize.Height);
                            countflag = true;
                        }
                        else//not first point
                        {

                            switch (lt)
                            {
                                case (int)LineTypes.Straight:
                                    Array.Resize(ref canvasPoints, len + 1);
                                    canvasPoints[len] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);

                                    break;
                                case (int)LineTypes.Ellipse:
                                    Array.Resize(ref canvasPoints, 5);
                                    canvasPoints[4] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);
                                    PointF mid = pointAverage(canvasPoints[0], canvasPoints[4]);
                                    PointF mid2 = ThirdPoint(canvasPoints[0], mid, true, 1f);
                                    canvasPoints[1] = pointAverage(canvasPoints[0], mid2);
                                    canvasPoints[2] = pointAverage(canvasPoints[4], mid2);
                                    canvasPoints[3] = ThirdPoint(canvasPoints[0], mid, false, 1f);
                                    break;

                                case (int)LineTypes.Cubic:
                                    Array.Resize(ref canvasPoints, len + 3);

                                    canvasPoints[len + 2] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);
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
                                case (int)LineTypes.Quadratic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);
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

                                case (int)LineTypes.SmoothCubic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);
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
                                case (int)LineTypes.SmoothQuadratic:
                                    Array.Resize(ref canvasPoints, len + 3);
                                    canvasPoints[len + 2] = new PointF(
                                        (float)eX / s.ClientSize.Width,
                                        (float)eY / s.ClientSize.Height);
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

                    // If modifying an existing Path, save the change
                    if (LineList.SelectedIndex != -1 && clickedNub != 0)
                    {
                        Lines[LineList.SelectedIndex] = new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked,
                            Sweep.Checked, (Lines[LineList.SelectedIndex] as PData).Alias, MPMode.Checked);
                        LineList.Items[LineList.SelectedIndex] = LineNames[getPathType()];
                    }
                }
                else if (Control.ModifierKeys == Keys.Shift && e.Button == MouseButtons.Left)
                {
                    if (canvasPoints.Length != 0)
                    {
                        if (clickedNub != -1)
                        {
                            setUndo();
                            canvas.Cursor = Cursors.SizeAll;
                        }
                    }
                    else
                    {
                        setUndo();
                        canvas.Cursor = Cursors.SizeAll;
                    }
                }
                else if (e.Button == MouseButtons.Left)
                {
                    if (clickedNub == -1)
                    {
                        int clickedPath = getNearestPath(bhit);
                        if (clickedPath != -1)
                            LineList.SelectedIndex = clickedPath;
                    }
                    else
                    {
                        setUndo();
                    }
                }
                else if (e.Button == MouseButtons.Middle)
                {
                    PanFlag = true;

                    if (canvas.Width > viewport.ClientRectangle.Width && canvas.Height > viewport.ClientRectangle.Height)
                        canvas.Cursor = Cursors.NoMove2D;
                    else if (canvas.Width > viewport.ClientRectangle.Width)
                        canvas.Cursor = Cursors.NoMoveHoriz;
                    else if (canvas.Height > viewport.ClientRectangle.Height)
                        canvas.Cursor = Cursors.NoMoveVert;
                    else
                        PanFlag = false;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

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

                /*
                 * P2 = 2P1 – P0
                 */

                mid4.X = 2 * mid3.X - knots[0].X;
                mid4.Y = 2 * mid3.Y - knots[0].Y;
                canvasPoints[1] = mid3;
                canvasPoints[2] = mid4;
            }

            if (n > 1)
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

        private PointF[] GetFirstControlPoints(PointF[] rhs)
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

        private int getNearestPath(RectangleF hit)
        {
            int result = -1;
            if (LineList.Items.Count == 0) return -1;
            for (int i = 0; i < LineList.Items.Count; i++)
            {
                PointF[] tmp = (Lines[i] as PData).Lines;

                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddLines(tmp);


                    gp.Flatten(null, .1f);
                    tmp = gp.PathPoints;
                    for (int j = 0; j < tmp.Length; j++)
                    {
                        // exclude 'control' nubs.
                        switch ((Lines[i] as PData).LineType)
                        {
                            case 1: // Ellipse (Red)
                                if (j % 4 != 0) continue;
                                break;
                            case 2: // Cubic (Blue)
                            case 3: // Smooth Cubic (Green)
                            case 4: // Quadratic (Goldenrod)
                                if (j % 3 != 0) continue;
                                break;
                        }

                        PointF p = new PointF(tmp[j].X * canvas.ClientSize.Width,
                        tmp[j].Y * canvas.ClientSize.Height);

                        if (hit.Contains(p))
                        {
                            result = i;
                            break;
                        }
                    }
                    if (result > -1) break;
                }
            }
            return result;
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            PanFlag = false;
            clickedNub = -1;
            canvas.Refresh();
            canvas.Cursor = Cursors.Default;
            posBarsTimer.Start();
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PictureBox s = (PictureBox)sender;

            int i = clickedNub;
            int ptype = getNubType(clickedNub);

            int eX = e.X, eY = e.Y;
            if (Snap.Checked)
            {
                eX = (int)(Math.Floor((double)(5 + eX) / 10) * 10);
                eY = (int)(Math.Floor((double)(5 + eY) / 10) * 10);
            }


            PointF mapPoint = new PointF(
                                (float)eX / s.ClientSize.Width,
                                (float)eY / s.ClientSize.Height);
            int lt = getPathType();

            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (s.ClientRectangle.Contains(e.Location))
                    {
                        //left shift move line or path
                        if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                        {
                            if (canvasPoints.Length != 0 && i > -1 && i < canvasPoints.Length)
                            {
                                StatusBarNubLocation(eX, eY);

                                PointF oldp = canvasPoints[i];

                                switch (lt)
                                {
                                    case (int)LineTypes.Straight:
                                    case (int)LineTypes.Cubic:
                                    case (int)LineTypes.Quadratic:
                                    case (int)LineTypes.SmoothCubic:
                                    case (int)LineTypes.SmoothQuadratic:
                                    case (int)LineTypes.Ellipse:
                                        for (int j = 0; j < canvasPoints.Length; j++)
                                        {
                                            canvasPoints[j] = movePoint(oldp, mapPoint, canvasPoints[j]);
                                        }

                                        break;
                                }

                            }
                            else
                            {

                                if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
                                {
                                    StatusBarNubLocation(eX, eY);

                                    for (int k = 0; k < Lines.Count; k++)
                                    {
                                        int t = (Lines[k] as PData).LineType;
                                        PointF[] pl = (Lines[k] as PData).Lines;
                                        switch (t)
                                        {
                                            case (int)LineTypes.Straight:
                                            case (int)LineTypes.Cubic:
                                            case (int)LineTypes.Quadratic:
                                            case (int)LineTypes.SmoothCubic:
                                            case (int)LineTypes.SmoothQuadratic:
                                            case (int)LineTypes.Ellipse:

                                                for (int j = 0; j < pl.Length; j++)
                                                {
                                                    pl[j] = movePoint(MoveStart, mapPoint, pl[j]);
                                                }
                                                break;
                                        }
                                    }
                                    MoveStart = mapPoint;
                                }
                            }
                        }//no shift movepoint
                        else if (canvasPoints.Length != 0 && i > 0 && i < canvasPoints.Length)
                        {
                            StatusBarNubLocation(eX, eY);

                            PointF oldp = canvasPoints[i];
                            switch (lt)
                            {
                                case (int)LineTypes.Straight:
                                    canvasPoints[i] = mapPoint;

                                    break;
                                case (int)LineTypes.Ellipse:
                                    canvasPoints[i] = mapPoint;
                                    break;
                                case (int)LineTypes.Cubic:
                                    #region cubic
                                    oldp = canvasPoints[i];
                                    if (ptype == 0)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        if (canvasPoints.Length > 1) canvasPoints[i + 1] =
                                            movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                    }
                                    else if (ptype == 1 || ptype == 2)
                                    {
                                        canvasPoints[i] = mapPoint;
                                    }
                                    else if (ptype == 3)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        canvasPoints[i - 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i - 1]);
                                        if ((i + 1) < canvasPoints.Length) canvasPoints[i + 1] =
                                           movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                    }
                                    if (MacroCubic.Checked) CubicAdjust();
                                    #endregion
                                    break;
                                case (int)LineTypes.Quadratic:
                                    #region Quadratic
                                    oldp = canvasPoints[i];
                                    if (ptype == 0)
                                    {
                                        canvasPoints[i] = mapPoint;
                                    }
                                    else if (ptype == 1)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        if ((i + 1) < canvasPoints.Length) canvasPoints[i + 1] = canvasPoints[i];
                                    }
                                    if (ptype == 2)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        if ((i - 1) > 0) canvasPoints[i - 1] = canvasPoints[i];
                                    }
                                    else if (ptype == 3)
                                    {
                                        if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                        {
                                            //online
                                            if (i == canvasPoints.Length - 1)
                                            {
                                                PointF rtmp = reverseAverage(canvasPoints[i - 1], canvasPoints[i]);
                                                canvasPoints[i] = onLinePoint(canvasPoints[i - 1], rtmp,
                                                    mapPoint);
                                            }
                                            else
                                            {
                                                canvasPoints[i] = onLinePoint(canvasPoints[i - 1], canvasPoints[i + 1],
                                                    mapPoint);
                                            }
                                        }
                                        else
                                        {
                                            canvasPoints[i] = mapPoint;
                                        }
                                    }
                                    #endregion
                                    break;
                                case (int)LineTypes.SmoothCubic:
                                    #region smooth Cubic
                                    oldp = canvasPoints[i];

                                    if (ptype == 0)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        if (canvasPoints.Length > 1) canvasPoints[i + 1] =
                                            movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                        canvasPoints[1] = canvasPoints[0];
                                    }
                                    else if (ptype == 1)
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
                                    else if (ptype == 2)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        if (i < canvasPoints.Length - 2)
                                        {
                                            canvasPoints[i + 2] = reverseAverage(canvasPoints[i], canvasPoints[i + 1]);
                                        }
                                    }
                                    else if (ptype == 3)
                                    {
                                        canvasPoints[i] = mapPoint;
                                        canvasPoints[i - 1] = movePoint(oldp, canvasPoints[i], canvasPoints[i - 1]);
                                        if ((i + 1) < canvasPoints.Length) canvasPoints[i + 1] =
                                           movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                    }
                                    #endregion

                                    break;
                                case (int)LineTypes.SmoothQuadratic:
                                    #region Smooth Quadratic
                                    oldp = canvasPoints[i];
                                    if (ptype == 0)
                                    {
                                        canvasPoints[i] = mapPoint;

                                    }
                                    if (ptype == 3)
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
                        }//move first point
                        else if (canvasPoints.Length != 0 && i == 0)
                        {
                            StatusBarNubLocation(eX, eY);

                            PointF oldp = canvasPoints[i];

                            if (ptype == 0)//special quadratic
                            {
                                switch (lt)
                                {
                                    case (int)LineTypes.Straight:
                                        canvasPoints[i] = mapPoint;
                                        break;
                                    case (int)LineTypes.Ellipse:
                                        canvasPoints[i] = mapPoint;
                                        break;
                                    case (int)LineTypes.Cubic:
                                    case (int)LineTypes.SmoothCubic:
                                        canvasPoints[i] = mapPoint;
                                        //recent change 8/22/2015
                                        if (canvasPoints.Length > 1) canvasPoints[i + 1] =
                                            movePoint(oldp, canvasPoints[i], canvasPoints[i + 1]);
                                        break;
                                    case (int)LineTypes.Quadratic:
                                        if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                                        {
                                            if (canvasPoints.Length == 1)
                                            {
                                                canvasPoints[i] = mapPoint;
                                            }
                                            else
                                            {
                                                PointF rtmp = reverseAverage(canvasPoints[i + 1], canvasPoints[i]);
                                                canvasPoints[i] = onLinePoint(canvasPoints[i + 1], rtmp,
                                                    mapPoint);
                                            }
                                        }
                                        else
                                        {
                                            canvasPoints[i] = mapPoint;
                                        }
                                        break;
                                    case (int)LineTypes.SmoothQuadratic:

                                        canvasPoints[0] = mapPoint;
                                        canvasPoints[1] = mapPoint;

                                        for (int j = 0; j < canvasPoints.Length; j++)
                                        {
                                            if (getNubType(j) == 1 && j > 1)
                                            {
                                                canvasPoints[j] = reverseAverage(canvasPoints[j - 3], canvasPoints[j - 1]);
                                                canvasPoints[j + 1] = canvasPoints[j];

                                            }
                                        }
                                        break;
                                }
                            }

                        }//Pan zoomed
                        else if (PanFlag)
                        {
                            int mpx = (int)(mapPoint.X * 100);
                            int msx = (int)(MoveStart.X * 100);
                            int mpy = (int)(mapPoint.Y * 100);
                            int msy = (int)(MoveStart.Y * 100);
                            int tx = 10 * (mpx - msx);
                            int ty = 10 * (mpy - msy);

                            int maxMoveX = canvas.Width - viewport.ClientSize.Width;
                            int maxMoveY = canvas.Height - viewport.ClientSize.Height;

                            if (canvas.Width > viewport.ClientSize.Width)
                                Zoomed.X = (canvas.Location.X + tx < -maxMoveX) ? -maxMoveX : (canvas.Location.X + tx > 0) ? 0 : canvas.Location.X + tx;
                            if (canvas.Height > viewport.ClientSize.Height)
                                Zoomed.Y = (canvas.Location.Y + ty < -maxMoveY) ? -maxMoveY : (canvas.Location.Y + ty > 0) ? 0 : canvas.Location.Y + ty;

                            canvas.Location = Zoomed;

                            posBarsTimer.Stop();
                            DrawPosBars = true;
                            verPosBar.Refresh();
                            horPosBar.Refresh();
                        }

                    }

                    canvas.Refresh();
                }
                else if (e.Button == MouseButtons.Middle && PanFlag)
                {
                    int mpx = (int)(mapPoint.X * 100);
                    int msx = (int)(MoveStart.X * 100);
                    int mpy = (int)(mapPoint.Y * 100);
                    int msy = (int)(MoveStart.Y * 100);
                    int tx = 10 * (mpx - msx);
                    int ty = 10 * (mpy - msy);

                    int maxMoveX = canvas.Width - viewport.ClientSize.Width;
                    int maxMoveY = canvas.Height - viewport.ClientSize.Height;

                    if (canvas.Width > viewport.ClientSize.Width)
                        Zoomed.X = (canvas.Location.X + tx < -maxMoveX) ? -maxMoveX : (canvas.Location.X + tx > 0) ? 0 : canvas.Location.X + tx;
                    if (canvas.Height > viewport.ClientSize.Height)
                        Zoomed.Y = (canvas.Location.Y + ty < -maxMoveY) ? -maxMoveY : (canvas.Location.Y + ty > 0) ? 0 : canvas.Location.Y + ty;

                    canvas.Location = Zoomed;

                    posBarsTimer.Stop();
                    DrawPosBars = true;
                    verPosBar.Refresh();
                    horPosBar.Refresh();
                }

            }
            catch { }// (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void StatusBarNubLocation(int x, int y)
        {
            int zoomFactor = canvas.Width / canvasBaseSize;
            statusLabelLocation.Text = $"{Math.Round(x / (float)zoomFactor / DPI)}, {Math.Round(y / (float)zoomFactor / DPI)}";
            statusStrip1.Refresh();
        }

        private PointF onLinePoint(PointF sp, PointF ep, PointF mt)
        {
            PointF xy = new PointF(sp.X, sp.Y);
            float dist = 9999;

            for (float i = 0; i < 1; i += .001f)
            {
                PointF test = new PointF(ep.X * i + sp.X - sp.X * i,
                    ep.Y * i + sp.Y - sp.Y * i);

                float tmp = pythag(mt, test);
                if (tmp < dist)
                {
                    dist = tmp;
                    xy = new PointF(test.X, test.Y);
                }
            }


            return xy;
        }

        private PointF movePoint(PointF orig, PointF dest, PointF target)
        {
            return new PointF
            {
                X = target.X + (dest.X - orig.X),
                Y = target.Y + (dest.Y - orig.Y)
            };
        }

        private int getNubType(int nubIndex)
        {
            if (nubIndex == 0)
                return 0;//base

            return ((nubIndex - 1) % 3) + 1;//1 =ctl1,2=ctl2, 3= end point;
        }

        private void loadRadio()
        {
            radios[0] = StraightLine;
            radios[1] = Ellipse;
            radios[2] = Cubic;
            radios[3] = SCubic;
            radios[4] = Quadratic;
            radios[5] = SQuadratic;
        }

        private int getPathType()
        {
            for (int i = 0; i < radios.Length; i++)
            {
                if (radios[i].Checked)
                    return i;
            }
            return 0;
        }

        private void setUiForPath(int pathType, bool closedPath, bool largeArc, bool revSweep, bool multiClosedPath)
        {
            SuspendLayout();
            MacroCubic.Checked = false;
            MacroCircle.Checked = false;
            MacroRect.Checked = false;
            MDown = -1;
            foreach (ToolStripMenuItem t in radios) t.Checked = (t.Name.Equals(radios[pathType].Name));
            Loop.Checked = closedPath;
            MPMode.Checked = multiClosedPath;
            Big.Checked = largeArc;
            Sweep.Checked = revSweep;
            ResumeLayout();
        }

        private PointF[] RemoveAt(PointF[] source, int index)
        {
            PointF[] dest = new PointF[source.Length - 1];
            if (index > 0)
                Array.Copy(source, 0, dest, 0, index);

            if (index < source.Length - 1)
                Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

            return dest;
        }

        private int[] RemoveAtInt(int[] source, int index)
        {
            int[] dest = new int[source.Length - 1];
            if (index > 0)
                Array.Copy(source, 0, dest, 0, index);

            if (index < source.Length - 1)
                Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

            return dest;
        }

        private bool[] RemoveAtBool(bool[] source, int index)
        {
            bool[] dest = new bool[source.Length - 1];
            if (index > 0)
                Array.Copy(source, 0, dest, 0, index);

            if (index < source.Length - 1)
                Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

            return dest;
        }

        private void canvas_Paint(object sender, PaintEventArgs e)
        {
            Image gridImg = Properties.Resources.bg;
            ImageAttributes attr = new ImageAttributes();
            ColorMatrix mx = new ColorMatrix();
            mx.Matrix33 = (1001f - opaque.Value) / 1000f;
            attr.SetColorMatrix(mx);
            using (TextureBrush texture =
                new TextureBrush(gridImg, new Rectangle(Point.Empty, gridImg.Size), attr))
            {
                texture.WrapMode = WrapMode.Tile;
                e.Graphics.FillRectangle(texture, e.ClipRectangle);
            }
            attr.Dispose();

            PointF loopBack = new PointF(-9999, -9999);
            PointF Oldxy = new PointF(-9999, -9999);

            int pType = 0;
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
                    if (j == Lines.Count && LineList.SelectedIndex == -1) j = -1;
                    if (j >= Lines.Count) continue;

                    if (j == LineList.SelectedIndex)
                    {
                        pPoints = canvasPoints;
                        pType = getPathType();
                        isClosed = Loop.Checked;
                        mpMode = MPMode.Checked;
                        isLarge = Big.Checked;
                        revSweep = Sweep.Checked;
                    }
                    else
                    {
                        PData itemPath = (Lines[j] as PData);
                        pPoints = itemPath.Lines;
                        pType = itemPath.LineType;
                        isClosed = itemPath.ClosedType;
                        mpMode = itemPath.LoopBack;
                        isLarge = itemPath.IsLarge;
                        revSweep = itemPath.RevSweep;
                    }

                    if (pPoints.Length == 0) continue;

                    PointF[] pts = new PointF[pPoints.Length];
                    for (int i = 0; i < pPoints.Length; i++)
                    {
                        pts[i].X = canvas.ClientSize.Width * pPoints[i].X;
                        pts[i].Y = canvas.ClientSize.Height * pPoints[i].Y;
                    }

                    PointF[] Qpts = new PointF[pPoints.Length];
                    #region cube to quad
                    if (pType == (int)LineTypes.Quadratic || pType == (int)LineTypes.SmoothQuadratic)
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
                    if (!Oldxy.Equals(pts[0]) || (j == LineList.SelectedIndex && Loop.Checked))
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
                                    case (int)LineTypes.Straight:
                                        e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                        break;
                                    case (int)LineTypes.Ellipse:
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
                                    case (int)LineTypes.Quadratic:
                                        if (getNubType(i) == 1)
                                        {
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i - 1], pts[i]);
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i + 2].X - 4, pts[i + 2].Y - 4, 6, 6);
                                            e.Graphics.DrawLine(Pens.Black, pts[i], pts[i + 2]);
                                        }
                                        break;
                                    case (int)LineTypes.SmoothQuadratic:
                                        if (getNubType(i) == 3)
                                        {
                                            e.Graphics.DrawEllipse(Pens.Black, pts[i].X - 4, pts[i].Y - 4, 6, 6);
                                        }
                                        break;
                                    case (int)LineTypes.Cubic:
                                    case (int)LineTypes.SmoothCubic:
                                        if (getNubType(i) == 1 && !MacroCubic.Checked)
                                        {
                                            if (i != 1 || pType == (int)LineTypes.Cubic)
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
                    using (Pen p = new Pen(LineColors[pType]))
                    using (Pen activePen = new Pen(LineColors[pType]))
                    {
                        p.DashStyle = DashStyle.Solid;
                        p.Width = 1;

                        activePen.Width = 5f;
                        activePen.Color = Color.FromArgb(51, p.Color);

                        if (pPoints.Length > 3 && (pType == (int)LineTypes.Quadratic || pType == (int)LineTypes.SmoothQuadratic))
                        {
                            try
                            {
                                e.Graphics.DrawBeziers(p, Qpts);
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawBeziers(activePen, Qpts);
                            }
                            catch { }
                        }
                        else if (pPoints.Length > 3 && (pType == (int)LineTypes.Cubic || pType == (int)LineTypes.SmoothCubic))
                        {
                            try
                            {
                                e.Graphics.DrawBeziers(p, pts);
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawBeziers(activePen, pts);
                            }
                            catch { }
                        }
                        else if (pPoints.Length > 1 && pType == (int)LineTypes.Straight)
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
                        else if (pPoints.Length == 5 && pType == (int)LineTypes.Ellipse)
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
                                        AddPath(gp, pts[0]
                                        , l, h, a,
                                        (isLarge) ? 1 : 0,
                                        (revSweep) ? 1 : 0,
                                        pts[4]);
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
                                                AddPath(gp, pts[0]
                                                , l, h, a,
                                                (isLarge) ? 0 : 1,
                                                (revSweep) ? 0 : 1,
                                                pts[4]);
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
                            if (MacroCircle.Checked && Ellipse.Checked) noJoin = true;
                            if (MacroRect.Checked && StraightLine.Checked) noJoin = true;
                        }

                        if (!mpMode)
                        {
                            if (!noJoin && isClosed && pts.Length > 1)
                            {
                                e.Graphics.DrawLine(p, pts[0], pts[pts.Length - 1]);//preserve
                                if (j == LineList.SelectedIndex)
                                    e.Graphics.DrawLine(activePen, pts[0], pts[pts.Length - 1]);//preserve

                                loopBack = pts[pts.Length - 1];
                            }
                        }
                        else
                        {
                            //if (!noJoin && ctype && pts.Length > 1)
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
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private PointF ThirdPoint(PointF p1, PointF p2, bool flip, float curve)
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

        private PointF ThirdPointAbs(PointF p1, PointF p2, bool flip, float curve)
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
            float dist = curve / pythag(new PointF(x3, y3), p2);

            x3 = (x3 - p2.X) * dist + p2.X;
            y3 = (y3 - p2.Y) * dist + p2.Y;
            return new PointF(x3, y3);
        }

        private PointF reverseAverage(PointF p1, PointF p2)
        {
            return new PointF
            {
                X = p2.X * 2f - p1.X,
                Y = p2.Y * 2f - p1.Y
            };
        }

        private PointF AsymRevAverage(PointF p0, PointF p1, PointF p2, PointF c1)
        {
            PointF tmp = reverseAverage(c1, p1);
            float py1 = pythag(p0, p1);
            float py2 = pythag(p2, p1);
            float norm = (py1 + py2) / (py1 * 2f + .00001f);
            tmp.X = (tmp.X - c1.X) * norm + c1.X;
            tmp.Y = (tmp.Y - c1.Y) * norm + c1.Y;
            return tmp;
        }

        private PointF pointAverage(PointF p1, PointF p2)
        {
            float x3, y3;
            x3 = (p2.X + p1.X) / 2f;
            y3 = (p2.Y + p1.Y) / 2f;
            return new PointF(x3, y3);
        }

        private PointF PathAverage(PointF[] p)
        {
            float x = 0, y = 0;
            if (p.Length != 0)
            {
                foreach (PointF pt in p)
                {
                    x += pt.X;
                    y += pt.Y;
                }
                return new PointF(x / (float)p.Length, y / (float)p.Length);
            }
            return new PointF(0, 0);
        }

        private float pythag(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private float hypot(float c, float a)
        {
            double cq = Math.Pow(c, 2);
            double aq = Math.Pow(a, 2);
            return (float)(Math.Sqrt(Math.Abs(cq - aq)) * Math.Sign(cq - aq));
        }

        private void style_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem s = (ToolStripMenuItem)sender;
            Big.Enabled = Ellipse.Checked & !MacroCircle.Checked;
            Sweep.Enabled = Ellipse.Checked & !MacroCircle.Checked;
            if ((sender as ToolStripMenuItem).Checked)
            {

                if (canvasPoints.Length > 0)
                {

                    PointF hold = canvasPoints[0];
                    Array.Resize(ref canvasPoints, 1);
                    canvasPoints[0] = hold;

                }
                canvas.Refresh();
            }
            menuStrip1.Refresh();
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
            if (canvasPoints.Length <= 1)
                return;

            if (Lines.Count < maxPaths)
            {
                setUndo(sender == LineList);
                if (MacroCircle.Checked && getPathType() == (int)LineTypes.Ellipse)
                {
                    if (canvasPoints.Length < 5) return;
                    PointF mid = pointAverage(canvasPoints[0], canvasPoints[4]);
                    canvasPoints[1] = canvasPoints[0];
                    canvasPoints[2] = canvasPoints[4];
                    canvasPoints[3] = mid;
                    Lines.Add(new PData(canvasPoints, false, getPathType(), Big.Checked, Sweep.Checked, string.Empty, false));
                    LineList.Items.Add(LineNames[(int)LineTypes.Ellipse]);
                    PointF[] tmp = new PointF[canvasPoints.Length];
                    //fix
                    tmp[0] = canvasPoints[4];
                    tmp[4] = canvasPoints[0];
                    tmp[3] = canvasPoints[3];
                    tmp[1] = tmp[0];
                    tmp[2] = tmp[4];
                    //test below
                    Lines.Add(new PData(tmp, false, getPathType(), Big.Checked, Sweep.Checked, string.Empty, true));
                    LineList.Items.Add(LineNames[(int)LineTypes.Ellipse]);
                }
                else if (MacroRect.Checked && getPathType() == (int)LineTypes.Straight)
                {
                    for (int i = 1; i < canvasPoints.Length; i++)
                    {
                        PointF[] tmp = new PointF[5];
                        tmp[0] = new PointF(canvasPoints[i - 1].X, canvasPoints[i - 1].Y);
                        tmp[1] = new PointF(canvasPoints[i].X, canvasPoints[i - 1].Y);
                        tmp[2] = new PointF(canvasPoints[i].X, canvasPoints[i].Y);
                        tmp[3] = new PointF(canvasPoints[i - 1].X, canvasPoints[i].Y);
                        tmp[4] = new PointF(canvasPoints[i - 1].X, canvasPoints[i - 1].Y);
                        Lines.Add(new PData(tmp, false, getPathType(), Big.Checked, Sweep.Checked, string.Empty, false));
                        LineList.Items.Add(LineNames[getPathType()]);
                    }
                }
                else
                {
                    Lines.Add(new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked, Sweep.Checked, string.Empty, MPMode.Checked));
                    LineList.Items.Add(LineNames[getPathType()]);
                }
            }
            else
            {
                MessageBox.Show($"Too Many Paths in List (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            resetRotation();

            PointF hold = canvasPoints[canvasPoints.Length - 1];
            if (!LinkedL.Checked)
            {
                Array.Resize(ref canvasPoints, 1);
                canvasPoints[0] = hold;
            }
            else
            {
                Array.Resize(ref canvasPoints, 0);
            }

            canvas.Refresh();
        }

        private void resetRotation()
        {
            SpinLine.Value = 180;
            toolTip1.SetToolTip(SpinLine, "0.0\u00B0");
            lastRot = 180;
        }

        private void removebtn_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex == -1) return;
            setUndo();
            if ((LineList.Items.Count > 0) && (LineList.SelectedIndex < Lines.Count))
            {
                int spi = LineList.SelectedIndex;
                Lines.RemoveAt(spi);
                LineList.Items.RemoveAt(spi);
                canvasPoints = new PointF[0];
                LineList.SelectedIndex = -1;
            }
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

                Lines.Add(new PData(tmp, Loop.Checked, getPathType(), Big.Checked, Sweep.Checked, string.Empty, MPMode.Checked));
                LineList.Items.Add(LineNames[getPathType()]);
                LineList.SelectedIndex = LineList.Items.Count - 1;

                canvas.Refresh();
            }
            else
            {
                MessageBox.Show($"Too Many Lines in List (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LineList_SelectedValueChanged(object sender, EventArgs e)
        {
            if (isNewPath && canvasPoints.Length > 1)
                ApplyBtn_Click(LineList, new EventArgs());
            isNewPath = false;


            if (LineList.SelectedIndex == -1) return;

            if ((LineList.Items.Count > 0) && (LineList.SelectedIndex < Lines.Count))
            {
                PData selectedPath = (Lines[LineList.SelectedIndex] as PData);
                setUiForPath(selectedPath.LineType, selectedPath.ClosedType, selectedPath.IsLarge, selectedPath.RevSweep, selectedPath.LoopBack);
                canvasPoints = selectedPath.Lines;
            }
            canvas.Refresh();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {

            if (keyData == Keys.Enter)
            {
                if (keylock == 0 && LineList.SelectedIndex == -1)
                {
                    keylock = 10;
                    ApplyBtn_Click(ApplyBtn, new EventArgs());
                }
                return true;
            }
            else if (keyData == Keys.Escape)
            {
                Deselect_Click(ClearBtn, new EventArgs());
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool getPathData(float Width, float Height, out string output)
        {
            string strPath = string.Empty;
            if (SolidFillMenuItem.Checked) strPath = "F1 ";
            if (Lines.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;


            for (int index = 0; index < Lines.Count; index++)
            {
                PData currentPath = (Lines[index] as PData);
                int lt = currentPath.LineType;
                PointF[] line = currentPath.Lines;
                bool islarge = currentPath.IsLarge;
                bool revsweep = currentPath.RevSweep;
                if (line.Length < 2)
                {
                    continue;
                }
                float x, y;


                x = Width * line[0].X;
                y = Height * line[0].Y;

                if (index == 0 || (x != oldx || y != oldy) || currentPath.ClosedType)
                {
                    if (index > 0) strPath += " ";
                    strPath += "M ";
                    strPath += $"{x:0.##}";
                    strPath += ",";
                    strPath += $"{y:0.##}";
                }
                switch (lt)
                {
                    case (int)LineTypes.Straight:
                        strPath += " L ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = Width * line[i].X;
                            y = Height * line[i].Y;
                            strPath += $"{x:0.##}";
                            strPath += ",";
                            strPath += $"{y:0.##}";
                            if (i < line.Length - 1) strPath += ",";

                        }
                        oldx = x; oldy = y;
                        break;
                    case (int)LineTypes.Ellipse:
                        strPath += " A ";
                        PointF[] pts = new PointF[line.Length];
                        for (int i = 0; i < line.Length; i++)
                        {
                            x = Width * line[i].X;
                            y = Height * line[i].Y;
                            pts[i] = new PointF(x, y);
                        }
                        PointF mid = pointAverage(pts[0], pts[4]);
                        float l = pythag(mid, pts[1]);
                        float h = pythag(mid, pts[2]);
                        float a = (float)(Math.Atan2(pts[3].Y - mid.Y, pts[3].X - mid.X) * 180 / Math.PI);
                        float b = (islarge) ? 1 : 0;
                        float s = (revsweep) ? 1 : 0;
                        strPath += $"{l:0.##}";
                        strPath += ",";
                        strPath += $"{h:0.##}";
                        strPath += ",";
                        strPath += $"{a:0.##}";
                        strPath += ",";
                        strPath += $"{b:0}";
                        strPath += ",";
                        strPath += $"{s:0}";
                        strPath += ",";
                        strPath += $"{pts[4].X:0.##}";
                        strPath += ",";
                        strPath += $"{pts[4].Y:0.##}";
                        oldx = pts[4].X;
                        oldy = pts[4].Y;
                        break;
                    case (int)LineTypes.Cubic:
                        strPath += " C ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            x = Width * line[i].X;
                            y = Height * line[i].Y;
                            strPath += $"{x:0.##}";
                            strPath += ",";
                            strPath += $"{y:0.##}";
                            if (i < line.Length - 1) strPath += ",";
                            oldx = x; oldy = y;
                        }

                        break;
                    case (int)LineTypes.Quadratic:
                        strPath += " Q ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 2)
                            {
                                x = Width * line[i].X;
                                y = Height * line[i].Y;
                                strPath += $"{x:0.##}";
                                strPath += ",";
                                strPath += $"{y:0.##}";
                                if (i < line.Length - 1) strPath += ",";
                                oldx = x; oldy = y;
                            }
                        }

                        break;
                    case (int)LineTypes.SmoothCubic:
                        strPath += " S ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 1)
                            {
                                x = Width * line[i].X;
                                y = Height * line[i].Y;
                                strPath += $"{x:0.##}";
                                strPath += ",";
                                strPath += $"{y:0.##}";
                                if (i < line.Length - 1) strPath += ",";
                                oldx = x; oldy = y;
                            }
                        }
                        break;
                    case (int)LineTypes.SmoothQuadratic:
                        strPath += " T ";
                        for (int i = 1; i < line.Length; i++)
                        {
                            if (getNubType(i) != 2 && getNubType(i) != 1)
                            {
                                x = Width * line[i].X;
                                y = Height * line[i].Y;
                                strPath += $"{x:0.##}";
                                strPath += ",";
                                strPath += $"{y:0.##}";
                                if (i < line.Length - 1) strPath += ",";
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

        private bool getPGPathData(float Width, float Height, out string output)
        {
            string strPath = string.Empty;
            if (Lines.Count < 1)
            {
                output = string.Empty;
                return false;
            }
            float oldx = 0, oldy = 0;
            //int oldLt = -1;
            string[] repstr = { "~1", "~2", "~3" };
            string tmpstr = string.Empty;
            for (int index = 0; index < Lines.Count; index++)
            {
                Application.DoEvents();

                PData currentPath = (Lines[index] as PData);
                int lt = currentPath.LineType;
                PointF[] line = currentPath.Lines;
                bool islarge = currentPath.IsLarge;
                bool revsweep = currentPath.RevSweep;
                if (line.Length < 2)
                {
                    continue;
                }
                float x, y;


                x = Width * line[0].X;
                y = Height * line[0].Y;

                //if (oldLt != lt || (x != oldx || y != oldy))
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
                    case (int)LineTypes.Straight:

                        tmpstr = string.Empty;
                        for (int i = 1; i < line.Length; i++)
                        {
                            strPath += $"\t\t\t\t\t{Properties.Resources.PGLine}\r\n";
                            x = Width * line[i].X;
                            y = Height * line[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";

                            strPath = strPath.Replace("~1", tmpstr);
                        }

                        oldx = x; oldy = y;
                        break;
                    case (int)LineTypes.Ellipse:
                        strPath += $"\t\t\t\t\t{Properties.Resources.PGEllipse}\r\n";
                        PointF[] pts = new PointF[line.Length];
                        for (int i = 0; i < line.Length; i++)
                        {
                            x = Width * line[i].X;
                            y = Height * line[i].Y;
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
                    case (int)LineTypes.SmoothCubic:
                    case (int)LineTypes.Cubic:

                        for (int i = 1; i < line.Length - 1; i += 3)
                        {

                            strPath += $"\t\t\t\t\t{Properties.Resources.PGBezier}\r\n";
                            for (int j = 0; j < 3; j++)
                            {
                                x = Width * line[j + i].X;
                                y = Height * line[j + i].Y;
                                tmpstr = $"{x:0.##},{y:0.##}";
                                strPath = strPath.Replace(repstr[j], tmpstr);
                            }
                        }

                        oldx = x; oldy = y;
                        break;
                    case (int)LineTypes.SmoothQuadratic:
                    case (int)LineTypes.Quadratic:

                        for (int i = 1; i < line.Length - 1; i += 3)
                        {
                            strPath += $"\t\t\t\t\t{Properties.Resources.PQQuad}\r\n";

                            x = Width * line[i].X;
                            y = Height * line[i].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~1", tmpstr);
                            x = Width * line[i + 2].X;
                            y = Height * line[i + 2].Y;
                            tmpstr = $"{x:0.##},{y:0.##}";
                            strPath = strPath.Replace("~2", tmpstr);

                        }

                        oldx = x; oldy = y;
                        break;
                }

                if (currentPath.ClosedType || currentPath.LoopBack)
                {
                    strPath = strPath.Replace("~0", "True");
                    //strPath += "\t\t\t\t</PathFigure>\r\n";
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
            if (strPath.Length == 0) return;
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

                        if (pts.Length > 1 && LineList.Items.Count < maxPaths) addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);

                        Array.Resize(ref pts, 1);
                        pts[0] = LastPos;

                        lineType = tmpline;
                        closedType = false;

                    }
                    if (strMode != "z") continue;
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
                        errorflagx = float.TryParse(str[i], out x);
                        if (!errorflagx) break;
                        SolidFillMenuItem.Checked = false;
                        if (x == 1)
                        {
                            SolidFillMenuItem.Checked = true;
                        }
                        else
                        {
                            SolidFillMenuItem.Checked = false;
                        }
                        break;
                    case "m":
                        errornum = 1;
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y); ;
                        HomePos = LastPos;
                        break;

                    case "c":
                    case "l":

                        Array.Resize(ref pts, pts.Length + 1);
                        errornum = 2;
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "s":
                        errornum = 9;
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y);
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
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y);
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
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y);
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
                        errorflagx = float.TryParse(str[i++], out x);
                        if (!errorflagx) break;
                        x = x / canvas.ClientRectangle.Height;
                        LastPos = pbAdjust(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "v":

                        Array.Resize(ref pts, pts.Length + 1);
                        x = LastPos.X;
                        errornum = 4;
                        errorflagy = float.TryParse(str[i], out y);
                        if (!errorflagy) break;
                        y = y / canvas.ClientRectangle.Height;
                        LastPos = pbAdjust(x, y);
                        pts[pts.Length - 1] = LastPos;
                        break;
                    case "a":

                        int ptbase = 0;
                        Array.Resize(ref pts, pts.Length + 4);
                        //to
                        errornum = 5;
                        errorflagx = float.TryParse(str[i + 5], out x);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i + 6], out y);
                        if (!errorflagy) break;
                        LastPos = pbAdjust(x, y);
                        pts[ptbase + 4] = LastPos; ;//ENDPOINT

                        PointF From = new PointF(pts[ptbase].X * (float)canvas.ClientRectangle.Width,
                            pts[ptbase].Y * (float)canvas.ClientRectangle.Height);
                        PointF To = new PointF(x, y);

                        PointF mid = pointAverage(From, To);
                        PointF mid2 = ThirdPoint(From, mid, true, 1f);
                        float far = pythag(From, mid);
                        float atan = (float)Math.Atan2(mid2.Y - mid.Y, mid2.X - mid.X);

                        float dist, dist2;
                        errornum = 6;
                        errorflagx = float.TryParse(str[i], out dist);//W
                        if (!errorflagx) break;
                        pts[ptbase + 1] = pointOrbit(mid, atan - (float)Math.PI / 4f, dist);

                        errorflagx = float.TryParse(str[i + 1], out dist);//H
                        if (!errorflagx) break;
                        pts[ptbase + 2] = pointOrbit(mid, atan + (float)Math.PI / 4f, dist);

                        errornum = 7;
                        errorflagx = float.TryParse(str[i + 2], out dist);
                        float rot = dist * (float)Math.PI / 180f;//ROT
                        pts[ptbase + 3] = pointOrbit(mid, rot, far);

                        errornum = 8;
                        errorflagx = float.TryParse(str[i + 3], out dist);
                        if (!errorflagx) break;
                        errorflagy = float.TryParse(str[i + 4], out dist2);
                        if (!errorflagy) break;
                        islarge = Convert.ToBoolean(dist);
                        revsweep = Convert.ToBoolean(dist2);

                        i += 6;
                        strMode = "n";
                        break;
                }
                if (!errorflagx || !errorflagy) break;
            }
            if (!errorflagx || !errorflagy || lineType < 0)
            {
                MessageBox.Show("No Line Type, or is not in the StreamGeometry Format",// + Environment.NewLine + "Error Number " + errornum.ToString(),
                    "Not a valid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (pts.Length > 1 && LineList.Items.Count < maxPaths) addPathtoList(pts, lineType, closedType, islarge, revsweep, mpmode);
            canvas.Refresh();
        }

        private PointF pointOrbit(PointF c, float r, float d)
        {
            float x = (float)Math.Cos(r) * d;
            float y = (float)Math.Sin(r) * d;
            PointF results = pbAdjust(c.X + x, c.Y + y);
            return results;
        }

        private PointF pbAdjust(float x, float y)
        {
            return new PointF(
                            x / (float)canvas.ClientSize.Width,
                            y / (float)canvas.ClientSize.Height);
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
                MessageBox.Show($"Too Many Lines in List (Max is {maxPaths})", "Buffer Full", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            }
        }

        private void LineList_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            int itemIndex = e.Index;
            if (itemIndex >= 0 && itemIndex < LineList.Items.Count)
            {
                Graphics g = e.Graphics;

                if (isItemSelected)
                {
                    using (SolidBrush backgroundColorBrush = new SolidBrush(Color.LightGray))
                        g.FillRectangle(backgroundColorBrush, e.Bounds);
                }

                PData itemPath = (Lines[itemIndex] as PData);

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

                using (SolidBrush itemTextColorBrush = new SolidBrush(LineColors[itemPath.LineType]))
                    g.DrawString(itemText, e.Font, itemTextColorBrush, LineList.GetItemRectangle(itemIndex).Location);

            }

            e.DrawFocusRectangle();
        }

        private void ClearAll()
        {
            Array.Resize(ref canvasPoints, 0);
            statusLabelNubsUsed.Text = $"{canvasPoints.Length}/{maxpoint} Nubs used";
            statusLabelLocation.Text = "0, 0";

            if (LineList.Items.Count == 0) return;
            Lines.Clear();
            LineList.Items.Clear();
            statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";

            canvas.Refresh();
        }

        private void clearAll_Click(object sender, EventArgs e)
        {
            DialogResult result2 = MessageBox.Show("Delete All Paths?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result2 == DialogResult.Yes)
            {
                setUndo();
                ClearAll();
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
            if (Lines.Count > 0)
            {
                ZoomToFactor(1);
                string TMP = string.Empty;
                bool r = getPathData((int)(OutputScale.Value * canvas.ClientRectangle.Width / 100), (int)(OutputScale.Value * canvas.ClientRectangle.Height / 100), out TMP);
                if (r)
                {
                    Clipboard.SetText(TMP);
                    MessageBox.Show("SVG Copied to Clipboard", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Copy Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else { MessageBox.Show("Nothing to Copy", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void Undo_Click(object sender, EventArgs e)
        {
            if (UDCount > 0)
            {
                canvasPoints = new PointF[0];
                setUiForPath(UDtype[UDPointer], false, false, false, false);
                if (UDPoint[UDPointer].Length != 0)
                {
                    canvasPoints = new PointF[UDPoint[UDPointer].Length];
                    Array.Copy(UDPoint[UDPointer], canvasPoints, canvasPoints.Length);
                }

                //MDown = UDtype[UDPointer];
                LineList.Items.Clear();
                Lines = new ArrayList();
                if (UDLines[UDPointer].Count != 0)
                {
                    foreach (PData pd in UDLines[UDPointer])
                    {
                        PointF[] tmp = new PointF[pd.Lines.Length];
                        Array.Copy(pd.Lines, tmp, pd.Lines.Length);
                        Lines.Add(new PData(tmp, pd.ClosedType, pd.LineType, pd.IsLarge, pd.RevSweep, pd.Alias, pd.LoopBack));
                        LineList.Items.Add(LineNames[pd.LineType]);
                    }
                    if (UDSelect[UDPointer] < LineList.Items.Count) LineList.SelectedIndex = UDSelect[UDPointer];
                }
                UDCount--;
                UDCount = (UDCount < 0) ? 0 : UDCount;
                UDPointer += (UndoMax - 1);
                UDPointer %= UndoMax;
            }

            undoToolStripMenuItem.Enabled = (UDCount != 0);
            resetRotation();

            menuStrip1.Refresh();
            canvas.Refresh();
        }

        private void Flip_Click(object sender, EventArgs e)
        {
            setUndo();

            if (canvasPoints.Length == 0)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    PData currentPath = (Lines[k] as PData);
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
                    if (currentPath.LineType == (int)LineTypes.Ellipse)
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
                if (Ellipse.Checked) Sweep.Checked = !Sweep.Checked;
                canvasPoints = tmp;

                // If modifying an existing Path, save the change
                if (LineList.SelectedIndex != -1)
                {
                    Lines[LineList.SelectedIndex] = new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked,
                        Sweep.Checked, (Lines[LineList.SelectedIndex] as PData).Alias, MPMode.Checked);
                    LineList.Items[LineList.SelectedIndex] = LineNames[getPathType()];
                }
            }
            canvas.Refresh();

        }

        private void FitBG_CheckedChanged(object sender, EventArgs e)
        {
            canvas.BackgroundImageLayout = (FitBG.Checked) ? ImageLayout.Zoom : ImageLayout.Center;
            canvas.Refresh();
        }

        private void FlipLines(int index)
        {
            if (index == -1)
                return;

            PData pd1 = (Lines[index] as PData);
            string LineTxt1 = LineList.Items[index].ToString();
            PointF[] tmp1 = new PointF[pd1.Lines.Length];
            Array.Copy(pd1.Lines, tmp1, pd1.Lines.Length);
            int tmpType1 = pd1.LineType;
            bool tmpClosed1 = pd1.ClosedType;
            bool tmpIsLarge1 = pd1.IsLarge;
            bool tmpRevSweep1 = pd1.RevSweep;
            string tmpAlias1 = pd1.Alias;

            PData pd2 = (Lines[index + 1] as PData);
            string LineTxt2 = LineList.Items[index + 1].ToString();
            PointF[] tmp2 = new PointF[pd2.Lines.Length];
            Array.Copy(pd2.Lines, tmp2, pd2.Lines.Length);
            int tmpType2 = pd2.LineType;
            bool tmpClosed2 = pd2.ClosedType;
            bool tmpIsLarge2 = pd2.IsLarge;
            bool tmpRevSweep2 = pd2.RevSweep;
            string tmpAlias2 = pd2.Alias;
            bool tmpMPMode2 = pd2.LoopBack;
            LineList.Items[index] = LineTxt2;

            Lines[index] = new PData(tmp2, tmpClosed2, tmpType2, tmpIsLarge2, tmpRevSweep2, tmpAlias2, tmpMPMode2);

            LineList.Items[index + 1] = LineTxt1;

            Lines[index + 1] = new PData(tmp1, tmpClosed1, tmpType1, tmpIsLarge1, tmpRevSweep1, tmpAlias1, tmpMPMode2);
        }

        private void DNList_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex > -1 && LineList.SelectedIndex < LineList.Items.Count - 1)
            {
                FlipLines(LineList.SelectedIndex);
                LineList.SelectedIndex++;
            }
        }

        private void upList_Click(object sender, EventArgs e)
        {
            if (LineList.SelectedIndex > 0)
            {
                FlipLines(LineList.SelectedIndex - 1);
                LineList.SelectedIndex--;
            }
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

        private void SpinLine_ValueChanged(object sender, float e)
        {
            toolTip1.SetToolTip((sender as BigKnobs), $"{e - 180f:0.0}\u00B0");

            double rad = (double)(lastRot - e) * Math.PI / 180;
            lastRot = e;


            if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
            {
                PointF mid = new PointF(.5f, .5f);
                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] tmp = (Lines[k] as PData).Lines;

                    for (int i = 0; i < tmp.Length; i++)
                    {
                        double x = (double)(tmp[i].X - mid.X);
                        double y = (double)(tmp[i].Y - mid.Y);
                        double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + (double)mid.X;
                        double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + (double)mid.Y;

                        tmp[i] = new PointF((float)nx, (float)ny);
                    }
                }
            }
            else if (canvasPoints.Length > 1)
            {
                PointF[] tmp = new PointF[canvasPoints.Length];
                Array.Copy(canvasPoints, tmp, canvasPoints.Length);
                PointF mid = PathAverage(tmp);

                for (int i = 0; i < tmp.Length; i++)
                {
                    double x = (double)(tmp[i].X - mid.X);
                    double y = (double)(tmp[i].Y - mid.Y);
                    double nx = Math.Cos(rad) * x + Math.Sin(rad) * y + (double)mid.X;
                    double ny = Math.Cos(rad) * y - Math.Sin(rad) * x + (double)mid.Y;

                    tmp[i] = new PointF((float)nx, (float)ny);
                }
                canvasPoints = tmp;

                // If modifying an existing Path, save the change
                if (LineList.SelectedIndex != -1)
                {
                    Lines[LineList.SelectedIndex] = new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked,
                        Sweep.Checked, (Lines[LineList.SelectedIndex] as PData).Alias, MPMode.Checked);
                    LineList.Items[LineList.SelectedIndex] = LineNames[getPathType()];
                }
            }

            canvas.Refresh();
        }

        private void SpinLine_MouseDown(object sender, MouseEventArgs e)
        {
            setUndo();
            toolTip1.Show($"{SpinLine.Value:0.0}\u00B0", (sender as BigKnobs));
        }

        private void LineLoop_Click(object sender, EventArgs e)
        {
            if (canvasPoints.Length > 2 && !Ellipse.Checked)
            {
                setUndo();
                canvasPoints[canvasPoints.Length - 1] = canvasPoints[0];
                canvas.Refresh();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            if (keylock > 0) keylock--;
            if (!Ellipse.Checked) MacroCircle.Checked = false;
            if (!StraightLine.Checked) MacroRect.Checked = false;
            if (!Cubic.Checked) MacroCubic.Checked = false;

            ToggleUpDownButtons();
            clonePathButton.Enabled = (LineList.SelectedIndex > -1);
            removePathButton.Enabled = (LineList.SelectedIndex > -1);
            MacroCircle.Enabled = (LineList.SelectedIndex == -1);
            MacroRect.Enabled = (LineList.SelectedIndex == -1);
            MacroCubic.Enabled = (LineList.SelectedIndex == -1);
            Loop.Enabled = !((MacroCircle.Checked && MacroCircle.Enabled) || (MacroRect.Checked && MacroRect.Enabled));
            MPMode.Enabled = !((MacroCircle.Checked && MacroCircle.Enabled) || (MacroRect.Checked && MacroRect.Enabled));
            ClearBtn.Enabled = (canvasPoints.Length != 0);
            ApplyBtn.Enabled = (LineList.SelectedIndex == -1 && canvasPoints.Length > 1);

            //MPMode.Enabled = Loop.Checked;
            //if (!Loop.Checked) MPMode.Checked = false;

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
                statusLabelNubsUsed.Text = $"{canvasPoints.Length}/{maxpoint} Nubs used";
                statusLabelPathsUsed.Text = $"{LineList.Items.Count}/{maxPaths} Paths used";
            }

            if (LineList.SelectedIndex == -1) isNewPath = true;
        }

        private string getMyFolder()
        {
            string fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("PDNDwarves");

            if (rk != null)
            {
                try
                {
                    fp = (string)rk.GetValue("ShapeMaker");
                    if (!Directory.Exists(fp)) fp = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
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
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("PDNDwarves");

            if (rk != null)
            {
                try
                {
                    fp = (string)rk.GetValue("ShapeMakerPrj");
                    if (!Directory.Exists(fp)) fp = Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
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
            key = Registry.CurrentUser.CreateSubKey("PDNDwarves");
            key.SetValue("ShapeMaker", Path.GetDirectoryName(filePath));
            key.Close();
        }

        private void saveMyProjectFolder(string filePath)
        {
            RegistryKey key;
            key = Registry.CurrentUser.CreateSubKey("PDNDwarves");
            key.SetValue("ShapeMakerPrj", Path.GetDirectoryName(filePath));
            key.Close();
        }

        private void exportPndShape_Click(object sender, EventArgs e)
        {
            if (Lines.Count == 0)
            {
                MessageBox.Show("Nothing to Save", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string TMP = string.Empty;
            bool r = getPathData((int)(OutputScale.Value * canvas.ClientRectangle.Width / 100), (int)(OutputScale.Value * canvas.ClientRectangle.Height / 100), out TMP);
            if (!r)
            {
                MessageBox.Show("Save Error", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ZoomToFactor(1);
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

                using (StreamWriter writer = new StreamWriter(sfd.FileName))
                    writer.Write(output);
                MessageBox.Show("The shape has been exported as a XAML file for use in paint.net.", "Paint.net Shape Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            bool r = getPGPathData((int)(OutputScale.Value * canvas.ClientRectangle.Width / 100), (int)(OutputScale.Value * canvas.ClientRectangle.Height / 100), out TMP);
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

                using (StreamWriter writer = new StreamWriter(sfd.FileName))
                    writer.Write(output);
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

                string data;
                string[] d;
                using (StreamReader reader = new StreamReader(OFD.FileName))
                {
                    data = reader.ReadToEnd();
                    d = data.Split(new char[] { '"' });
                }
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

        private void Loops_Click(object sender, EventArgs e)
        {
            if (MPMode.Equals(sender))
            {
                if (!MPMode.Checked)
                {
                    Loop.Checked = false;
                    MPMode.Checked = true;
                }
                else
                {
                    MPMode.Checked = false;
                }
            }
            else
            {
                if (!Loop.Checked)
                {
                    Loop.Checked = true;
                    MPMode.Checked = false;
                }
                else
                {
                    Loop.Checked = false;
                }
            }
            canvas.Refresh();

            // If modifying an existing Path, save the change
            if (LineList.SelectedIndex != -1)
            {
                Lines[LineList.SelectedIndex] = new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked,
                    Sweep.Checked, (Lines[LineList.SelectedIndex] as PData).Alias, MPMode.Checked);
                LineList.Items[LineList.SelectedIndex] = LineNames[getPathType()];
            }
        }

        private void Property_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            canvas.Refresh();

            // If modifying an existing Path, save the change
            if (LineList.SelectedIndex != -1)
            {
                Lines[LineList.SelectedIndex] = new PData(canvasPoints, Loop.Checked, getPathType(), Big.Checked,
                    Sweep.Checked, (Lines[LineList.SelectedIndex] as PData).Alias, MPMode.Checked);
                LineList.Items[LineList.SelectedIndex] = LineNames[getPathType()];
            }
        }

        private void MacroRect_Click(object sender, EventArgs e)
        {
            MacroRect.Checked = !MacroRect.Checked;
            if (MacroRect.Checked) ToolMenu_Click(radios[0], e);
            canvas.Refresh();
        }

        private void MacroCubic_Click(object sender, EventArgs e)
        {
            MacroCubic.Checked = !MacroCubic.Checked;
            if (MacroCubic.Checked) ToolMenu_Click(radios[2], e);
            if (MacroCubic.Checked) CubicAdjust();
            canvas.Refresh();
        }

        private void MacroCircle_Click(object sender, EventArgs e)
        {
            MacroCircle.Checked = !MacroCircle.Checked;
            Big.Enabled = !MacroCircle.Checked;
            Sweep.Enabled = !MacroCircle.Checked;
            if (MacroCircle.Checked) ToolMenu_Click(radios[1], e);
            canvas.Refresh();
        }

        private void ToolMenu_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem s = (ToolStripMenuItem)sender;
            int z = Convert.ToInt32(s.Tag);
            if (z != MDown) MDown = -1;
            foreach (ToolStripMenuItem t in radios) t.Checked = (t.Name.Equals(s.Name));
        }

        private void SpinLine_MouseUp(object sender, MouseEventArgs e)
        {
            toolTip1.Hide((sender as BigKnobs));
        }

        private Bitmap SpriteSheet(int x, int y)
        {
            Bitmap bmp = new Bitmap((int)(buttonWd * DPI), (int)(buttonHt * DPI));
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(ShapeMaker.Properties.Resources.NewUI,
                    new RectangleF(0, 0, (int)(buttonWd * DPI), (int)(buttonHt * DPI)),
                    new RectangleF(x * buttonWd, y * buttonHt, buttonWd, buttonHt),
                    GraphicsUnit.Pixel);
            }
            return bmp;
        }

        private void ToolChecked_Paint(object sender, PaintEventArgs e)
        {
            int z = Convert.ToInt32((sender as ToolStripMenuItem).Tag);

            if (!(sender as ToolStripMenuItem).Enabled)
            {
                e.Graphics.DrawImage(SpriteSheet(z, 2), 0, 0);
            }
            else if ((z == MDown && sender != Big && sender != Sweep) || (sender as ToolStripMenuItem).Checked)
            {
                e.Graphics.DrawImage(SpriteSheet(z, 1), 0, 0);

            }
            else
            {
                e.Graphics.DrawImage(SpriteSheet(z, 0), 0, 0);
            }


        }

        private void undoToolStripMenuItem_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) (sender as ToolStripMenuItem).PerformClick();
            (sender as ToolStripMenuItem).Checked = true;
            menuStrip1.Refresh();
        }

        private void undoToolStripMenuItem_MouseUp(object sender, MouseEventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = false;
            menuStrip1.Refresh();
        }

        private void blank_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(Properties.Resources.blank2, 0, 0, (int)(blankWd * DPI), (int)(blankHt * DPI));
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
                catch (Exception e) { MessageBox.Show(e.Message); }

                PData currentPath = (Lines[j] as PData);
                line = currentPath.Lines;
                ltype = currentPath.LineType;
                ctype = currentPath.ClosedType;
                mpmode = currentPath.LoopBack;
                islarge = currentPath.IsLarge;
                revsweep = currentPath.RevSweep;


                PointF[] pts = new PointF[line.Length];
                PointF[] Qpts = new PointF[line.Length];
                for (int i = 0; i < line.Length; i++)
                {
                    float x = (float)OutputScale.Value * (float)SuperSize.Width / 100f * line[i].X;
                    float y = (float)OutputScale.Value * (float)SuperSize.Height / 100f * line[i].Y;
                    pts[i] = new PointF(x, y);
                }
                #region cube to quad
                if (ltype == (int)LineTypes.Quadratic || ltype == (int)LineTypes.SmoothQuadratic)
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


                if (line.Length > 3 && (ltype == (int)LineTypes.Quadratic || ltype == (int)LineTypes.SmoothQuadratic))
                {
                    try
                    {
                        PGP[j].AddBeziers(Qpts);
                    }
                    catch { }
                }
                else if (line.Length > 3 && (ltype == (int)LineTypes.Cubic || ltype == (int)LineTypes.SmoothCubic))
                {
                    try
                    {
                        PGP[j].AddBeziers(pts);
                    }
                    catch { }
                }
                else if (line.Length > 1 && ltype == (int)LineTypes.Straight)
                {


                    PGP[j].AddLines(pts);

                }
                else if (line.Length == 5 && ltype == (int)LineTypes.Ellipse)
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

                        AddPath(PGP[j], pts[0]
                            , l, h, a,
                            (islarge) ? 1 : 0,
                            (revsweep) ? 1 : 0,
                            pts[4]);
                    }



                }
                //if (ctype && pts.Length > 1)
                //{
                //    PointF[] points = new PointF[] { pts[0], pts[pts.Length - 1] };
                //    PGP[j].AddLines(points);
                //}
                if (!mpmode)
                {
                    if (ctype && pts.Length > 1)
                    {
                        PointF[] points = { pts[0], pts[pts.Length - 1] };
                        PGP[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                else
                {
                    if (pts.Length > 1)
                    {
                        PointF[] points = { loopBack, pts[pts.Length - 1] };
                        PGP[j].AddLines(points);
                        loopBack = pts[pts.Length - 1];
                    }
                }
                #endregion
                Oldxy = pts[pts.Length - 1];
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

        private void AddPath(GraphicsPath graphicsPath, PointF start, float radiusX, float radiusY, float angle, int size, int sweep, PointF end)
        {
            float RadiusX = Math.Abs(radiusX);
            float RadiusY = Math.Abs(radiusY);
            float Angle = angle;
            int Sweep = sweep;
            int Size = size;

            if (start == end)
            {
                return;
            }

            if (RadiusX == 0.0f && RadiusY == 0.0f)
            {
                graphicsPath.AddLine(start, end);
                return;
            }

            double sinPhi = Math.Sin(Angle * RadPerDeg);
            double cosPhi = Math.Cos(Angle * RadPerDeg);

            double x1dash = cosPhi * (start.X - end.X) / 2.0 + sinPhi * (start.Y - end.Y) / 2.0;
            double y1dash = -sinPhi * (start.X - end.X) / 2.0 + cosPhi * (start.Y - end.Y) / 2.0;

            double root;
            double numerator = RadiusX * RadiusX * RadiusY * RadiusY - RadiusX * RadiusX * y1dash * y1dash - RadiusY * RadiusY * x1dash * x1dash;

            float rx = RadiusX;
            float ry = RadiusY;

            if (numerator < 0.0)
            {
                float s = (float)Math.Sqrt(1.0 - numerator / (RadiusX * RadiusX * RadiusY * RadiusY));

                rx *= s;
                ry *= s;
                root = 0.0;
            }
            else
            {
                root = ((Size == 1 && Sweep == 1) || (Size == 0 && Sweep == 0) ? -1.0 : 1.0) * Math.Sqrt(numerator / (RadiusX * RadiusX * y1dash * y1dash + RadiusY * RadiusY * x1dash * x1dash));
            }

            double cxdash = root * rx * y1dash / ry;
            double cydash = -root * ry * x1dash / rx;

            double cx = cosPhi * cxdash - sinPhi * cydash + (start.X + end.X) / 2.0;
            double cy = sinPhi * cxdash + cosPhi * cydash + (start.Y + end.Y) / 2.0;

            double theta1 = VectorAngle(1.0, 0.0, (x1dash - cxdash) / rx, (y1dash - cydash) / ry);
            double dtheta = VectorAngle((x1dash - cxdash) / rx, (y1dash - cydash) / ry, (-x1dash - cxdash) / rx, (-y1dash - cydash) / ry);

            if (Sweep == 0 && dtheta > 0)
            {
                dtheta -= 2.0 * Math.PI;
            }
            else if (Sweep == 1 && dtheta < 0)
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

        private void keyboardShortcutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Shortcuts ks = new Shortcuts())
            {
                ks.ShowDialog(this);
            }

        }

        private void aboutShapeMakerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutBox1 box = new AboutBox1())
            {
                box.ShowDialog(this);
            }

        }

        private bool InView()
        {

            if (canvasPoints.Length > 0)
            {
                for (int j = 0; j < canvasPoints.Length; j++)
                {
                    if (canvasPoints[j].X > 1.5f) return false;
                    if (canvasPoints[j].Y > 1.5f) return false;

                }
            }

            if (Lines.Count > 0)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] pl = (Lines[k] as PData).Lines;
                    for (int j = 0; j < pl.Length; j++)
                    {
                        if (pl[j].X > 1.5f) return false;
                        if (pl[j].Y > 1.5f) return false;
                    }
                }
            }
            return true;

        }

        private void opaque_ValueChanged(object sender, float e)
        {
            canvas.Refresh();
        }

        private void tool_MouseDown(object sender, MouseEventArgs e)
        {
            if (canvasPoints.Length > 0) setUndo();
            int z = Convert.ToInt32((sender as ToolStripMenuItem).Tag);
            if (z > 0 && z < 11)
            {
                if (z != 2 && z != 6) //momentary down
                {
                    MDown = z;
                }
                else
                {
                    z = -1;
                }
            }
            else
            {
                z = -1;
            }

            if (e.Button == MouseButtons.Right) (sender as ToolStripMenuItem).PerformClick();
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

                        Array.Resize(ref canvasPoints, 0);

                        Lines.Clear();
                        Lines = projectPaths;

                        LineList.Items.Clear();
                        for (int i = 0; i < Lines.Count; i++)
                            LineList.Items.Add(LineNames[(Lines[i] as PData).LineType]);

                        FigureName.Text = (Lines[Lines.Count - 1] as PData).Meta;
                        SolidFillMenuItem.Checked = (Lines[Lines.Count - 1] as PData).SolidFill;

                        UDCount = 0;
                        UDPointer = 0;
                        undoToolStripMenuItem.Enabled = false;
                        resetRotation();
                        ZoomToFactor(1);
                        canvas.Refresh();
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
            bool r = getPathData((int)(OutputScale.Value * canvas.ClientRectangle.Width / 100), (int)(OutputScale.Value * canvas.ClientRectangle.Height / 100), out TMP);
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

                XmlSerializer ser = new XmlSerializer(typeof(ArrayList), new Type[] { typeof(PData) });
                (Lines[Lines.Count - 1] as PData).Meta = FigureName.Text;
                (Lines[Lines.Count - 1] as PData).SolidFill = SolidFillMenuItem.Checked;
                using (FileStream stream = File.Open(sfd.FileName, FileMode.Create))
                    ser.Serialize(stream, Lines);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.Alt) == Keys.Alt)
                if ((keyData & Keys.F) == Keys.F)
                {
                    return base.ProcessDialogKey(keyData);
                }
                else if ((keyData & Keys.E) == Keys.E)
                {
                    return base.ProcessDialogKey(keyData);
                }
                else if ((keyData & Keys.H) == Keys.H)
                {
                    return base.ProcessDialogKey(keyData);
                }
                else
                {

                    return true;
                }
            else
            {
                return base.ProcessDialogKey(keyData);
            }
        }

        private void LineList_DoubleClick(object sender, EventArgs e)
        {
            if (LineList.Items.Count == 0 || LineList.SelectedItem == null) return;
            string s = Microsoft.VisualBasic.Interaction.InputBox("Please enter a name for this path.", "Path Name",
                LineList.SelectedItem.ToString(), -1, -1);
            if (!s.IsNullOrEmpty()) (Lines[LineList.SelectedIndex] as PData).Alias = s;
        }

        private void splitButtonZoom_ButtonClick(object sender, EventArgs e)
        {
            PanFlag = false;

            if (canvas.Width > canvasBaseSize)
            {
                ZoomToFactor(1);
            }
            else if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                ZoomToFactor(8);
            }
            else if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                ZoomToFactor(4);
            }
            else
            {
                ZoomToFactor(2);
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
            int oldZoomFactor = canvas.Width / canvasBaseSize;
            if (oldZoomFactor == zoomFactor)
                return;

            int newDimension = canvasBaseSize * zoomFactor;
            int maxMoveX = newDimension - viewport.ClientSize.Width;
            int maxMoveY = newDimension - viewport.ClientSize.Height;

            canvas.Location = new Point(0, 0);
            Zoomed.X = (maxMoveX > 0) ? -maxMoveX / 2 : maxMoveX / -2;
            Zoomed.Y = (maxMoveY > 0) ? -maxMoveY / 2 : maxMoveY / -2;

            // to avoid flicker, the order of execution is important
            if (oldZoomFactor > zoomFactor) // Zooming Out
            {
                canvas.Location = Zoomed;
                canvas.Width = newDimension;
                canvas.Height = newDimension;
            }
            else // Zooming In
            {
                canvas.Width = newDimension;
                canvas.Height = newDimension;
                canvas.Location = Zoomed;
            }
            canvas.Refresh();

            splitButtonZoom.Text = $"Zoom {zoomFactor}x";

            // Update Position Bars
            posBarsTimer.Stop();
            posBarsTimer.Start();
            DrawPosBars = true;
            verPosBar.Refresh();
            horPosBar.Refresh();
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

            if (canvas.Height != canvas.Width)
                return;

            int delta = Math.Sign(e.Delta);
            int oldZoomFactor = canvas.Width / canvasBaseSize;
            if ((delta > 0 && oldZoomFactor == 8) || (delta < 0 && oldZoomFactor == 1))
                return;

            int newDimension = (delta > 0) ? canvas.Width * 2 : canvas.Width / 2;
            int zoomFactor = newDimension / canvasBaseSize;

            Point mousePosition = new Point(e.X - viewport.Location.X, e.Y - viewport.Location.Y);

            Zoomed = new Point((canvas.Location.X - mousePosition.X) * newDimension / canvas.Width + mousePosition.X,
                                (canvas.Location.Y - mousePosition.Y) * newDimension / canvas.Height + mousePosition.Y);

            // Clamp the canvas location; we're not overscrolling... yet
            int minX = (viewport.ClientSize.Width > newDimension) ? (viewport.ClientSize.Width - newDimension) / 2 : viewport.ClientSize.Width - newDimension;
            int maxX = (viewport.ClientSize.Width > newDimension) ? (viewport.ClientSize.Width - newDimension) / 2 : 0;
            Zoomed.X.Clamp(minX, maxX);

            int minY = (viewport.ClientSize.Height > newDimension) ? (viewport.ClientSize.Height - newDimension) / 2 : viewport.ClientSize.Height - newDimension;
            int maxY = (viewport.ClientSize.Height > newDimension) ? (viewport.ClientSize.Height - newDimension) / 2 : 0;
            Zoomed.Y.Clamp(minY, maxY);


            // to avoid flicker, the order of execution is important
            if (delta > 0) // Zooming In
            {
                canvas.Width = newDimension;
                canvas.Height = newDimension;
                canvas.Location = Zoomed;
            }
            else // Zooming Out
            {
                canvas.Location = Zoomed;
                canvas.Width = newDimension;
                canvas.Height = newDimension;
            }
            canvas.Refresh();

            splitButtonZoom.Text = $"Zoom {zoomFactor}x";

            posBarsTimer.Stop();
            posBarsTimer.Start();
            DrawPosBars = true;
            verPosBar.Refresh();
            horPosBar.Refresh();

            base.OnMouseWheel(e);
        }

        private void verPosBar_Paint(object sender, PaintEventArgs e)
        {
            if (DrawPosBars)
            {
                if (canvas.Height <= viewport.ClientRectangle.Height)
                    return;

                float length = viewport.ClientSize.Height / (canvas.Height / (float)viewport.ClientSize.Height);
                float maxPos = viewport.ClientSize.Height - length;
                float pos = Math.Abs(canvas.Location.Y) / (float)(canvas.Height - viewport.ClientSize.Height) * maxPos;
                RectangleF verBar = new RectangleF(1, pos, 3, length);
                e.Graphics.FillRectangle(Brushes.Gray, verBar);
            }
        }

        private void horPosBar_Paint(object sender, PaintEventArgs e)
        {
            if (DrawPosBars)
            {
                if (canvas.Width <= viewport.ClientSize.Width)
                    return;

                float length = viewport.ClientSize.Width / (canvas.Width / (float)viewport.ClientSize.Width);
                float maxPos = viewport.ClientSize.Width - length;
                float pos = Math.Abs(canvas.Location.X) / (float)(canvas.Width - viewport.ClientSize.Width) * maxPos;
                RectangleF horBar = new RectangleF(pos, 1, length, 3);
                e.Graphics.FillRectangle(Brushes.Gray, horBar);
            }
        }

        private void newProjectMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show("Clear the current Shape Project, and start over?", "New Shape", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                ClearAll();

                ZoomToFactor(1);
                resetRotation();
                FigureName.Text = "Untitled";

                UDCount = 0;
                UDPointer = 0;
                undoToolStripMenuItem.Enabled = false;
            }
        }

        private void scaleSlider_ValueChanged(object sender, float e)
        {
            float scale = e;
            toolTip1.SetToolTip(scaleSlider, $"{scale:0.00}x");

            if (scale > 1 && !InView())
                return;

            if (canvasPoints.Length == 0 && LineList.Items.Count > 0)
            {
                PointF pa = new PointF(.5f, .5f);

                for (int k = 0; k < Lines.Count; k++)
                {
                    PointF[] tmp = (Lines[k] as PData).Lines;
                    PointF[] tmp2 = (UDLines[UDPointer][k] as PData).Lines;
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i] = new PointF((tmp2[i].X - pa.X) * scale + pa.X,
                        (tmp2[i].Y - pa.Y) * scale + pa.Y);
                    }
                }
            }
            else if (canvasPoints.Length > 1)
            {
                PointF pa = PathAverage(canvasPoints);

                for (int idx = 0; idx < canvasPoints.Length; idx++)
                {
                    canvasPoints[idx].X = (UDPoint[UDPointer][idx].X - pa.X) * scale + pa.X;
                    canvasPoints[idx].Y = (UDPoint[UDPointer][idx].Y - pa.Y) * scale + pa.Y;
                }
            }
            canvas.Refresh();
        }

        private void scaleSlider_MouseDown(object sender, MouseEventArgs e)
        {
            if (canvasPoints.Length > 1 || LineList.Items.Count > 0)
                setUndo();

            toolTip1.SetToolTip(scaleSlider, $"{scaleSlider.Value:0.00}x");
        }

        private void scaleSlider_MouseUp(object sender, MouseEventArgs e)
        {
            scaleSlider.ValueChanged -= scaleSlider_ValueChanged;
            scaleSlider.Value = 1;
            toolTip1.SetToolTip(scaleSlider, $"{scaleSlider.Value:0.00}x");
            scaleSlider.ValueChanged += scaleSlider_ValueChanged;
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            removePathToolStripMenuItem.Enabled = (LineList.SelectedIndex > -1);
            clonePathToolStripMenuItem.Enabled = (LineList.SelectedIndex > -1);
            loopPathToolStripMenuItem.Enabled = (canvasPoints.Length > 1);
            flipHorizontalToolStripMenuItem.Enabled = (canvasPoints.Length > 1 || LineList.Items.Count > 0);
            flipVerticalToolStripMenuItem.Enabled = (canvasPoints.Length > 1 || LineList.Items.Count > 0);
            clearAllToolStripMenuItem.Enabled = (canvasPoints.Length > 0 || LineList.Items.Count > 0);
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

        private void EffectPluginConfigDialog_Resize(object sender, EventArgs e)
        {
            viewport.Width = LineList.Left - viewport.Left - 30;
            viewport.Height = statusStrip1.Top - viewport.Top - 15;

            horPosBar.Top = viewport.Bottom;
            horPosBar.Width = viewport.Width;

            verPosBar.Left = viewport.Right;
            verPosBar.Height = viewport.Height;

            if (canvas.Width < viewport.ClientSize.Width || canvas.Location.X > 0)
                Zoomed.X = (viewport.ClientSize.Width - canvas.Width) / 2;
            if (canvas.Height < viewport.ClientSize.Height || canvas.Location.Y > 0)
                Zoomed.Y = (viewport.ClientSize.Height - canvas.Height) / 2;
            canvas.Location = Zoomed;
        }
    }
}