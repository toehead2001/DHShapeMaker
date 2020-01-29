using System;

namespace ShapeMaker
{
    [Flags]
    internal enum DrawModes
    {
        Stroke = 1,
        Fill = 2,
        Fit = 4
    }
}
