using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;

using TheRavine.Base;

public sealed class BehaviourDescriptorLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int Actions = ActionCatalog.Count;
    private const int Goals   = SharedHierarchicalBrain.GoalCount;

    private readonly int _capacity;

    private NativeArray<int>   _actions;
    private NativeArray<int>   _goals;
    private NativeArray<float> _distance;
    private NativeArray<float> _lastX;
    private NativeArray<float> _lastZ;
    private NativeArray<byte>  _hasLast;
    private NativeArray<int>   _speech;
    private NativeArray<int>   _attacks;
    private NativeArray<int>   _food;

    private StreamWriter _writer;
    private double _windowStart;
    private bool _disposed;

    public BehaviourDescriptorLog(int capacity)
    {
        _capacity = math.max(capacity, 1);
        _actions  = new NativeArray<int>(_capacity * Actions, Allocator.Persistent);
        _goals    = new NativeArray<int>(_capacity * Goals, Allocator.Persistent);
        _distance = new NativeArray<float>(_capacity, Allocator.Persistent);
        _lastX    = new NativeArray<float>(_capacity, Allocator.Persistent);
        _lastZ    = new NativeArray<float>(_capacity, Allocator.Persistent);
        _hasLast  = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _speech   = new NativeArray<int>(_capacity, Allocator.Persistent);
        _attacks  = new NativeArray<int>(_capacity, Allocator.Persistent);
        _food     = new NativeArray<int>(_capacity, Allocator.Persistent);
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)_capacity) return;
        ClearRow(index);
        _hasLast[index] = 0;
    }

    public void NoteAction(int index, int action, int goal)
    {
        if ((uint)index >= (uint)_capacity) return;
        if ((uint)action < (uint)Actions) _actions[index * Actions + action]++;
        if ((uint)goal < (uint)Goals) _goals[index * Goals + goal]++;
    }

    public void NoteMove(int index, float2 position)
    {
        if ((uint)index >= (uint)_capacity) return;
        if (_hasLast[index] != 0)
            _distance[index] += math.length(position - new float2(_lastX[index], _lastZ[index]));
        _lastX[index]   = position.x;
        _lastZ[index]   = position.y;
        _hasLast[index] = 1;
    }

    public void NoteSpeech(int index)
    {
        if ((uint)index < (uint)_capacity) _speech[index]++;
    }

    public void NoteAttack(int index)
    {
        if ((uint)index < (uint)_capacity) _attacks[index]++;
    }

    public void NoteFood(int index)
    {
        if ((uint)index < (uint)_capacity) _food[index]++;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            for (int a = 0; a < Actions; a++) _actions[index * Actions + a] = _actions[last * Actions + a];
            for (int g = 0; g < Goals; g++) _goals[index * Goals + g] = _goals[last * Goals + g];
            _distance[index] = _distance[last];
            _lastX[index]    = _lastX[last];
            _lastZ[index]    = _lastZ[last];
            _hasLast[index]  = _hasLast[last];
            _speech[index]   = _speech[last];
            _attacks[index]  = _attacks[last];
            _food[index]     = _food[last];
        }
        ClearRow(last);
        _hasLast[last] = 0;
    }

    public int ActionCount(int index, int action) => _actions[index * Actions + action];
    public int GoalCount(int index, int goal)     => _goals[index * Goals + goal];
    public float Distance(int index)              => _distance[index];
    public int SpeechCount(int index)             => _speech[index];
    public int AttackCount(int index)             => _attacks[index];
    public int FoodCount(int index)               => _food[index];

    private void ClearRow(int index)
    {
        for (int a = 0; a < Actions; a++) _actions[index * Actions + a] = 0;
        for (int g = 0; g < Goals; g++) _goals[index * Goals + g] = 0;
        _distance[index] = 0f;
        _speech[index]   = 0;
        _attacks[index]  = 0;
        _food[index]     = 0;
    }

    public void Sample(string stamp, double now, List<EntityModel> entities)
    {
        if (_disposed || entities == null) return;
        if (_writer == null && !Open(stamp)) return;

        float window = (float)(now - _windowStart);
        _windowStart = now;
        float time = (float)now;
        float floor = SimulationRules.Frame.CuriosityEnergyFloor;
        var sb = new StringBuilder(256);

        try
        {
            int n = Math.Min(entities.Count, _capacity);
            for (int i = 0; i < n; i++)
            {
                var e = entities[i];
                if (e == null || e.IsDisposed || e.IsDeathPending || e.ManagerIndex != i) continue;
                var colony = e.Colony;
                if (colony == null) continue;

                sb.Clear();
                sb.Append(time.ToString("G6", Inv)).Append(',');
                sb.Append(e.EntityId.ToString(Inv)).Append(',');
                sb.Append(colony.ColonyId.ToString(Inv)).Append(',');
                sb.Append(ColonyArms.Name(colony.Arm)).Append(',');
                sb.Append(CasteLabel(e)).Append(',');
                sb.Append(e.TimeAlive.ToString("G6", Inv));

                int started = 0;
                for (int a = 0; a < Actions; a++) started += _actions[i * Actions + a];
                float invStarted = started > 0 ? 1f / started : 0f;
                for (int a = 0; a < Actions; a++)
                    sb.Append(',').Append((_actions[i * Actions + a] * invStarted).ToString("G6", Inv));

                int goalTotal = 0;
                for (int g = 0; g < Goals; g++) goalTotal += _goals[i * Goals + g];
                float invGoals = goalTotal > 0 ? 1f / goalTotal : 0f;
                for (int g = 0; g < Goals; g++)
                    sb.Append(',').Append((_goals[i * Goals + g] * invGoals).ToString("G6", Inv));

                float observed = math.max(math.min(window, e.TimeAlive), 1e-3f);
                float maxEnergy = math.max(e.Stats.MaxEnergy, 1e-3f);
                sb.Append(',').Append(_distance[i].ToString("G6", Inv));
                sb.Append(',').Append((_speech[i] / observed).ToString("G6", Inv));
                sb.Append(',').Append(_attacks[i].ToString(Inv));
                sb.Append(',').Append(_food[i].ToString(Inv));
                sb.Append(',').Append(SharedHierarchicalBrain.EnergySurplus(e.Stats.En / maxEnergy, floor).ToString("G6", Inv));
                var genes = e.Brain.Context.CoordMLP.Params;
                var span = GeneticParameters.AsReadOnlySpan(in genes);
                for (int k = 0; k < SimulationRules.PredispositionSize; k++)
                    sb.Append(',').Append(span[GeneticParameters.IdxPredisposition0 + k].ToString("G6", Inv));
                _writer.WriteLine(sb.ToString());

                ClearRow(i);
            }
            _writer.Flush();
        }
        catch (Exception ex)
        {
            RavineLog.Warning($"BehaviourDescriptorLog: запись остановлена: {ex.Message}");
            Dispose();
        }
    }

    private static string CasteLabel(EntityModel e)
    {
        if (e.IsWild) return "wild";
        if (!e.CasteFixed || e.IsJuvenile) return "juvenile";
        return e.Caste.ToString().ToLowerInvariant();
    }

    private bool Open(string stamp)
    {
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(Path.Combine(dir, $"behaviour_descriptors_{stamp}.csv"), false, new UTF8Encoding(false));
            _writer.WriteLine(BuildHeader());
            _writer.Flush();
            return true;
        }
        catch (Exception e)
        {
            RavineLog.Warning($"BehaviourDescriptorLog: не удалось открыть файл лога: {e.Message}");
            _writer?.Dispose();
            _writer = null;
            _disposed = true;
            return false;
        }
    }

    private static string BuildHeader()
    {
        var sb = new StringBuilder(512);
        sb.Append("time_s,agent_id,colony_id,arm,caste,age_s");
        string[] actions = Enum.GetNames(typeof(EntityAction));
        for (int a = 0; a < Actions; a++) sb.Append(',').Append(SnakeCase(actions[a])).Append("_share");
        string[] goals = Enum.GetNames(typeof(SharedHierarchicalBrain.Goal));
        for (int g = 0; g < Goals; g++) sb.Append(",goal_").Append(SnakeCase(goals[g])).Append("_share");
        sb.Append(",distance,speech_rate,attacks,food_eaten,energy_surplus");
        for (int i = 0; i < SimulationRules.PredispositionSize; i++) sb.Append(",predisposition_").Append(i);
        return sb.ToString();
    }

    private static string SnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char ch = name[i];
            if (char.IsUpper(ch) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_actions.IsCreated)  _actions.Dispose();
        if (_goals.IsCreated)    _goals.Dispose();
        if (_distance.IsCreated) _distance.Dispose();
        if (_lastX.IsCreated)    _lastX.Dispose();
        if (_lastZ.IsCreated)    _lastZ.Dispose();
        if (_hasLast.IsCreated)  _hasLast.Dispose();
        if (_speech.IsCreated)   _speech.Dispose();
        if (_attacks.IsCreated)  _attacks.Dispose();
        if (_food.IsCreated)     _food.Dispose();
        _writer?.Dispose();
        _writer = null;
        _disposed = true;
    }
}
