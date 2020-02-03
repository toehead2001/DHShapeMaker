using System.IO;
using System.Text;

namespace ShapeMaker
{
    internal sealed class StringWriterWithEncoding : StringWriter
    {
        public override Encoding Encoding { get; }

        internal StringWriterWithEncoding()
            : this(Encoding.UTF8)
        {
        }

        internal StringWriterWithEncoding(Encoding encoding)
        {
            this.Encoding = encoding;
        }
    }
}
