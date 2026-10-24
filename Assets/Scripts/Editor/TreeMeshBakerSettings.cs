
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[FilePath("Library/TreeMeshBakerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class TreeMeshBakerSettings : ScriptableSingleton<TreeMeshBakerSettings>
{
    [SerializeField] private Material[] farMaterials = new Material[TreeMeshCollection.Submeshes];
    [SerializeField] private Material billboardMaterial;

    public Material[] FarMaterials => farMaterials;
    public Material BillboardMaterial => billboardMaterial;

    public void SetFarMaterial(int index, Material material)
    {
        if ((uint)index >= (uint)farMaterials.Length)
            return;

        farMaterials[index] = material;
        Save(true);
    }

    public void SetBillboardMaterial(Material material)
    {
        billboardMaterial = material;
        Save(true);
    }
}
#endif
