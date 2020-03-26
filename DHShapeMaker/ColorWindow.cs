using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ShapeMaker
{
    internal partial class ColorWindow : Form
    {
        internal ColorWindow()
        {
            InitializeComponent();
        }

        internal Color Color
        {
            get => pdnColor1.Color;
            set => pdnColor1.Color = value;
        }

        internal IReadOnlyList<Color> PaletteColors
        {
            get => pdnColor1.PaletteColors;
            set => pdnColor1.PaletteColors = value;
        }

        internal bool ShowAlpha
        {
            get => pdnColor1.ShowAlpha;
            set => pdnColor1.ShowAlpha = value;
        }
    }
}
