using System;
using System.Windows.Forms;

namespace ShapeMaker
{
    public partial class Shortcuts : Form
    {
        public Shortcuts()
        {
            InitializeComponent();
        }

        private void Shortcuts_Load(object sender, EventArgs e)
        {
            rt3.Rtf = Properties.Resources.Keyboard;
            rt1.Rtf = Properties.Resources.Mouse;
            rt2.Rtf = Properties.Resources.Misc;
        }
    }
}
