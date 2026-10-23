using System;
using Celeste.Core.Geometry;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    internal static class ContainerGeometry
    {
        public static IntRect Union(IntRect first, IntRect second)
        {
            int left = Math.Min(first.X, second.X), top = Math.Min(first.Y, second.Y);
            int right = Math.Max(first.Right, second.Right), bottom = Math.Max(first.Bottom, second.Bottom);
            return new(left, top, unchecked(right - left), unchecked(bottom - top));
        }
    }
}
