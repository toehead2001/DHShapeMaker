using System;
using System.Drawing;
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

        private void Shortcuts_Paint(object sender, PaintEventArgs e)
        {
            using (Pen p = new Pen(Color.DarkGray))
            {
                p.Width = 4;
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 2, ClientSize.Height - 2);
                p.Dispose();
            }
        }
    }
}
