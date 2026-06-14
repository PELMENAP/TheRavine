using UnityEngine;
using Cysharp.Threading.Tasks;
public interface IEndless
{
    UniTaskVoid UpdateChunk(long position);
}
