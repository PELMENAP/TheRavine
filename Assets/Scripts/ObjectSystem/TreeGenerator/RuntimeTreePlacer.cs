using UnityEngine;

public class RuntimeTreePlacer : MonoBehaviour, IPlaceable
{
    [SerializeField] private TreeMeshCollection _collection;
    [SerializeField] private MeshFilter _filter;

    public void Place(int seed)
    {
        _filter.sharedMesh = _collection.GetMesh(seed);
    }
}