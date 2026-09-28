using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TreeMeshCollection", menuName = "Trees/Mesh Collection")]
public class TreeMeshCollection : ObjectLodSet
{
    public const int Submeshes = 2;

    [Serializable]
    public struct IndexRange
    {
        public uint start;
        public uint count;
    }

    [Serializable]
    public class TreeVariant
    {
        public Mesh mesh;
        public IndexRange[] ranges;
        public Vector4 billboardBox;
        public int billboardCell;
    }

    [SerializeField] private TreeVariant[] variants = Array.Empty<TreeVariant>();
    [SerializeField] private int lodCount = 1;
    [SerializeField] private int[] ringLod = { 0, 1, 2, 3, 3 };
    [SerializeField] private int billboardFromRing = 3;
    [SerializeField] private Material[] farMaterials = new Material[Submeshes];
    [SerializeField] private Material billboardMaterial;
    [SerializeField] private Texture2D billboardAlbedo;
    [SerializeField] private Texture2D billboardNormal;
    [SerializeField] private float billboardPitch = 30f;

    [SerializeField, HideInInspector] private Mesh[] _entries;

    public int Count => variants.Length > 0 ? variants.Length : _entries?.Length ?? 0;
    public int LodCount => lodCount;
    public float BillboardPitch => billboardPitch;

    public override int VariantCount => variants.Length;
    public override int SubmeshCount => Submeshes;
    public override int BillboardFromRing => billboardFromRing;
    public override Material BillboardMaterial => billboardMaterial;

    public Mesh GetMesh(int seed)
    {
        int index = seed & int.MaxValue;
        if (variants.Length > 0) return variants[index % variants.Length].mesh;
        return _entries[index % _entries.Length];
    }

    public override Mesh GetMesh(int variant, int ring) => variants[variant].mesh;

    public override int GetMeshLod(int variant, int ring)
    {
        if (ringLod == null || ringLod.Length == 0) return 0;
        int lod = ringLod[Mathf.Clamp(ring, 0, ringLod.Length - 1)];
        return Mathf.Clamp(lod, 0, Mathf.Max(lodCount - 1, 0));
    }

    public override Material GetMaterial(int ring, int submesh = 0) =>
        (uint)submesh < (uint)farMaterials.Length ? farMaterials[submesh] : null;

    public override Vector4 GetBillboardBox(int variant) => variants[variant].billboardBox;

    public override int GetBillboardCell(int variant) => variants[variant].billboardCell;

    public override void GetIndexRange(int variant, int lod, int submesh, out uint indexStart, out uint indexCount)
    {
        IndexRange[] ranges = variants[variant].ranges;
        int slot = lod * Submeshes + submesh;
        if (ranges == null || (uint)slot >= (uint)ranges.Length)
        {
            base.GetIndexRange(variant, lod, submesh, out indexStart, out indexCount);
            return;
        }

        indexStart = ranges[slot].start;
        indexCount = ranges[slot].count;
    }

#if UNITY_EDITOR
    public void SetBakeResult(TreeVariant[] bakedVariants, int bakedLodCount, Texture2D albedo, Texture2D normal, float pitch)
    {
        variants = bakedVariants;
        lodCount = bakedLodCount;
        billboardAlbedo = albedo;
        billboardNormal = normal;
        billboardPitch = pitch;
        _entries = null;
    }

    public void ApplyBillboardMaterial()
    {
        if (billboardMaterial == null) return;
        billboardMaterial.SetTexture("_BillboardAlbedo", billboardAlbedo);
        billboardMaterial.SetTexture("_BillboardNormal", billboardNormal);
        billboardMaterial.SetVector("_AtlasGrid", new Vector4(2f, Mathf.Max(variants.Length, 1), 0f, 0f));
        billboardMaterial.SetFloat("_BillboardPitch", billboardPitch);
    }
#endif
}
