using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShapeMaker
{
    [DefaultEvent("ValueChanged")]
    public class BigKnobs : Control
    {
        public BigKnobs()
        {
            this.SuspendLayout();
            this.MinimumSize = new Size(100, 100);
            this.Size = new Size(100, 100);
            this.ResumeLayout(false);
        }

        private float rtate = 180;
        private float minvalue = 0;
        private float maxvalue = 359f;
        private float span = 359f;
        private float spinrate = 1f;
        private float touchpoint = 0;
        private bool rtating = false;
        private Image bottomImage, midImage, topImage;

        [Category("Behavior")]
        [DefaultValue(180F)]
        public float Value
        {
            get
            {
                return this.rtate * (this.maxvalue - this.minvalue) / this.span + this.minvalue;
            }
            set
            {
                this.rtate = (value - this.minvalue) * this.span / (this.maxvalue - this.minvalue);
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(0F)]
        public float Minimum
        {
            get
            {
                return this.minvalue;
            }
            set
            {
                this.minvalue = (value < this.maxvalue) ? value : this.maxvalue - .1f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(359F)]
        public float Maximum
        {
            get
            {
                return this.maxvalue;
            }
            set
            {
                this.maxvalue = (value > this.minvalue) ? value : this.minvalue + .1f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(359F)]
        public float Span
        {
            get
            {
                return this.span;
            }
            set
            {
                this.span = (value < 360) ? value : 359f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(1F)]
        public float SpinRate
        {
            get
            {
                return this.spinrate;
            }
            set
            {
                this.spinrate = (value < .3f) ? .3f : (value > 2f) ? 2f : value;
                this.Refresh();
            }
        }
        [Category("Appearance")]
        public Image BaseImage
        {
            get
            {
                return this.bottomImage;
            }
            set
            {
                this.bottomImage = value;
                this.Refresh();
            }
        }
        [Category("Appearance")]
        public Image DialImage
        {
            get
            {
                return this.midImage;
            }
            set
            {
                this.midImage = value;
                this.Refresh();
            }
        }
        [Category("Appearance")]
        public Image DisabledDialImage
        {
            get
            {
                return this.topImage;
            }
            set
            {
                this.topImage = value;
                this.Refresh();
            }
        }

        #region Inherited Properties to hide
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new ImeMode ImeMode { get; set; }
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new RightToLeft RightToLeft { get; set; }
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new string Text { get; set; }
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Font Font { get; set; }
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Color ForeColor { get; set; }
        #endregion

        [Category("Action")]
        public event EventHandler ValueChanged;
        protected void OnValueChanged()
        {
            this.ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!this.Focused)
            {
                Focus();
            }

            this.rtating = true;
            this.touchpoint = (float)Math.Atan2(e.Y - this.ClientRectangle.Height / 2f, e.X - this.ClientRectangle.Width / 2f) * 180f / (float)Math.PI + 180f;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!this.rtating)
            {
                return;
            }

            this.rtating = false;
            OnValueChanged();
            this.Refresh();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!this.rtating)
            {
                return;
            }

            float movepoint = (float)Math.Atan2(e.Y - this.ClientRectangle.Height / 2f, e.X - this.ClientRectangle.Width / 2f) * 180f / (float)Math.PI + 180f;

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                movepoint = movepoint.ConstrainToInterval(15);
                this.rtate = this.rtate.ConstrainToInterval(15);
            }
            else if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                movepoint = movepoint.ConstrainToInterval(5);
                this.rtate = this.rtate.ConstrainToInterval(5);
            }

            float travel = (movepoint < this.touchpoint) ? (movepoint + 360f - this.touchpoint) : movepoint - this.touchpoint;

            travel = (travel > 180) ? travel - 360f : travel;
            travel *= this.spinrate;
            this.rtate += travel;

            this.rtate = (this.rtate > this.span) ? this.rtate - this.span : (this.rtate < 0) ? this.rtate + this.span : this.rtate;

            this.touchpoint = movepoint;
            OnValueChanged();
            this.Refresh();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                this.rtate += Math.Sign(e.Delta) * 15;
                this.rtate = this.rtate.ConstrainToInterval(15);
            }
            else if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                this.rtate += Math.Sign(e.Delta) * 5;
                this.rtate = this.rtate.ConstrainToInterval(5);
            }
            else
            {
                this.rtate += Math.Sign(e.Delta) * 5;
            }

            this.rtate = (this.rtate > this.span) ? this.rtate - this.span : (this.rtate < 0) ? this.rtate + this.span : this.rtate;

            OnValueChanged();
            this.Refresh();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rotRect = this.ClientRectangle;
            e.Graphics.CompositingMode = CompositingMode.SourceOver;
            if (this.bottomImage != null)
            {
                e.Graphics.DrawImage(this.bottomImage, rotRect);
            }
            else
            {
                using (SolidBrush backBrush = new SolidBrush(this.BackColor))
                {
                    e.Graphics.FillRectangle(backBrush, rotRect);
                }
            }

            if (this.midImage == null)
            {
                base.OnPaint(e);
                return;
            }

            e.Graphics.TranslateTransform(rotRect.Width / 2f, rotRect.Height / 2f);
            e.Graphics.RotateTransform(this.rtate);
            e.Graphics.TranslateTransform(rotRect.Width / -2f, rotRect.Height / -2f);

            e.Graphics.DrawImage(this.Enabled ? this.midImage : this.topImage ?? this.midImage, rotRect);
            e.Graphics.ResetTransform();

            base.OnPaint(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Left)
            {
                this.rtate--;
            }
            else if (e.KeyCode == Keys.Right)
            {
                this.rtate++;
            }
            else
            {
                return;
            }

            this.rtate = (this.rtate > this.span) ? this.rtate - this.span : (this.rtate < 0) ? this.rtate + this.span : this.rtate;
            OnValueChanged();
            this.Refresh();
        }
    }
}
