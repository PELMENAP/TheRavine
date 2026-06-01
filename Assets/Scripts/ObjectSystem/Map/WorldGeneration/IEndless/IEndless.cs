using UnityEngine;
using Cysharp.Threading.Tasks;
public interface IEndless
{
    UniTaskVoid UpdateChunk(Vector2Int position);
}
