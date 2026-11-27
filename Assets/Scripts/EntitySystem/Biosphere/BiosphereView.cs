using UnityEngine;
using Unity.Mathematics;
using TheRavine.Generator;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed class BiosphereView : MonoBehaviour
    {
        private const int Batch = 1023;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private float cellScale = 0.8f;
        [SerializeField] private float lift = 0.15f;
        [SerializeField] private Color[] colors =
        {
            new(0.65f, 0.62f, 0.55f), new(0.25f, 0.7f, 0.25f), new(0.45f, 0.3f, 0.15f),
            new(0.85f, 0.2f, 0.25f), new(0.55f, 0.3f, 0.7f),
        };

        private CellBiosphere _biosphere;
        private MapGenerator _map;
        private Matrix4x4[][] _matrices;
        private int[] _counts;
        private MaterialPropertyBlock[] _blocks;

        private void LateUpdate()
        {
            if (mesh == null || material == null) return;
            if (_biosphere == null && !ServiceLocator.Services.TryGet(out _biosphere)) return;
            if (_map == null && !ServiceLocator.Services.TryGet(out _map)) return;
            Ensure(_biosphere.MaxCells);

            for (int t = 0; t < _counts.Length; t++) _counts[t] = 0;
            var scale = new Vector3(cellScale, cellScale, cellScale);
            for (int i = 0; i < _biosphere.CellCount; i++)
            {
                if (!_biosphere.TryGetCell(i, out long key, out CellType type, out _)) break;
                int t = (int)type;
                if ((uint)t >= (uint)_counts.Length) continue;
                float2 c = ChunkFoodIndex.CellCenter(key);
                float h = _map.SampleHeightBilinear(c.x, c.y);
                _matrices[t][_counts[t]++] = Matrix4x4.TRS(new Vector3(c.x, h + lift, c.y), Quaternion.identity, scale);
            }

            for (int t = 0; t < _counts.Length; t++)
            {
                int count = _counts[t];
                if (count == 0) continue;
                var rp = new RenderParams(material) { matProps = _blocks[t] };
                for (int start = 0; start < count; start += Batch)
                    Graphics.RenderMeshInstanced(rp, mesh, 0, _matrices[t], math.min(Batch, count - start), start);
            }
        }

        private void Ensure(int capacity)
        {
            int types = (int)CellType.Count;
            if (_matrices != null && _matrices[0].Length >= capacity) return;
            _matrices = new Matrix4x4[types][];
            _counts = new int[types];
            _blocks = new MaterialPropertyBlock[types];
            for (int t = 0; t < types; t++)
            {
                _matrices[t] = new Matrix4x4[math.max(capacity, 1)];
                _blocks[t] = new MaterialPropertyBlock();
                Color color = colors != null && t < colors.Length ? colors[t] : Color.white;
                _blocks[t].SetColor(BaseColorId, color);
                _blocks[t].SetColor(ColorId, color);
            }
        }
    }
}
