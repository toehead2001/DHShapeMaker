using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ShapeMaker
{
    internal sealed class ThemeRenderer : ToolStripProfessionalRenderer
    {
        internal ThemeRenderer(PathType pathType)
            : this(PathTypeUtil.GetLightColor(pathType), PathTypeUtil.GetColor(pathType))
        {
        }

        internal ThemeRenderer(Color backColor, Color borderColor)
            : base(new ToolColorTable(backColor, borderColor))
        {
            this.RoundedEdges = false;
        }

        private sealed class ToolColorTable : ThemeColorTable
        {
            internal ToolColorTable(Color backColor, Color borderColor)
            {
                this.backColor = backColor;
                this.borderColor = borderColor;
            }

            private readonly Color backColor;
            private readonly Color borderColor;

            public override Color ButtonPressedHighlight => this.backColor;
            public override Color ButtonPressedGradientBegin => this.backColor;
            public override Color ButtonPressedGradientMiddle => this.backColor;
            public override Color ButtonPressedGradientEnd => this.backColor;
            public override Color ButtonPressedBorder => this.borderColor;
            public override Color ButtonPressedHighlightBorder => this.borderColor;

            public override Color ButtonSelectedHighlight => Color.Transparent;
            public override Color ButtonSelectedBorder => this.borderColor;
            public override Color ButtonSelectedGradientBegin => Color.Transparent;
            public override Color ButtonSelectedGradientMiddle => Color.Transparent;
            public override Color ButtonSelectedGradientEnd => Color.Transparent;
            public override Color ButtonSelectedHighlightBorder => this.borderColor;

            public override Color ButtonCheckedGradientBegin => this.backColor;
            public override Color ButtonCheckedGradientMiddle => this.backColor;
            public override Color ButtonCheckedGradientEnd => this.backColor;
            public override Color ButtonCheckedHighlight => this.backColor;
            public override Color ButtonCheckedHighlightBorder => this.borderColor;
        }

        private class ThemeColorTable : ProfessionalColorTable
        {
            internal ThemeColorTable()
            {
                this.UseSystemColors = false;
            }

            private readonly Color backColor = Color.Transparent;
            private readonly Color borderColor = Color.FromArgb(186, 0, 105, 210);
            private readonly Color hiliteColor = Color.FromArgb(62, 0, 103, 206);
            private readonly Color checkedColor = Color.FromArgb(129, 52, 153, 254);
            private readonly Color checkedBorderColor = Color.FromArgb(52, 153, 254);

            public override Color ButtonSelectedHighlight => this.hiliteColor;
            public override Color ButtonSelectedBorder => this.borderColor;
            public override Color ButtonSelectedGradientBegin => this.hiliteColor;
            public override Color ButtonSelectedGradientMiddle => this.hiliteColor;
            public override Color ButtonSelectedGradientEnd => this.hiliteColor;
            public override Color ButtonSelectedHighlightBorder => this.borderColor;

            public override Color ButtonPressedHighlight => this.hiliteColor;
            public override Color ButtonPressedGradientBegin => this.checkedColor;
            public override Color ButtonPressedGradientMiddle => this.checkedColor;
            public override Color ButtonPressedGradientEnd => this.checkedColor;
            public override Color ButtonPressedBorder => this.checkedBorderColor;
            public override Color ButtonPressedHighlightBorder => this.checkedBorderColor;

            public override Color ButtonCheckedGradientBegin => this.checkedColor;
            public override Color ButtonCheckedGradientMiddle => this.checkedColor;
            public override Color ButtonCheckedGradientEnd => this.checkedColor;
            public override Color ButtonCheckedHighlight => this.checkedColor;
            public override Color ButtonCheckedHighlightBorder => this.checkedBorderColor;

            public override Color ToolStripBorder => this.backColor;
            public override Color ToolStripGradientBegin => this.backColor;
            public override Color ToolStripGradientMiddle => this.backColor;
            public override Color ToolStripGradientEnd => this.backColor;
            public override Color ToolStripDropDownBackground => this.backColor;

            public override Color MenuItemBorder => this.borderColor;
            public override Color MenuItemPressedGradientBegin => this.backColor;
            public override Color MenuItemPressedGradientMiddle => this.backColor;
            public override Color MenuItemPressedGradientEnd => this.backColor;

            public override Color MenuItemSelected => this.hiliteColor;
            public override Color MenuItemSelectedGradientBegin => this.hiliteColor;
            public override Color MenuItemSelectedGradientEnd => this.hiliteColor;

            public override Color CheckBackground => this.checkedColor;
            public override Color CheckSelectedBackground => this.hiliteColor;
            public override Color CheckPressedBackground => this.checkedColor;

            public override Color MenuStripGradientBegin => this.backColor;
            public override Color MenuStripGradientEnd => this.backColor;
            public override Color MenuBorder => Color.Gray;

            public override Color ImageMarginGradientBegin => this.backColor;
            public override Color ImageMarginGradientMiddle => this.backColor;
            public override Color ImageMarginGradientEnd => this.backColor;

            public override Color SeparatorLight => this.backColor;

            public override Color StatusStripGradientBegin => this.backColor;
            public override Color StatusStripGradientEnd => this.backColor;
        }
    }

    public sealed class ToolStripButtonWithKeys : ToolStripButton
    {
        public ToolStripButtonWithKeys()
        {
            this.Margin = new Padding(0, 1, 2, 2);
        }

        private CheckState checkHolder;

        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!this.Enabled)
            {
                this.checkHolder = this.CheckState;
                this.Checked = false;
            }
            else
            {
                this.CheckState = this.checkHolder;
            }

            base.OnEnabledChanged(e);
        }

        [DefaultValue(PathType.None)]
        public PathType PathType { get; set; }

        [Localizable(true), DefaultValue(Keys.None)]
        public Keys ShortcutKeys { get; set; }
    }
}
