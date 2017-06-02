using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ShapeMaker
{
    internal class ProRenderer : ToolStripProfessionalRenderer
    {
        internal ProRenderer(Color backColor, Color borderColor) : base(new ProColorTable(backColor, borderColor))
        {
            RoundedEdges = false;
        }
    }

    internal class ProColorTable : ProfessionalColorTable
    {
        internal ProColorTable(Color backColor, Color borderColor)
        {
            UseSystemColors = true;
            BackColor = backColor;
            BorderColor = borderColor;
        }

        private readonly Color BackColor;
        private readonly Color BorderColor;

        public override Color ButtonCheckedHighlight => BackColor;
        public override Color ButtonPressedHighlight => BackColor;
        public override Color ButtonSelectedHighlight => Color.Transparent;
        public override Color ButtonSelectedBorder => BorderColor;
        public override Color ToolStripBorder => Color.Transparent;
        public override Color ToolStripGradientBegin => Color.Transparent;
        public override Color ToolStripGradientMiddle => Color.Transparent;
        public override Color ToolStripGradientEnd => Color.Transparent;
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

        [DefaultValue(PathType.None)]
        public PathType PathType { get; set; }

        [Localizable(true), DefaultValue(Keys.None)]
        public Keys ShortcutKeys { get; set; }
    }
}
