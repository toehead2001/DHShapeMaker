using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ShapeMaker
{
    internal sealed class ThemeRenderer : ToolStripProfessionalRenderer
    {
        internal ThemeRenderer(Color backColor, Color borderColor) : base(new ToolColorTable(backColor, borderColor))
        {
            RoundedEdges = false;
        }

        internal ThemeRenderer() : base(new ThemeColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = PdnTheme.ForeColor;
            base.OnRenderItemText(e);
        }

        private sealed class ToolColorTable : ThemeColorTable
        {
            internal ToolColorTable(Color backColor, Color borderColor)
            {
                BackColor = backColor;
                BorderColor = borderColor;
            }

            private readonly Color BackColor;
            private readonly Color BorderColor;

            public override Color ButtonPressedHighlight => BackColor;
            public override Color ButtonPressedGradientBegin => BackColor;
            public override Color ButtonPressedGradientMiddle => BackColor;
            public override Color ButtonPressedGradientEnd => BackColor;
            public override Color ButtonPressedBorder => BorderColor;
            public override Color ButtonPressedHighlightBorder => BorderColor;

            public override Color ButtonSelectedHighlight => Color.Transparent;
            public override Color ButtonSelectedBorder => BorderColor;
            public override Color ButtonSelectedGradientBegin => Color.Transparent;
            public override Color ButtonSelectedGradientMiddle => Color.Transparent;
            public override Color ButtonSelectedGradientEnd => Color.Transparent;
            public override Color ButtonSelectedHighlightBorder => BorderColor;

            public override Color ButtonCheckedGradientBegin => BackColor;
            public override Color ButtonCheckedGradientMiddle => BackColor;
            public override Color ButtonCheckedGradientEnd => BackColor;
            public override Color ButtonCheckedHighlight => BackColor;
            public override Color ButtonCheckedHighlightBorder => BorderColor;
        }

        private class ThemeColorTable : ProfessionalColorTable
        {
            internal ThemeColorTable()
            {
                UseSystemColors = false;
            }

            private readonly Color ForeColor = PdnTheme.ForeColor;
            private readonly Color BackColor = PdnTheme.BackColor;
            private readonly Color BorderColor = Color.FromArgb(186, 0, 105, 210);
            private readonly Color HiliteColor = Color.FromArgb(62, 0, 103, 206);
            private readonly Color CheckedColor = Color.FromArgb(129, 52, 153, 254);
            private readonly Color CheckedBorderColor = Color.FromArgb(52, 153, 254);

            public override Color ButtonSelectedHighlight => HiliteColor;
            public override Color ButtonSelectedBorder => BorderColor;
            public override Color ButtonSelectedGradientBegin => HiliteColor;
            public override Color ButtonSelectedGradientMiddle => HiliteColor;
            public override Color ButtonSelectedGradientEnd => HiliteColor;
            public override Color ButtonSelectedHighlightBorder => BorderColor;

            public override Color ButtonPressedHighlight => HiliteColor;
            public override Color ButtonPressedGradientBegin => CheckedColor;
            public override Color ButtonPressedGradientMiddle => CheckedColor;
            public override Color ButtonPressedGradientEnd => CheckedColor;
            public override Color ButtonPressedBorder => CheckedBorderColor;
            public override Color ButtonPressedHighlightBorder => CheckedBorderColor;

            public override Color ButtonCheckedGradientBegin => CheckedColor;
            public override Color ButtonCheckedGradientMiddle => CheckedColor;
            public override Color ButtonCheckedGradientEnd => CheckedColor;
            public override Color ButtonCheckedHighlight => CheckedColor;
            public override Color ButtonCheckedHighlightBorder => CheckedBorderColor;

            public override Color ToolStripBorder => BackColor;
            public override Color ToolStripGradientBegin => BackColor;
            public override Color ToolStripGradientMiddle => BackColor;
            public override Color ToolStripGradientEnd => BackColor;
            public override Color ToolStripDropDownBackground => BackColor;

            public override Color MenuItemBorder => BorderColor;
            public override Color MenuItemPressedGradientBegin => BackColor;
            public override Color MenuItemPressedGradientMiddle => BackColor;
            public override Color MenuItemPressedGradientEnd => BackColor;

            public override Color MenuItemSelected => HiliteColor;
            public override Color MenuItemSelectedGradientBegin => HiliteColor;
            public override Color MenuItemSelectedGradientEnd => HiliteColor;

            public override Color CheckBackground => CheckedColor;
            public override Color CheckSelectedBackground => HiliteColor;
            public override Color CheckPressedBackground => CheckedColor;

            public override Color MenuStripGradientBegin => BackColor;
            public override Color MenuStripGradientEnd => BackColor;
            public override Color MenuBorder => Color.Gray;

            public override Color ImageMarginGradientBegin => BackColor;
            public override Color ImageMarginGradientMiddle => BackColor;
            public override Color ImageMarginGradientEnd => BackColor;

            public override Color SeparatorLight => BackColor;

            public override Color StatusStripGradientBegin => BackColor;
            public override Color StatusStripGradientEnd => BackColor;
        }
    }

    public sealed class ToolStripButtonWithKeys : ToolStripButton
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

    internal static class PdnTheme
    {
        private static Color foreColor;
        private static Color backColor;
        private static ThemeRenderer themeRenderer;

        internal static Color ForeColor
        {
            get
            {
                return foreColor;
            }
            set
            {
                foreColor = value;
                themeRenderer = null;
            }
        }
        internal static Color BackColor
        {
            get
            {
                return backColor;
            }
            set
            {
                backColor = value;
                themeRenderer = null;
            }
        }
        internal static ThemeRenderer Renderer
        {
            get
            {
                if (themeRenderer == null)
                {
                    themeRenderer = new ThemeRenderer();
                }

                return themeRenderer;
            }
        }
    }
}
