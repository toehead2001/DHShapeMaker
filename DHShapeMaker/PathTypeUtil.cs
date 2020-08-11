using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShapeMaker
{
    internal static class PathTypeUtil
    {
        private static readonly IReadOnlyList<string> pathNames = new string[]
        {
            "Straight Lines",
            "Ellipse",
            "Cubic Beziers",
            "Smooth Cubic Beziers",
            "Quadratic Beziers",
            "Smooth Quadratic Beziers"
        };

        private static readonly IReadOnlyList<Color> pathColors = new Color[]
        {
            Color.Black,
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.DarkGoldenrod,
            Color.Purple
        };

        private static readonly IReadOnlyList<Color> lightPathColors = new Color[]
        {
            Color.FromArgb(204, 204, 204),
            Color.FromArgb(255, 204, 204),
            Color.FromArgb(204, 204, 255),
            Color.FromArgb(204, 230, 204),
            Color.FromArgb(241, 231, 206),
            Color.FromArgb(230, 204, 230)
        };

        internal static string GetName(PathType pathType)
        {
            if (pathType == PathType.None)
            {
                throw new ArgumentException($"PathType can't be {nameof(PathType)}.{nameof(PathType.None)}.", nameof(pathType));
            }

            return pathNames[(int)pathType];
        }

        internal static Color GetColor(PathType pathType)
        {
            if (pathType == PathType.None)
            {
                throw new ArgumentException($"PathType can't be {nameof(PathType)}.{nameof(PathType.None)}.", nameof(pathType));
            }

            return pathColors[(int)pathType];
        }

        internal static Color GetLightColor(PathType pathType)
        {
            if (pathType == PathType.None)
            {
                throw new ArgumentException($"PathType can't be {nameof(PathType)}.{nameof(PathType.None)}.", nameof(pathType));
            }

            return lightPathColors[(int)pathType];
        }
    }
}
