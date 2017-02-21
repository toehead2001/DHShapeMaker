using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ShapeMaker
{
    class ProRenderer : ToolStripProfessionalRenderer
    {
        internal ProRenderer(Color backColor, Color borderColor) : base(new ProColorTable(backColor, borderColor))
        {
            RoundedEdges = false;
        }
    }

    class ProColorTable : ProfessionalColorTable
    {
        internal ProColorTable(Color backColor, Color borderColor)
        {
            UseSystemColors = true;
            BackColor = backColor;
            BorderColor = borderColor;
        }

        private Color BackColor;
        private Color BorderColor;

        public override Color ButtonCheckedHighlight
        {
            get
            {
                return BackColor;
            }
        }
        public override Color ButtonPressedHighlight
        {
            get
            {
                return BackColor;
            }
        }
        public override Color ButtonSelectedHighlight
        {
            get
            {
                return Color.Transparent;
            }
        }
        public override Color ButtonSelectedBorder
        {
            get
            {
                return BorderColor;
            }
        }
        public override Color ToolStripBorder
        {
            get
            {
                return Color.Transparent;
            }
        }
        public override Color ToolStripGradientBegin
        {
            get
            {
                return Color.Transparent;
            }
        }
        public override Color ToolStripGradientMiddle
        {
            get
            {
                return Color.Transparent;
            }
        }
        public override Color ToolStripGradientEnd
        {
            get
            {
                return Color.Transparent;
            }
        }
    }

    public class ToolStripButtonWithKeys : ToolStripButton
    {
        public ToolStripButtonWithKeys()
        {
            Margin = new Padding(0, 1, 2, 2);
        }

        private CheckState checkHolder;

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled)
            {
                checkHolder = CheckState;
                Checked = false;
            }
            else
            {
                CheckState = checkHolder;
            }

            base.OnEnabledChanged(e);
        }

        [Localizable(true), DefaultValue(Keys.None)]
        public Keys ShortcutKeys { get; set; }
    }
}
