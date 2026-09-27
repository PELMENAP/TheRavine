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

    public bool UsesBillboard(int ring) => BillboardFromRing > 0 && ring >= BillboardFromRing;
}
