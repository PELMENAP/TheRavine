using UnityEngine;


namespace TheRavine.Extensions
{
    public static class GeneratorExtensions
    {
        private static Vector3 specialOffset = new(40, 0, 40);
        public static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(localBounds.center) + specialOffset;
            Vector3 extents = localBounds.extents;
            
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
            
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            
            return new Bounds(center, extents * 2f);
        }
    }
}