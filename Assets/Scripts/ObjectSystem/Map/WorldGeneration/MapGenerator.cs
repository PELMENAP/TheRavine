using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Extentions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    public class MapGenerator : MonoBehaviour, ISetAble
    {
        public const byte mapChunkSize = 16, chunkCount = 3, scale = 5, generationSize = scale * mapChunkSize, waterLevel = 1;
        public static Vector2 vectorOffset = new Vector2(generationSize, generationSize) * chunkCount / 2;
        private Dictionary<Vector2, ChunkData> mapData = new Dictionary<Vector2, ChunkData>(128);
        public ChunkData GetMapData(Vector2 position)
        {
            if (!mapData.ContainsKey(position))
                mapData[position] = GenerateMapData(position);
            return mapData[position];
        }

        public byte GetMapHeight(Vector2 position)
        {
            Vector2 playerPos = new Vector2((int)position.x, (int)position.y) + vectorOffset;
            Vector2 chunkPos = GetChunkPosition(playerPos);
            Vector2 XYpos = (playerPos - chunkPos * generationSize) / scale;
            if (XYpos.x > 15)
                XYpos.x = 15;
            if (XYpos.y > 15)
                XYpos.y = 15;
            if (XYpos.x < 0)
                XYpos.x = 0;
            if (XYpos.y < 0)
                XYpos.y = 0;
            return GetMapData(chunkPos).heightMap[(int)XYpos.x, (int)XYpos.y];
        }

        public Vector2 GetChunkPosition(Vector2 position)
        {
            Vector2 chunkPos = position / generationSize;
            if (position.x < 0)
                chunkPos.x = -(-position.x / generationSize) - 1;
            if (position.y < 0)
                chunkPos.y = -(-position.y / generationSize) - 1;
            return new Vector2((int)chunkPos.x, (int)chunkPos.y);
        }

        public bool TryToAddPositionToChunk(Vector2 position)
        {
            SortedSet<Vector2> objectsToInst = GetMapData(GetChunkPosition(position + vectorOffset)).objectsToInst;
            if (objectsToInst.Contains(position))
                return false;
            else
            {
                objectsToInst.Add(position);
                return true;
            }
        }
        public Transform terrainT, waterT;
        public MeshFilter terrainF, waterF;
        public int seed;
        private ObjectSystem objectSystem;
        [SerializeField] private float noiseScale;
        [SerializeField] private byte octaves;
        [SerializeField, Range(0, 1)] private float persistance;
        [SerializeField] private float lacunarity;
        [SerializeField] private TerrainType[] regions;
        private Transform viewer;
        [SerializeField] private bool[] endlessFlag;
        private List<IEndless> endless = new List<IEndless>(3);
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            viewer = locator.GetPlayerTransform();
            objectSystem = locator.GetService<ObjectSystem>();
            DayCycle.newDay += UpdateNAL;
            if (endlessFlag[0])
                endless.Add(new EndlessTerrain(this));
            if (endlessFlag[1])
                endless.Add(new EndlessLiquids(this));
            if (endlessFlag[2])
                endless.Add(new EndlessObjects(this, objectSystem));
            for (sbyte i = -5; i < 5; i++)
            {
                for (sbyte j = -5; j < 5; j++)
                {
                    Vector2 centre = new Vector2(i, j);
                    mapData[centre] = GenerateMapData(centre);
                }
            }
            position = GetPlayerPosition();
            foreach (var generator in endless)
            {
                generator.UpdateChunk(position);
            }
            GenerationUpdate().Forget();
            if (endlessFlag[3])
                NAL().Forget();
            callback?.Invoke();
        }

        private Queue<Vector2> NALQueue = new Queue<Vector2>(64);
        private Queue<Vector2> NALQueueUpdate = new Queue<Vector2>(32);
        public void ClearNALQueue() => NALQueue.Clear();
        public void AddNALObject(Vector2 current) => NALQueue.Enqueue(current);
        public byte step, deadChance = 0;
        private async UniTaskVoid NAL()
        {
            await UniTask.Delay(10000);
            bool NALthread = true;
            int countCycle = 0;
            while (NALthread)
            {
                countCycle++;
                if (NALQueue.Count > 100)
                {
                    if (countCycle % (step * step) != 0)
                    {
                        await UniTask.Delay(10);
                        continue;
                    }
                }
                else if (countCycle % step != 0 || NALQueue.Count == 0)
                {
                    await UniTask.Delay(10);
                    continue;
                }
                deadChance = NALQueue.Count > 200 ? (byte)10 : (byte)0;
                Vector2 current = NALQueue.Dequeue();
                ObjectInstInfo instInfo = objectSystem.GetGlobalObjectInfo(current);
                if (instInfo.name == null)
                    continue;
                if (instInfo.prefabID == 0)
                {
                    print(instInfo.name);
                    print(instInfo.name == null);
                    print(instInfo.prefabID);
                    print(instInfo.amount);
                    print(instInfo.objectType);
                    continue;
                }
                ObjectInfo currentObjectPrefabData = objectSystem.GetPrefabInfo(instInfo.prefabID);
                ObjectInfo nextGenInfo = currentObjectPrefabData.nextStep;
                if (currentObjectPrefabData.bType == BehaviourType.GROW)
                {
                    if (nextGenInfo == null)
                    {
                        if (Random.Range(0, 4) < 1)
                            objectSystem.RemoveFromGlobal(current);
                        continue;
                    }
                    objectSystem.RemoveFromGlobal(current);
                    if (objectSystem.TryAddToGlobal(current, nextGenInfo.prefab.GetInstanceID(), nextGenInfo.title, nextGenInfo.amount, nextGenInfo.iType, (current.x + current.y) % 2 == 0))
                        NALQueueUpdate.Enqueue(current);
                    continue;
                }
                // ObjectInfo childObjectInfo = objectSystem.GetPrefabInfo(currentObjectInfo.childPrefab.GetInstanceID());
                bool closeto = false;
                for (sbyte x = -1; x <= 1; x++)
                    for (sbyte y = -1; y <= 1; y++)
                        if (x != 0 && y != 0 && objectSystem.ContainsGlobal(current + new Vector2(x, y)))
                            closeto = true;
                NAlInfo nalinfo = currentObjectPrefabData.nalinfo;
                if (Random.Range(0, 100) < (nalinfo.chance / 2 + deadChance) || closeto)
                {
                    objectSystem.RemoveFromGlobal(current);
                    SpreadPattern pattern = currentObjectPrefabData.deadPattern;
                    if (pattern == null)
                        continue;
                    if (objectSystem.TryAddToGlobal(current, pattern.main.prefab.GetInstanceID(), pattern.main.title, pattern.main.amount, pattern.main.iType, (current.x + current.y) % 2 == 0))
                        NALQueueUpdate.Enqueue(current);
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2 newPos = Extention.GenerateRandomPointAround(current, pattern.minDis, pattern.maxDis);
                            if (objectSystem.TryAddToGlobal(newPos, pattern.other[i].prefab.GetInstanceID(), pattern.other[i].title, pattern.other[i].amount, pattern.other[i].iType, Extention.newx < current.x))
                                NALQueueUpdate.Enqueue(newPos);
                        }
                    }
                    continue;
                }
                else
                {
                    byte attempts = nalinfo.attempt;
                    while (attempts > 0)
                    {
                        if (Random.Range(0, 100) < nalinfo.chance)
                        {
                            int xpos = (int)current.x, ypos = (int)current.y;
                            Vector2 newPos = new Vector2(Random.Range(xpos - nalinfo.distance, xpos + nalinfo.distance), Random.Range(ypos - nalinfo.distance, ypos + nalinfo.distance));
                            if (objectSystem.TryAddToGlobal(newPos, nextGenInfo.prefab.GetInstanceID(), nextGenInfo.title, nextGenInfo.amount, nextGenInfo.iType, (newPos.x + newPos.y) % 2 == 0))
                                NALQueueUpdate.Enqueue(newPos);
                        }
                        //await UniTask.Delay(100);
                        attempts--;
                    }
                }
                await UniTask.Delay(10 * nalinfo.delay); // nalinfo * 100
            }
        }

        public void UpdateNAL()
        {
            if (rotateTarget != 0f)
                return;
            ClearNALQueue();
            while (NALQueueUpdate.Count > 0)
            {
                Vector2 item = NALQueueUpdate.Dequeue();
                byte height = GetMapHeight(item);
                if (height < 3 || height > 7)
                    continue;
                GetMapData(GetChunkPosition(item + vectorOffset)).objectsToInst.Add(item);
            }
            ExtraUpdate();
        }
        private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
        // private int[] countOfHeights = new int[9];
        bool isEqual;
        float currentHeight;
        public ChunkData GenerateMapData(Vector2 centre)
        {
            SortedSet<Vector2> objectsToInst = new SortedSet<Vector2>(new Vector2Comparer());
            byte[,] heightMap = new byte[mapChunkSize, mapChunkSize];
            if (centre.x > 10000 || centre.y > 10000)
                Noise.GenerateNoiseMap(ref noiseMap, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Local);
            else
                Noise.GenerateNoiseMap(ref noiseMap, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Global);
            isEqual = true;
            for (byte x = 0; x < mapChunkSize; x++)
            {
                for (byte y = 0; y < mapChunkSize; y++)
                {
                    currentHeight = noiseMap[x, y];
                    for (byte i = 0; i < regions.Length; i++)
                    {
                        if (currentHeight >= regions[i].height)
                            heightMap[(mapChunkSize - x - 1), (mapChunkSize - y - 1)] = i;
                        else
                            break;
                    }
                    if (heightMap[x, y] != heightMap[0, 0])
                        isEqual = false;
                    if (!endlessFlag[2])
                        continue;
                    bool structHere = false;
                    for (byte i = 0; i < regions[heightMap[x, y]].structs.Length; i++)
                    {
                        StructInfoGeneration sinfo = regions[heightMap[x, y]].structs[i];
                        if ((x * y + (byte)centre.x * centre.y + seed) % sinfo.Chance == 0)
                        {
                            Vector2 posstruct = new Vector2(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale) - vectorOffset;
                            WFCA(posstruct, (byte)((seed + (int)x + (int)y) % sinfo.info.tileInfo.Length), sinfo.info);
                            foreach (var item in WFCAobjects)
                            {
                                if (objectSystem.TryAddToGlobal(item.Key, item.Value.prefab.GetInstanceID(), item.Value.title, item.Value.amount, item.Value.iType, (x + y) % 2 == 0))
                                    objectsToInst.Add(item.Key);
                            }
                            structHere = true;
                            break;
                        }
                    }
                    if (structHere)
                        continue;
                    for (byte i = 0; i < regions[heightMap[x, y]].objects.Length; i++)
                    {
                        ObjectInfoGeneration ginfo = regions[heightMap[x, y]].objects[i];
                        if ((x * y + (byte)centre.x * centre.y + seed % 100) % ginfo.Chance == 0)
                        {
                            Vector2 posobj = new Vector2(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale) - vectorOffset;
                            if (objectSystem.TryAddToGlobal(posobj, ginfo.info.prefab.GetInstanceID(), ginfo.info.title, ginfo.info.amount, ginfo.info.iType, (x + y) % 2 == 0))
                            {
                                objectsToInst.Add(posobj);
                                break;
                            }
                        }
                    }
                }
            }
            return new ChunkData(heightMap, isEqual, objectsToInst);
        }

        private Dictionary<Vector2, ObjectInfo> WFCAobjects = new Dictionary<Vector2, ObjectInfo>(8);
        private Queue<Pair<Vector2, byte>> WFCAqueue = new Queue<Pair<Vector2, byte>>(16);
        private void WFCA(Vector2 curPos, byte type, StructInfo structInfo)
        {
            WFCAobjects.Clear();
            WFCAqueue.Clear();
            byte maxIteration = 0, count = 0;
            for (byte i = 0; i < structInfo.tileInfo.Length; i++)
                maxIteration += structInfo.tileInfo[i].MCount;
            byte[] Count = new byte[9];
            count++;
            WFCAqueue.Enqueue(new Pair<Vector2, byte>(curPos, type));
            while (WFCAqueue.Count != 0)
            {
                Pair<Vector2, byte> current = WFCAqueue.Dequeue();
                if (count > maxIteration)
                    break;
                if (structInfo.tileInfo[current.Second].MCount > Count[current.Second] && !WFCAobjects.ContainsKey(current.First))
                {
                    WFCAobjects[current.First] = structInfo.tileInfo[current.Second].objectInfo;
                    Count[current.Second]++;
                    count++;
                }
                byte c = 0;
                for (sbyte x = -1; x <= 1; x++)
                {
                    for (sbyte y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;
                        Vector2 newPos = current.First + new Vector2(x, y) * structInfo.distortion;
                        byte field = structInfo.tileInfo[current.Second].neight[c++];
                        if (field == 0)
                            continue;
                        if (WFCAobjects.ContainsKey(newPos))
                            continue;
                        WFCAqueue.Enqueue(new Pair<Vector2, byte>(newPos, --field));
                    }
                }
            }
        }
        private Vector2 OldVposition, position;
        private async UniTaskVoid GenerationUpdate()
        {
            while (!DataStorage.sceneClose)
            {
                position = GetPlayerPosition();
                if (position != OldVposition && rotateTarget == 0f)
                {
                    OldVposition = position;
                    foreach (var generator in endless)
                    {
                        await UniTask.WaitForFixedUpdate();
                        generator.UpdateChunk(position);
                    }
                }
                await UniTask.Delay(1000);
            }
        }

        public void ExtraUpdate()
        {
            if (rotateTarget != 0f)
                return;
            position = GetPlayerPosition();
            endless[2].UpdateChunk(position);
        }

        private Vector2 GetPlayerPosition() => Extention.RoundVector2D(viewer.position / (scale * mapChunkSize));
        public void RotateBasis(sbyte angle)
        {
            if (rotateValue != 0f)
                return;
            if (angle == 90)
            {
                if (rotateTarget != 0f)
                    return;
                RotationCome().Forget();
            }
            else if (angle == -90)
            {
                if (rotateTarget != 270f)
                    return;
                RotationBack().Forget();
            }
        }
        public float rotateValue = 0f, rotateTarget = 0f;
        private static EnumerableSnapshot<int> objectsSnapshot;
        private Dictionary<int, ushort> objectUpdate = new Dictionary<int, ushort>(16);
        List<Vector2> chunkCoord = new List<Vector2>();
        private async UniTaskVoid RotationCome()
        {
            rotateValue = 0.1f;
            rotateTarget = 270f;
            Vector2 Vposition = GetPlayerPosition();
            ObjectInfo[] prefabInfo = objectSystem._info;
            for (int i = 0; i < prefabInfo.Length; i++)
                objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                    chunkCoord.Add(new Vector2(Vposition.x + xOffset, Vposition.y + yOffset));
            do
            {
                foreach (var vector in chunkCoord)
                {
                    foreach (var item in GetMapData(vector).objectsToInst)
                    {
                        ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
                        if (info.prefabID == 0)
                            continue;
                        objectUpdate[info.prefabID]++;
                        objectSystem.Reuse(info.prefabID, item, info.flip, rotateValue);
                    }
                }
                objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
                foreach (var ID in objectsSnapshot)
                {
                    for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                        objectSystem.Deactivate(ID);
                    objectUpdate[ID] = 0;
                }
                objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
                viewer.Rotate(0, 0, rotateValue, Space.Self);
                await UniTask.WaitForFixedUpdate();
            } while (objectSystem.transform.eulerAngles.z > rotateTarget);
            rotateValue = 0f;
            await UniTask.WaitForFixedUpdate();
        }

        private async UniTaskVoid RotationBack()
        {
            rotateValue = -0.1f;
            rotateTarget = 360f;
            Vector2 Vposition = GetPlayerPosition();
            ObjectInfo[] prefabInfo = objectSystem._info;
            for (int i = 0; i < prefabInfo.Length; i++)
                objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
            do
            {
                foreach (var vector in chunkCoord)
                {
                    foreach (var item in GetMapData(vector).objectsToInst)
                    {
                        ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
                        if (info.prefabID == 0)
                            continue;
                        objectUpdate[info.prefabID]++;
                        objectSystem.Reuse(info.prefabID, item, info.flip, rotateValue);
                    }
                }
                objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
                foreach (var ID in objectsSnapshot)
                {
                    for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                        objectSystem.Deactivate(ID);
                    objectUpdate[ID] = 0;
                }
                objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
                viewer.Rotate(0, 0, rotateValue, Space.Self);
                await UniTask.WaitForFixedUpdate();
            } while ((int)objectSystem.transform.eulerAngles.z != 0);
            rotateValue = 0f;
            rotateTarget = 0f;
            await UniTask.WaitForFixedUpdate();
        }

        public void BreakUp()
        {
            endless.Clear();
            NALQueue.Clear();
            NALQueueUpdate.Clear();
            WFCAobjects.Clear();
            WFCAqueue.Clear();
            mapData.Clear();
            DayCycle.newDay -= UpdateNAL;
        }

        public Transform viewerTest;
        public void TestGeneration()
        {
            if (endlessFlag[0])
                endless.Add(new EndlessTerrain(this));
            if (endlessFlag[1])
                endless.Add(new EndlessLiquids(this));
            endlessFlag[2] = false;
            foreach (var generator in endless)
            {
                generator.UpdateChunk(Extention.RoundVector2D(viewerTest.position / (scale * mapChunkSize)));
            }
        }
    }

    [System.Serializable]
    public struct TerrainType
    {
        public float height;
        public ObjectInfoGeneration[] objects;
        public StructInfoGeneration[] structs;
    }

    [System.Serializable]
    public struct ObjectInfoGeneration
    {
        public byte Chance;
        public ObjectInfo info;
    }

    [System.Serializable]
    public struct StructInfoGeneration
    {
        public byte Chance;
        public StructInfo info;
    }
    public class ChunkData
    {
        public readonly byte[,] heightMap;
        public readonly bool isEqual;
        public SortedSet<Vector2> objectsToInst;
        public ChunkData(byte[,] heightMap, bool isEqual, SortedSet<Vector2> objectsToInst)
        {
            this.heightMap = heightMap;
            this.isEqual = isEqual;
            this.objectsToInst = objectsToInst;
        }
    }

}