﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShapeMaker
{
    [DefaultEvent("ValueChanged")]
    public partial class BigKnobs : Control
    {
        public BigKnobs()
        {
            InitializeComponent();
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
                return adjustment();
            }
            set
            {
                rtate = (value - minvalue) * span / (maxvalue - minvalue);
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(0F)]
        public float Minimum
        {
            get
            {
                return minvalue;
            }
            set
            {
                minvalue = (value < maxvalue) ? value : maxvalue - .1f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(359F)]
        public float Maximum
        {
            get
            {
                return maxvalue;
            }
            set
            {
                maxvalue = (value > minvalue) ? value : minvalue + .1f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(359F)]
        public float Span
        {
            get
            {
                return span;
            }
            set
            {
                span = (value < 360) ? value : 359f;
                this.Refresh();
            }
        }
        [Category("Behavior")]
        [DefaultValue(1F)]
        public float SpinRate
        {
            get
            {
                return spinrate;
            }
            set
            {
                spinrate = (value < .3f) ? .3f : (value > 2f) ? 2f : value;
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

        public delegate void ValueChangedEventHandler(object sender, float e);
        [Category("Action")]
        public event ValueChangedEventHandler ValueChanged;

        protected void OnValueChanged(float e)
        {
            this.ValueChanged?.Invoke(this, e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Focused) Focus();
            rtating = true;
            touchpoint = (float)Math.Atan2(e.Y - this.ClientRectangle.Height / 2f, e.X - this.ClientRectangle.Width / 2f) * 180f / (float)Math.PI + 180f;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!rtating)
                return;

            rtating = false;
            OnValueChanged(adjustment());
            this.Refresh();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!rtating)
                return;

            float movepoint = (float)Math.Atan2(e.Y - this.ClientRectangle.Height / 2f, e.X - this.ClientRectangle.Width / 2f) * 180f / (float)Math.PI + 180f;

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                movepoint = (float)(Math.Round(movepoint / 15) * 15);
                rtate = (float)(Math.Round(rtate / 15) * 15);
            }
            else if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                movepoint = (float)(Math.Round(movepoint / 5) * 5);
                rtate = (float)(Math.Round(rtate / 5) * 5);
            }

            float travel = (movepoint < touchpoint) ? (movepoint + 360f - touchpoint) : movepoint - touchpoint;

            travel = (travel > 180) ? travel - 360f : travel;
            travel *= spinrate;
            rtate += travel;

            rtate = (rtate > span) ? rtate - span : (rtate < 0) ? rtate + span : rtate;

            touchpoint = movepoint;
            OnValueChanged(adjustment());
            this.Refresh();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                rtate += Math.Sign(e.Delta) * 15;
                rtate = (float)(Math.Round(rtate / 15) * 15);
            }
            else if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                rtate += Math.Sign(e.Delta) * 5;
                rtate = (float)(Math.Round(rtate / 5) * 5);
            }
            else
            {
                rtate += Math.Sign(e.Delta) * 5;
            }

            rtate = (rtate > span) ? rtate - span : (rtate < 0) ? rtate + span : rtate;

            OnValueChanged(adjustment());
            this.Refresh();
        }

        private float adjustment()
        {
            return rtate * (maxvalue - minvalue) / span + minvalue;
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
                    e.Graphics.FillRectangle(backBrush, rotRect);
            }

            if (this.midImage == null)
            {
                base.OnPaint(e);
                return;
            }

            e.Graphics.TranslateTransform(rotRect.Width / 2f, rotRect.Height / 2f);
            e.Graphics.RotateTransform(rtate);
            e.Graphics.TranslateTransform(rotRect.Width / -2f, rotRect.Height / -2f);

            e.Graphics.DrawImage(this.Enabled ? this.midImage : this.topImage ?? this.midImage, rotRect);
            e.Graphics.ResetTransform();

            base.OnPaint(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right)
                return true;

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Left)
            {
                rtate--;
            }
            else if (e.KeyCode == Keys.Right)
            {
                rtate++;
            }
            else
            {
                return;
            }

            rtate = (rtate > span) ? rtate - span : (rtate < 0) ? rtate + span : rtate;
            OnValueChanged(adjustment());
            this.Refresh();
        }

    }
}
