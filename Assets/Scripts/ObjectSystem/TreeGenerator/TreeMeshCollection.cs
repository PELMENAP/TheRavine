using UnityEngine;

[CreateAssetMenu(fileName = "TreeMeshCollection", menuName = "Trees/Mesh Collection")]
public class TreeMeshCollection : ScriptableObject
{

    [SerializeField] private Mesh[] _entries;

    public Mesh GetMesh(int seed) => _entries[Mathf.Abs(seed) % _entries.Length];
    public int Count => _entries.Length;

#if UNITY_EDITOR
    public void SetEntries(Mesh[] entries) => _entries = entries;
#endif
}