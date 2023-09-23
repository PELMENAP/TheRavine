using UnityEngine;

namespace Modern2D
{

    public static class Boundsxtension
    {
        public static string ToStringPlus(this Bounds bounds)
        {
            return "extends : " + bounds.extents.ToString() + " pos : " + bounds.center.ToString();

        }

        public static bool ContainBounds(this Bounds bounds, Bounds target)
        {
            return bounds.Contains(target.min) && bounds.Contains(target.max);
        }

        public static bool Intersects2D(this Bounds bounds, Bounds target)
        {
            bounds.extents = new Vector3(bounds.extents.x, bounds.extents.y, 100000000);
            return bounds.Intersects(target);
        }
    }

}