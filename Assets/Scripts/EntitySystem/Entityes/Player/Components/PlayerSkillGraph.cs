using System;
using Unity.Mathematics;
using TheRavine.Base;

namespace TheRavine.EntityControl
{
    public enum SkillCommandOp : byte
    {
        Build = 0,
        Prune = 1,
        Restore = 2,
        ToggleAuto = 3,
        Sync = 4,
        Count,
    }

    public sealed class PlayerSkillGraph : IComponent, ISkillGraphSource, ISkillGraphCommands
    {
        public const int Slot = 0;
        public const int GeneCount = 6;
        public const int ForestCount = 4;

        private readonly SkillAtlas _atlas;
        private readonly SkillGraphStore _store;
        private readonly SkillGraphSolver _solver = new();
        private readonly bool _authority;
        private SkillGraphTopology _topology;
        private GeneticParameters _genes;
        private SkillModifiers _modifiers = SkillModifiers.Neutral;
        private bool _modifiersChanged;
        private bool _auto;
        private int _stateVersion;
        private int _sentTopologyVersion;
        private float _lastSweep;
        private float _nextSolve;
        private float _nextReplicate;
        private SkillTopologyPacket _topologyPacket;
        private SkillLevelsPacket _levelsPacket;
        private ushort[] _sentLevels;
        private ushort[] _sentPeaks;
        private byte _sentRoot = byte.MaxValue;
        private Func<SkillCommandOp, int, int, bool> _sender;

        public bool IsAuthority => _authority;
        public SkillAtlas Atlas => _atlas;
        public SkillGraphTopology Topology => _topology;
        public SkillGraphStore Store => _store;
        public bool AutoMode => _auto;
        public int StateVersion => _stateVersion;
        public int Revision => _topology.Revision;
        public ref readonly SkillModifiers Modifiers => ref _modifiers;
        public GeneticParameters Genes => _genes;
        int ISkillGraphSource.StoreIndex => Slot;

        private PlayerSkillGraph(SkillAtlas atlas, bool authority)
        {
            _atlas = atlas;
            _authority = authority;
            _store = new SkillGraphStore(1, atlas.Count);
            _stateVersion = authority ? 1 : 0;
            _topologyPacket = SkillTopologyPacket.Allocate(atlas.Count);
            _levelsPacket = SkillLevelsPacket.Allocate(atlas.Count);
            _sentLevels = new ushort[atlas.Count];
            _sentPeaks = new ushort[atlas.Count];
        }

        public static PlayerSkillGraph Create(SkillAtlas atlas, ref GeneticParameters genes, PlayerSkillSave save, bool authority)
        {
            var graph = new PlayerSkillGraph(atlas, authority);
            if (authority && save != null)
            {
                if (graph.TryRestore(save, ref genes)) return graph;
                RavineLog.Warning("[PlayerSkillGraph] Saved skill graph does not match the atlas: a fresh graph is started");
            }
            if (authority) DrawGenes(ref genes);
            graph._genes = genes;
            graph._topology = SkillGraphTopology.Create(atlas, authority ? SkillGraphTopology.DrawForestGenes() : float4.zero);
            graph.Attach();
            return graph;
        }

        public static void DrawGenes(ref GeneticParameters genes)
        {
            var span = GeneticParameters.AsSpan(ref genes);
            for (int i = GeneticParameters.IdxSkillOriginX; i <= GeneticParameters.IdxPlastNeed; i++)
            {
                var (min, max, _) = GeneticParameters.ParameterRanges[i];
                span[i] = RavineRandom.RangeFloat(min, max);
            }
        }

        private void Attach()
        {
            _store.Reset(Slot, in _genes, _topology, _atlas);
            _store.SetColony(Slot, 0);
            _store.PublishColony(0, _topology);
        }

        public void Tick(float now)
        {
            if (!_authority) return;
            ref readonly var f = ref SimulationRules.Frame;
            if (now - _lastSweep >= f.SkillSweepInterval)
            {
                _lastSweep = now;
                _store.ScheduleDecay(Slot, 1, 1, now, f.SkillDecayRate, f.SkillSavingsFloor).Complete();
            }
            if (_auto && now >= _nextSolve)
            {
                _nextSolve = now + SimulationRules.Active.SkillSolverInterval;
                RefreshPhi();
                if (_solver.StepSingle(_topology, 0, Slot, _store, _atlas, SimulationRules.Active) > 0) _stateVersion++;
            }
        }

        public void Award(EntityAction action, float now)
        {
            if (!_authority) return;
            SkillGraphXp.Award(_store, Slot, (int)action, now, _topology, _atlas, in SimulationRules.Frame);
        }

        public void Refresh()
        {
            if (!_store.IsDirty(Slot)) return;
            _store.ClearDirty(Slot);
            _modifiers = SkillModifiers.From(_store, Slot, _topology, _atlas);
            _modifiersChanged = true;
        }

        public bool ConsumeModifiersChanged()
        {
            bool changed = _modifiersChanged;
            _modifiersChanged = false;
            return changed;
        }

        private void RefreshPhi()
        {
            var peaks = _store.Peaks;
            _topology.UpdatePhi(SkillGraphMath.Phi(in peaks, 0, _atlas.Count, in SimulationRules.Frame));
        }

        public void BindCommandSender(Func<SkillCommandOp, int, int, bool> sender) => _sender = sender;

        public bool Build(int u, int v) => Send(SkillCommandOp.Build, u, v);

        public bool Prune(int u, int v) => Send(SkillCommandOp.Prune, u, v);

        public bool Restore(int u, int v) => Send(SkillCommandOp.Restore, u, v);

        public bool ToggleAutoMode() => Send(SkillCommandOp.ToggleAuto, 0, 0);

        private bool Send(SkillCommandOp op, int u, int v)
        {
            if (_sender != null) return _sender(op, u, v);
            return _authority && TryExecute(op, u, v, SimulationClock.Time);
        }

        public bool IsOpen(int skill) => SkillGraphMath.IsOpen(_store.Peak(Slot, skill), skill, _store.Root(Slot));

        public bool CanBuild(int u, int v)
            => CanExecute(SkillCommandOp.Build, u, v);

        public bool CanPrune(int u, int v)
            => CanExecute(SkillCommandOp.Prune, u, v);

        public bool CanRestore(int u, int v)
            => CanExecute(SkillCommandOp.Restore, u, v);

        public bool CanExecute(SkillCommandOp op, int u, int v)
        {
            if ((uint)op >= (uint)SkillCommandOp.Count) return false;
            if (op == SkillCommandOp.ToggleAuto || op == SkillCommandOp.Sync) return true;
            if ((uint)u >= (uint)_atlas.Count || (uint)v >= (uint)_atlas.Count || u == v) return false;
            switch (op)
            {
                case SkillCommandOp.Build:
                    if (!_atlas.HasPotentialEdge(u, v) || (!IsOpen(u) && !IsOpen(v))) return false;
                    if (_authority) RefreshPhi();
                    return _topology.CanBuild(u, v);
                case SkillCommandOp.Prune:
                    return _topology.CanPrune(u, v, _store.Root(Slot));
                default:
                    if (_authority) RefreshPhi();
                    return _topology.CanRestore(u, v);
            }
        }

        public bool TryExecute(SkillCommandOp op, int u, int v, float now)
        {
            if (!_authority || !CanExecute(op, u, v)) return false;
            switch (op)
            {
                case SkillCommandOp.ToggleAuto:
                    _auto = !_auto;
                    _nextSolve = 0f;
                    _stateVersion++;
                    return true;
                case SkillCommandOp.Sync:
                    _sentTopologyVersion = 0;
                    return true;
            }

            _store.Materialize(Slot, now, _topology, in SimulationRules.Frame);
            ulong prunedBefore = _topology.PrunedMask;
            bool ok = op == SkillCommandOp.Build ? _topology.TryBuild(u, v)
                : op == SkillCommandOp.Prune ? _topology.TryPrune(u, v, _store.Root(Slot))
                : _topology.TryRestore(u, v);
            if (!ok) return false;

            if (op == SkillCommandOp.Restore) _store.Thaw(Slot, prunedBefore & ~_topology.PrunedMask, now);
            _store.PublishColony(0, _topology);
            _store.MarkDirty(Slot);
            _stateVersion++;
            return true;
        }

        public bool TryTakeTopology(out SkillTopologyPacket packet)
        {
            packet = default;
            if (!_authority || _stateVersion == _sentTopologyVersion) return false;
            _sentTopologyVersion = _stateVersion;
            _topologyPacket.Capture(_topology, _stateVersion, _auto);
            _topologyPacket.Origin = new float2(_genes.SkillOriginX, _genes.SkillOriginY);
            _topologyPacket.Plast = new float4(_genes.PlastHebb, _genes.PlastStruct, _genes.PlastExplore, _genes.PlastNeed);
            packet = _topologyPacket;
            return true;
        }

        public bool TryTakeLevels(float now, out SkillLevelsPacket packet)
        {
            packet = default;
            if (!_authority || now < _nextReplicate) return false;
            _nextReplicate = now + SimulationRules.Active.SkillReplicateInterval;
            _levelsPacket.Capture(_store, Slot, _stateVersion, SimulationRules.Active.SkillLevelFixedScale);

            bool changed = _levelsPacket.Root != _sentRoot;
            for (int v = 0; v < _sentLevels.Length; v++)
            {
                changed |= _sentLevels[v] != _levelsPacket.Levels[v] || _sentPeaks[v] != _levelsPacket.Peaks[v];
                _sentLevels[v] = _levelsPacket.Levels[v];
                _sentPeaks[v] = _levelsPacket.Peaks[v];
            }
            _sentRoot = _levelsPacket.Root;
            if (!changed) return false;
            packet = _levelsPacket;
            return true;
        }

        public bool IsStale(in SkillLevelsPacket packet) => packet.Version != _stateVersion;

        public bool ApplyTopology(in SkillTopologyPacket packet)
        {
            if (_authority || packet.Count != _atlas.Count) return false;
            if (!math.all(math.isfinite(packet.Forest)) || !math.all(math.isfinite(packet.Origin)) || !math.all(math.isfinite(packet.Plast))) return false;

            var forest = _topology.ForestGenes;
            SkillGraphTopology next = _topology;
            if (!forest.Equals(packet.Forest))
                next = SkillGraphTopology.Create(_atlas, packet.Forest);
            if (!next.TryApplyState(in packet, true))
            {
                if (!ReferenceEquals(next, _topology)) next.Dispose();
                return false;
            }
            if (!ReferenceEquals(next, _topology))
            {
                _topology.Dispose();
                _topology = next;
            }

            _genes.SkillOriginX = math.saturate(packet.Origin.x);
            _genes.SkillOriginY = math.saturate(packet.Origin.y);
            _genes.PlastHebb = math.clamp(packet.Plast.x, 0f, 2f);
            _genes.PlastStruct = math.clamp(packet.Plast.y, 0f, 2f);
            _genes.PlastExplore = math.clamp(packet.Plast.z, 0f, 2f);
            _genes.PlastNeed = math.clamp(packet.Plast.w, 0f, 2f);
            _auto = packet.Auto != 0;
            _stateVersion = packet.Version;
            _store.Retarget(Slot, in _genes, _topology, _atlas);
            _store.PublishColony(0, _topology);
            _store.MarkDirty(Slot);
            return true;
        }

        public bool ApplyLevels(in SkillLevelsPacket packet, float now)
        {
            if (_authority || !packet.IsWellFormed(_atlas.Count) || packet.Version != _stateVersion) return false;
            float scale = SimulationRules.Active.SkillLevelFixedScale;
            for (int v = 0; v < packet.Levels.Length; v++)
                _store.SetReplicated(Slot, v, SkillLevelsPacket.Dequantize(packet.Levels[v], scale), SkillLevelsPacket.Dequantize(packet.Peaks[v], scale), now);
            _store.MarkDirty(Slot);
            return true;
        }

        public void Capture(PlayerSkillSave save, float now)
        {
            int n = _atlas.Count;
            _store.Materialize(Slot, now, _topology, in SimulationRules.Frame);
            save.skillCount = n;
            save.genes = new[] { _genes.SkillOriginX, _genes.SkillOriginY, _genes.PlastHebb, _genes.PlastStruct, _genes.PlastExplore, _genes.PlastNeed };
            var forest = _topology.ForestGenes;
            save.forest = new[] { forest.x, forest.y, forest.z, forest.w };
            save.levels = new float[n];
            save.peaks = new float[n];
            save.active = new ulong[n];
            for (int v = 0; v < n; v++)
            {
                save.levels[v] = _store.Level(Slot, v);
                save.peaks[v] = _store.Peak(Slot, v);
                save.active[v] = _topology.ActiveRow(v);
            }
            save.islandMask = _topology.IslandMask;
            save.prunedMask = _topology.PrunedMask;
            save.bridges = new ushort[_topology.PrunedBridgeCount];
            for (int i = 0; i < save.bridges.Length; i++) save.bridges[i] = _topology.PrunedBridgeAt(i);
            save.phiPeak = _topology.PhiPeak;
            save.spentEdgePoints = (byte)_topology.SpentEdgePoints;
            save.autoMode = _auto;
        }

        public static bool IsWellFormed(PlayerSkillSave save, int n)
        {
            if (save == null || save.skillCount != n) return false;
            if (save.genes == null || save.genes.Length != GeneCount || save.forest == null || save.forest.Length != ForestCount) return false;
            if (save.levels == null || save.levels.Length != n || save.peaks == null || save.peaks.Length != n) return false;
            if (save.active == null || save.active.Length != n || save.bridges == null || save.bridges.Length > SkillGraphTopology.MaxPrunedBridges) return false;
            if (!math.isfinite(save.phiPeak)) return false;
            for (int i = 0; i < GeneCount; i++)
            {
                var (min, max, _) = GeneticParameters.ParameterRanges[GeneticParameters.IdxSkillOriginX + i];
                if (!math.isfinite(save.genes[i]) || save.genes[i] < min || save.genes[i] > max) return false;
            }
            for (int i = 0; i < ForestCount; i++) if (!math.isfinite(save.forest[i])) return false;
            for (int v = 0; v < n; v++)
            {
                if (!math.isfinite(save.levels[v]) || !math.isfinite(save.peaks[v])) return false;
                if (save.levels[v] < 0f || save.peaks[v] < 0f || save.levels[v] > SkillGraphTopology.MaxPhiPeak || save.peaks[v] > SkillGraphTopology.MaxPhiPeak) return false;
            }
            return true;
        }

        private bool TryRestore(PlayerSkillSave save, ref GeneticParameters genes)
        {
            int n = _atlas.Count;
            if (!IsWellFormed(save, n)) return false;

            var packet = SkillTopologyPacket.Allocate(n);
            packet.Version = 1;
            packet.Revision = 1;
            packet.PhiPeak = save.phiPeak;
            packet.Spent = save.spentEdgePoints;
            packet.IslandMask = save.islandMask;
            packet.PrunedMask = save.prunedMask;
            packet.BridgeCount = (byte)save.bridges.Length;
            for (int i = 0; i < save.bridges.Length; i++) packet.Bridges[i] = save.bridges[i];
            for (int v = 0; v < n; v++) packet.Rows[v] = save.active[v];

            var topology = SkillGraphTopology.Create(_atlas, new float4(save.forest[0], save.forest[1], save.forest[2], save.forest[3]));
            if (!topology.TryApplyState(in packet, false))
            {
                topology.Dispose();
                return false;
            }

            genes.SkillOriginX = save.genes[0];
            genes.SkillOriginY = save.genes[1];
            genes.PlastHebb = save.genes[2];
            genes.PlastStruct = save.genes[3];
            genes.PlastExplore = save.genes[4];
            genes.PlastNeed = save.genes[5];
            _genes = genes;
            _topology = topology;
            _auto = save.autoMode;
            Attach();
            float now = SimulationClock.Time;
            for (int v = 0; v < n; v++) _store.SetReplicated(Slot, v, save.levels[v], save.peaks[v], now);
            _store.MarkDirty(Slot);
            return true;
        }

        public void Dispose()
        {
            _topology?.Dispose();
            _topology = null;
            _store?.Dispose();
        }
    }
}
