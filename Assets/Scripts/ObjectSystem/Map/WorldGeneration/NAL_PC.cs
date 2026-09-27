using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public class NAL_PC
    {
        private readonly ObjectSystem objectSystem;
        private readonly MapGenerator generator;
        private readonly CancellationToken token;

        private readonly Queue<Vector2Int> nalQueue = new(128);
        private readonly HashSet<long> queued = new();
        private readonly Queue<Vector2Int> removeQueue = new(64);
        private readonly Queue<Pair<Vector2Int, ObjectInfo>> addQueue = new(64);

        private readonly int step = 10;
        private int deadChance;
        private int cycle;

        public NAL_PC(MapGenerator generator, ObjectSystem objectSystem, CancellationToken token)
        {
            this.generator = generator;
            this.objectSystem = objectSystem;
            this.token = token;
        }

        public void Enqueue(Vector2Int pos)
        {
            if (queued.Add(Position2Int.Pack(pos)))
                nalQueue.Enqueue(pos);
        }

        public void Clear()
        {
            nalQueue.Clear();
            queued.Clear();
        }

        private Vector2Int Dequeue()
        {
            Vector2Int pos = nalQueue.Dequeue();
            queued.Remove(Position2Int.Pack(pos));
            return pos;
        }

        private void Requeue(Vector2Int pos)
        {
            if (generator.IsInActiveWindow(Position2Int.Pack(pos)))
                Enqueue(pos);
        }

        public async UniTaskVoid RunNAL()
        {
            await UniTask.Delay(10000);

            while (!token.IsCancellationRequested)
            {
                cycle++;

                if (nalQueue.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                    continue;
                }

                bool skip = nalQueue.Count > 100
                            ? cycle % (step * step) != 0
                            : cycle % step != 0;

                if (skip)
                {
                    Requeue(Dequeue());
                    await UniTask.Delay(100, cancellationToken: token);
                    continue;
                }

                deadChance = nalQueue.Count > 200 ? 10 : 0;

                Vector2Int current = Dequeue();
                ProcessNAL(current).Forget();

                await UniTask.Yield();
            }
        }

        private async UniTaskVoid ProcessNAL(Vector2Int current)
        {
            if (!generator.TryGetObject(Position2Int.Pack(current), out ObjectInstInfo instInfo)) return;

            ObjectInfo info = objectSystem.GetInfo(instInfo.PrefabID);
            if (info == null) return;

            ObjectInfo next = info.EvolutionStep;

            if (info.BehaviourType == BehaviourType.GROW)
            {
                if (next == null)
                {
                    if (Extension.ComparePercent(25))
                        removeQueue.Enqueue(current);
                    else
                        Requeue(current);
                    return;
                }

                removeQueue.Enqueue(current);
                addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, next));
                return;
            }

            bool close = false;
            for (int x = -MapGenerator.scale; x <= MapGenerator.scale; x++)
                for (int y = -MapGenerator.scale; y <= MapGenerator.scale; y++)
                    if ((x != 0 || y != 0) && generator.ContainsObject(Position2Int.Pack(current + new Vector2Int(x, y))))
                        close = true;

            NAlInfo n = info.NalInfo;
            if (n == null) return;

            if (Extension.ComparePercent(n.chance / 2 + deadChance) || close)
            {
                removeQueue.Enqueue(current);
                SpreadPattern pattern = info.OnDeathPattern;
                if (pattern != null)
                {
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, pattern.main));
                    foreach (var other in pattern.other)
                    {
                        Vector2Int p = Extension.GetRandomPointAround(current, pattern.factor);
                        addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(p, other));
                    }
                }
                return;
            }

            if (!generator.IsHeightIsLiveAble(Position2Int.Pack(current))) return;

            int attempts = n.attempt;
            while (attempts-- > 0)
            {
                if (next != null && Extension.ComparePercent(n.chance))
                {
                    Vector2Int p = Extension.GetRandomPointAround(current, n.distance);
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(p, next));
                }
                await UniTask.Delay(10, cancellationToken: token);
            }

            await UniTask.Delay(10 * n.delay, cancellationToken: token);
            Requeue(current);
        }

        public async UniTaskVoid RunUpdate()
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(100000, cancellationToken: token);

                while (removeQueue.Count > 0)
                    generator.RemoveObject(Position2Int.Pack(removeQueue.Dequeue()));

                while (addQueue.Count > 0)
                {
                    var item = addQueue.Dequeue();
                    if (item.Second == null) continue;

                    long pos = Position2Int.Pack(item.First);
                    if (generator.TryAddObject(
                            pos,
                            generator.GetRealPosition(pos),
                            item.Second.Id,
                            item.Second.DefaultAmount)
                        && item.Second.HasLifecycle)
                    {
                        Requeue(item.First);
                    }
                }

                generator.ExtraUpdate();
            }
        }
    }
}
