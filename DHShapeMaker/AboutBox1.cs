using System.Reflection;
using System.Windows.Forms;

namespace ShapeMaker
{
    partial class AboutBox1 : Form
    {
        public AboutBox1()
        {
            InitializeComponent();
            this.labelVersion.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version}";
        }
    }
}
