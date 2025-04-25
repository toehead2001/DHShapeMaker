using System.Collections.Generic;
using System.ComponentModel;
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color Color
        {
            get => pdnColor1.Color;
            set => pdnColor1.Color = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal IReadOnlyList<Color> PaletteColors
        {
            get => pdnColor1.PaletteColors;
            set => pdnColor1.PaletteColors = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool ShowAlpha
        {
            get => pdnColor1.ShowAlpha;
            set => pdnColor1.ShowAlpha = value;
        }
    }
}
