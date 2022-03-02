using System.Collections;

namespace ShapeMaker
{
    public class ArrayListEx : ArrayList
    {
        public ArrayListEx() : base()
        {
        }

        public ArrayListEx(ICollection c) : base(c)
        {
        }

        public ArrayListEx(int capacity) : base(capacity)
        {
        }
    }
}
