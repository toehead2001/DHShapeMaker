using System;

namespace ShapeMaker
{
    [Flags]
    internal enum DrawModes
    {
        None = 0,
        Stroke = 1,
        Fill = 2,
        Fit = 4
    }
}
