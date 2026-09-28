using UnityEngine;

public abstract class ObjectLodSet : ScriptableObject
{
    public const int WholeMesh = -1;

    public abstract int VariantCount { get; }
    public abstract int SubmeshCount { get; }
    public abstract int BillboardFromRing { get; }

    public abstract Mesh GetMesh(int variant, int ring);
    public abstract int GetMeshLod(int variant, int ring);
    public abstract Material GetMaterial(int ring, int submesh = 0);

    public virtual Material BillboardMaterial => null;
    public virtual Vector4 GetBillboardBox(int variant) => Vector4.zero;
    public virtual int GetBillboardCell(int variant) => variant;

    public virtual void GetIndexRange(int variant, int lod, int submesh, out uint indexStart, out uint indexCount)
    {
        Mesh mesh = GetMesh(variant, 0);
        indexStart = mesh.GetIndexStart(submesh);
        indexCount = mesh.GetIndexCount(submesh);
    }

    public bool UsesBillboard(int ring) => BillboardFromRing > 0 && ring >= BillboardFromRing && BillboardMaterial != null;
}
