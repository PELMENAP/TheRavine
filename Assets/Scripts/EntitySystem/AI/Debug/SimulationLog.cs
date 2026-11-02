using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class SimulationLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int DeathCount  = (int)DeathCause.Burn + 1;
    private const int SourceCount = (int)CommandSource.Count;
    private const int AttackCount = (int)ColonyStats.AttackTarget.Count;
    private const int ActionCount = ActionCatalog.Count;

    private const int OffBorn           = 0;
    private const int OffDied           = 1;
    private const int OffDeaths         = 2;
    private const int OffActions        = OffDeaths + DeathCount;
    private const int OffPlansStarted   = OffActions + 3 * ActionCount;
    private const int OffPlansCompleted = OffPlansStarted + 1;
    private const int OffSources        = OffPlansCompleted + 1;
    private const int OffRelocations    = OffSources + SourceCount;
    private const int OffAttacks        = OffRelocations + 1;
    private const int OffHarm           = OffAttacks + AttackCount;
    private const int OffWarmth         = OffHarm + 1;
    private const int Stride            = OffWarmth + 1;

    private readonly int[]   _prev         = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly int[]   _delta        = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly float[] _prevLifespan = new float[ColonyRegistry.MaxColonies];
    private readonly float[] _deltaLifespan = new float[ColonyRegistry.MaxColonies];
    private readonly int[]   _current      = new int[Stride];
    private readonly int[]   _world        = new int[Stride];
    private float _prevResidualSum;

    private StreamWriter _writer;
    private bool _disposed;

    public bool IsOpen => _writer != null;

    public SimulationLog()
    {
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"simulation_log_{DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv)}.csv");
            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            _writer.WriteLine(BuildHeader());
            _writer.Flush();
            Application.quitting += Dispose;
        }
        catch (Exception e)
        {
            RavineLog.Warning($"SimulationLog: не удалось открыть файл лога: {e.Message}");
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void Sample(double now, ColonyRegistry colonies, int alive, EnergyLedger ledger, EvolutionService evolution)
    {
        if (_writer == null || colonies == null) return;

        float time   = (float)now;
        float season = SeasonClock.Value(time);
        bool  winter = SeasonClock.IsWinter(time);

        Array.Clear(_world, 0, Stride);
        float worldLifespan = 0f, fill = 0f, rest = 0f, storage = 0f;
        int   members = 0;

        int n = Math.Min(colonies.Count, ColonyRegistry.MaxColonies);
        for (int c = 0; c < n; c++)
        {
            var colony = colonies[c];
            var s      = colony.Stats;
            Fill(s, _current);

            int b = c * Stride;
            for (int k = 0; k < Stride; k++)
            {
                int d = _current[k] - _prev[b + k];
                _delta[b + k] = d;
                _prev[b + k]  = _current[k];
                _world[k]    += d;
            }

            float lifespan = s.LifespanSum;
            _deltaLifespan[c] = lifespan - _prevLifespan[c];
            _prevLifespan[c]  = lifespan;
            worldLifespan    += _deltaLifespan[c];

            int m = colony.MemberCount;
            if (m <= 0) continue;
            fill    += s.MeanFill * m;
            rest    += s.RestAtNestShare * m;
            storage += s.StorageShare * m;
            members += m;
        }

        float inv = members > 0 ? 1f / members : 0f;
        float residualSum = ledger != null ? ledger.ResidualSum : 0f;
        float residual    = residualSum - _prevResidualSum;
        _prevResidualSum  = residualSum;

        try
        {
            WriteCommon(time, season, winter, "world", -1, alive, _world, 0, worldLifespan, fill * inv, rest * inv, storage * inv);
            _writer.Write(',');
            WriteFloat(residual);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.FitnessP10 : 0f);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.FitnessP50 : 0f);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.FitnessP90 : 0f);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.FitnessMax : 0f);
            _writer.Write(',');
            WriteInt(evolution != null ? evolution.FitnessOutliers : 0);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.AvgFitness : 0f);
            _writer.Write(',');
            WriteFloat(evolution != null ? evolution.AvgEntropy : 0f);
            _writer.WriteLine();

            for (int c = 0; c < n; c++)
            {
                var colony = colonies[c];
                if (colony.MemberCount <= 0) continue;
                var s = colony.Stats;
                WriteCommon(time, season, winter, "colony", colony.ColonyId, colony.MemberCount, _delta, c * Stride,
                    _deltaLifespan[c], s.MeanFill, s.RestAtNestShare, s.StorageShare);
                _writer.WriteLine(",,,,,,,,");
            }
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"SimulationLog: запись остановлена: {e.Message}");
            Dispose();
        }
    }

    private static void Fill(ColonyStats s, int[] dst)
    {
        dst[OffBorn] = s.Born;
        dst[OffDied] = s.Died;
        for (int i = 0; i < DeathCount; i++) dst[OffDeaths + i] = s.DeathsByCause[i];
        for (int a = 0; a < ActionCount; a++)
        {
            int o = OffActions + 3 * a;
            dst[o]     = s.ActionsStarted[a];
            dst[o + 1] = s.ActionsCompleted[a];
            dst[o + 2] = s.ActionsFailed[a];
        }

        int started = 0, completed = 0;
        for (int p = 0; p < s.PlansStarted.Length; p++)
        {
            started   += s.PlansStarted[p];
            completed += s.PlansCompleted[p];
        }
        dst[OffPlansStarted]   = started;
        dst[OffPlansCompleted] = completed;

        for (int i = 0; i < SourceCount; i++) dst[OffSources + i] = s.CommandsBySource[i];
        dst[OffRelocations] = s.Relocations;
        for (int i = 0; i < AttackCount; i++) dst[OffAttacks + i] = s.Attacks[i];
        dst[OffHarm]   = s.ForeignHarmTicks;
        dst[OffWarmth] = s.WarmthDrawTicks;
    }

    private void WriteCommon(float time, float season, bool winter, string scope, int colonyId, int alive,
        int[] d, int o, float lifespanSum, float fill, float rest, float storage)
    {
        WriteFloat(time);
        _writer.Write(',');
        WriteFloat(season);
        _writer.Write(',');
        _writer.Write(winter ? '1' : '0');
        _writer.Write(',');
        _writer.Write(scope);
        _writer.Write(',');
        WriteInt(colonyId);
        _writer.Write(',');
        WriteInt(alive);

        int died = d[o + OffDied];
        WriteNext(d[o + OffBorn]);
        WriteNext(died);
        for (int i = 0; i < DeathCount; i++) WriteNext(d[o + OffDeaths + i]);
        _writer.Write(',');
        WriteFloat(died > 0 ? lifespanSum / died : 0f);

        for (int k = OffActions; k < OffActions + 3 * ActionCount; k++) WriteNext(d[o + k]);
        WriteNext(d[o + OffPlansStarted]);
        WriteNext(d[o + OffPlansCompleted]);

        int commands = 0;
        for (int i = 0; i < SourceCount; i++) commands += d[o + OffSources + i];
        float invCommands = commands > 0 ? 1f / commands : 0f;
        for (int i = 0; i < SourceCount; i++)
        {
            _writer.Write(',');
            WriteFloat(d[o + OffSources + i] * invCommands);
        }

        WriteNext(d[o + OffRelocations]);
        for (int i = 0; i < AttackCount; i++) WriteNext(d[o + OffAttacks + i]);
        WriteNext(d[o + OffHarm]);
        WriteNext(d[o + OffWarmth]);
        _writer.Write(',');
        WriteFloat(fill);
        _writer.Write(',');
        WriteFloat(rest);
        _writer.Write(',');
        WriteFloat(storage);
    }

    private void WriteNext(int value)
    {
        _writer.Write(',');
        WriteInt(value);
    }

    private void WriteInt(int value) => _writer.Write(value.ToString(Inv));

    private void WriteFloat(float value) => _writer.Write(value.ToString("G6", Inv));

    private static string BuildHeader()
    {
        var sb = new StringBuilder(4096);
        sb.Append("time_s,season,winter,scope,colony_id,alive,born,died");
        sb.Append(",deaths_killed,deaths_starved,deaths_virus,deaths_toxic,deaths_age,deaths_burn,mean_lifespan_s");

        string[] actions = Enum.GetNames(typeof(EntityAction));
        for (int a = 0; a < ActionCount; a++)
        {
            string name = SnakeCase(actions[a]);
            sb.Append(',').Append(name).Append("_started");
            sb.Append(',').Append(name).Append("_completed");
            sb.Append(',').Append(name).Append("_failed");
        }

        sb.Append(",plans_started,plans_completed,brain_share,instinct_share,plan_share,relocations");
        sb.Append(",attacks_own,attacks_foreign,attacks_player,foreign_harm_ticks,warmth_draw_ticks");
        sb.Append(",mean_fill,rest_at_nest_share,storage_share");
        sb.Append(",ledger_residual,fitness_p10,fitness_p50,fitness_p90,fitness_max,fitness_outliers,avg_fitness,avg_entropy");
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
        if (_disposed) return;
        _disposed = true;
        Application.quitting -= Dispose;
        _writer?.Dispose();
        _writer = null;
    }
}
