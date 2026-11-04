using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using TheRavine.Base;
public sealed class SimulationLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int DeathCount  = (int)DeathCause.Burn + 1;
    private const int SourceCount = (int)CommandSource.Count;
    private const int AttackCount = (int)ColonyStats.AttackTarget.Count;
    private const int ActionCount = ActionCatalog.Count;
    private const int ActionFields = 4;

    private const int OffBorn           = 0;
    private const int OffDied           = 1;
    private const int OffDeaths         = 2;
    private const int OffActions        = OffDeaths + DeathCount;
    private const int OffPlansStarted   = OffActions + ActionFields * ActionCount;
    private const int OffPlansCompleted = OffPlansStarted + 1;
    private const int OffSources        = OffPlansCompleted + 1;
    private const int OffRelocations    = OffSources + SourceCount;
    private const int OffAttacks        = OffRelocations + 1;
    private const int OffHarm           = OffAttacks + AttackCount;
    private const int OffWarmth         = OffHarm + 1;
    private const int OffAttackFails    = OffWarmth + 1;
    private const int AttackFailCount   = 4;
    private const int OffSourceStarts   = OffAttackFails + AttackFailCount;

    private static readonly EntityAction[] SourceActions =
    {
        EntityAction.Flee, EntityAction.Attack, EntityAction.ApproachFood, EntityAction.Wander,
        EntityAction.ReturnNest, EntityAction.Eat, EntityAction.Rest,
    };

    private static readonly int OffPlanKinds = OffSourceStarts + SourceActions.Length * SourceCount;
    private static readonly int Stride = OffPlanKinds + PlanCatalog.Count * 2;

    private const int ResidualColumns = 8;

    private static readonly (string Name, Func<EnergyFlows, float> Get)[] FlowColumns =
    {
        ("flow_food_spawn",        f => f.FoodSpawn),
        ("flow_food_maturation",   f => f.FoodMaturation),
        ("flow_child_start",       f => f.ChildStart),
        ("flow_spawn_start",       f => f.SpawnStart),
        ("flow_starvation_return", f => f.StarvationReturn),
        ("flow_clamp_gain",        f => f.ClampGain),
        ("flow_viral_net",         f => f.ViralNet),
        ("flow_rescale_net",       f => f.RescaleNet),
        ("flow_basal",             f => f.Basal),
        ("flow_movement",          f => f.Movement),
        ("flow_digestion_loss",    f => f.DigestionLoss),
        ("flow_speech",            f => f.Speech),
        ("flow_threaten",          f => f.Threaten),
        ("flow_dash",              f => f.Dash),
        ("flow_strike",            f => f.Strike),
        ("flow_reproduce",         f => f.Reproduce),
        ("flow_rest_heal",         f => f.RestHeal),
        ("flow_corpse_spoil",      f => f.CorpseSpoil),
        ("flow_death_loss",        f => f.DeathLoss),
        ("flow_yield_loss",        f => f.YieldLoss),
        ("flow_overflow_loss",     f => f.OverflowLoss),
        ("flow_store_loss",        f => f.StoreLoss),
        ("flow_wilt",              f => f.Wilt),
        ("flow_soil_release",      f => f.SoilRelease),
        ("flow_warmth_draw",       f => f.WarmthDraw),
        ("flow_warmth_decay",      f => f.WarmthDecay),
        ("flow_soil_return",       f => f.SoilReturn),
        ("flow_warmth_emit",       f => f.WarmthEmit),
        ("flow_metabolism_credit", f => f.MetabolismCredit),
        ("flow_inflow",            f => f.Inflow),
        ("flow_outflow",           f => f.Outflow),
    };

    private static readonly string[] StockColumns =
    {
        "stock_entities", "stock_nest", "stock_food", "stock_corpses", "stock_soil", "stock_warmth", "stock_total",
    };

    private static readonly int WorldOnlyColumns = ResidualColumns + FlowColumns.Length + StockColumns.Length;
    private static readonly string WorldOnlyPlaceholder = new string(',', WorldOnlyColumns);

    private readonly int[]   _prev          = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly int[]   _delta         = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly float[] _prevLifespan  = new float[ColonyRegistry.MaxColonies];
    private readonly float[] _deltaLifespan = new float[ColonyRegistry.MaxColonies];
    private readonly int[]   _current       = new int[Stride];
    private readonly int[]   _world         = new int[Stride];
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
        bool  night  = IsNight();

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
            WriteCommon(time, season, winter, night, "world", -1, alive, _world, 0, worldLifespan, fill * inv, rest * inv, storage * inv);
            WriteWorldOnly(ledger, evolution, residual);
            WriteAttackFails(_world, 0);
            WriteWorldDiagnostics(colonies, n);
            WritePlanKinds(_world, 0);
            _writer.WriteLine();
            ledger?.ResetWindow();

            for (int c = 0; c < n; c++)
            {
                var colony = colonies[c];
                if (colony.MemberCount <= 0) continue;
                var s = colony.Stats;
                WriteCommon(time, season, winter, night, "colony", colony.ColonyId, colony.MemberCount, _delta, c * Stride,
                    _deltaLifespan[c], s.MeanFill, s.RestAtNestShare, s.StorageShare);
                _writer.Write(WorldOnlyPlaceholder);
                WriteAttackFails(_delta, c * Stride);
                WriteColonyDiagnostics(colony);
                WritePlanKinds(_delta, c * Stride);
                _writer.WriteLine();
            }
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"SimulationLog: запись остановлена: {e.Message}");
            Dispose();
        }
    }

    private void WriteWorldOnly(EnergyLedger ledger, EvolutionService evolution, float residual)
    {
        WriteComma();
        WriteFloat(residual);
        WriteComma();
        WriteFloat(evolution != null ? evolution.FitnessP10 : 0f);
        WriteComma();
        WriteFloat(evolution != null ? evolution.FitnessP50 : 0f);
        WriteComma();
        WriteFloat(evolution != null ? evolution.FitnessP90 : 0f);
        WriteComma();
        WriteFloat(evolution != null ? evolution.FitnessMax : 0f);
        WriteComma();
        WriteInt(evolution != null ? evolution.FitnessOutliers : 0);
        WriteComma();
        WriteFloat(evolution != null ? evolution.AvgFitness : 0f);
        WriteComma();
        WriteFloat(evolution != null ? evolution.AvgEntropy : 0f);

        EnergyFlows window = ledger != null ? ledger.Window : default;
        for (int i = 0; i < FlowColumns.Length; i++)
        {
            WriteComma();
            WriteFloat(FlowColumns[i].Get(window));
        }

        WriteComma();
        WriteFloat(ledger != null ? ledger.StockEntities : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.StockNest : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.StockFood : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.StockCorpses : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.StockSoil : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.StockWarmth : 0f);
        WriteComma();
        WriteFloat(ledger != null ? ledger.Stock : 0f);
    }

    private static bool IsNight()
    {
        DayCycle dayCycle = null;
        float phase = CreatureCore.DayPhase(ref dayCycle);
        ref readonly var r = ref SimulationRules.Frame;
        return CreatureCore.IsNightPhase(phase, in r);
    }

    private static void Fill(ColonyStats s, int[] dst)
    {
        dst[OffBorn] = s.Born;
        dst[OffDied] = s.Died;
        for (int i = 0; i < DeathCount; i++) dst[OffDeaths + i] = s.DeathsByCause[i];
        for (int a = 0; a < ActionCount; a++)
        {
            int o = OffActions + ActionFields * a;
            dst[o]     = s.ActionsStarted[a];
            dst[o + 1] = s.ActionsCompleted[a];
            dst[o + 2] = s.ActionsFailed[a];
            dst[o + 3] = s.ActionsInterrupted[a];
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
        dst[OffAttackFails]     = s.AttackFailNoTarget;
        dst[OffAttackFails + 1] = s.AttackFailTargetLost;
        dst[OffAttackFails + 2] = s.AttackFailLowEnergy;
        dst[OffAttackFails + 3] = s.AttackFailLurkTimeout;
        for (int p = 0; p < PlanCatalog.Count; p++)
        {
            dst[OffPlanKinds + p * 2]     = s.PlansStarted[p];
            dst[OffPlanKinds + p * 2 + 1] = s.PlansCompleted[p];
        }
        for (int k = 0; k < SourceActions.Length; k++)
        {
            int action = (int)SourceActions[k];
            for (int i = 0; i < SourceCount; i++)
                dst[OffSourceStarts + k * SourceCount + i] = s.ActionsStartedBySource[action * SourceCount + i];
        }
    }

    private void WriteColonyDiagnostics(ColonyState colony)
    {
        var s = colony.Stats;
        WriteComma();
        WriteFloat(colony.Scarcity);
        WriteComma();
        WriteFloat(colony.Nest.Hunger);
        WriteComma();
        WriteFloat(colony.IsWild ? 0f : colony.Nest.MigrationPressure);
        WriteComma();
        WriteFloat(colony.SiteVotesMax);
        WriteComma();
        WriteFloat(s.DietMean);
        WriteComma();
        WriteFloat(s.GhrelinMean);
        WriteComma();
        WriteFloat(s.HungryShare);
    }

    private void WriteWorldDiagnostics(ColonyRegistry colonies, int n)
    {
        float scarcity = 0f, hunger = 0f, pressure = 0f, quorum = 0f, diet = 0f, ghrelin = 0f, hungry = 0f;
        int civil = 0, all = 0;
        for (int c = 0; c < n; c++)
        {
            var colony = colonies[c];
            int m = colony.MemberCount;
            if (m <= 0) continue;
            var s = colony.Stats;
            diet    += s.DietMean * m;
            ghrelin += s.GhrelinMean * m;
            hungry  += s.HungryShare * m;
            all     += m;
            if (colony.IsWild) continue;
            scarcity += colony.Scarcity * m;
            hunger   += colony.Nest.Hunger * m;
            pressure  = Math.Max(pressure, colony.Nest.MigrationPressure);
            quorum    = Math.Max(quorum, colony.SiteVotesMax);
            civil    += m;
        }
        float invCivil = civil > 0 ? 1f / civil : 0f;
        float invAll   = all > 0 ? 1f / all : 0f;
        WriteComma();
        WriteFloat(scarcity * invCivil);
        WriteComma();
        WriteFloat(hunger * invCivil);
        WriteComma();
        WriteFloat(pressure);
        WriteComma();
        WriteFloat(quorum);
        WriteComma();
        WriteFloat(diet * invAll);
        WriteComma();
        WriteFloat(ghrelin * invAll);
        WriteComma();
        WriteFloat(hungry * invAll);
    }

    private void WritePlanKinds(int[] d, int o)
    {
        for (int i = 0; i < PlanCatalog.Count * 2; i++) WriteNext(d[o + OffPlanKinds + i]);
    }

    private void WriteAttackFails(int[] d, int o)
    {
        for (int i = 0; i < AttackFailCount; i++) WriteNext(d[o + OffAttackFails + i]);
        int sourceColumns = SourceActions.Length * SourceCount;
        for (int i = 0; i < sourceColumns; i++) WriteNext(d[o + OffSourceStarts + i]);
    }

    private void WriteCommon(float time, float season, bool winter, bool night, string scope, int colonyId, int alive,
        int[] d, int o, float lifespanSum, float fill, float rest, float storage)
    {
        WriteFloat(time);
        WriteComma();
        WriteFloat(season);
        WriteComma();
        _writer.Write(winter ? '1' : '0');
        WriteComma();
        _writer.Write(scope);
        WriteComma();
        WriteInt(colonyId);
        WriteComma();
        WriteInt(alive);

        int died = d[o + OffDied];
        WriteNext(d[o + OffBorn]);
        WriteNext(died);
        for (int i = 0; i < DeathCount; i++) WriteNext(d[o + OffDeaths + i]);
        WriteComma();
        WriteFloat(died > 0 ? lifespanSum / died : 0f);

        for (int k = OffActions; k < OffActions + ActionFields * ActionCount; k++) WriteNext(d[o + k]);
        WriteNext(d[o + OffPlansStarted]);
        WriteNext(d[o + OffPlansCompleted]);

        int commands = 0;
        for (int i = 0; i < SourceCount; i++) commands += d[o + OffSources + i];
        float invCommands = commands > 0 ? 1f / commands : 0f;
        for (int i = 0; i < SourceCount; i++)
        {
            WriteComma();
            WriteFloat(d[o + OffSources + i] * invCommands);
        }

        WriteNext(d[o + OffRelocations]);
        for (int i = 0; i < AttackCount; i++) WriteNext(d[o + OffAttacks + i]);
        WriteNext(d[o + OffHarm]);
        WriteNext(d[o + OffWarmth]);
        WriteComma();
        WriteFloat(fill);
        WriteComma();
        WriteFloat(rest);
        WriteComma();
        WriteFloat(storage);
        WriteComma();
        _writer.Write(night ? '1' : '0');
    }

    private void WriteComma() => _writer.Write(',');

    private void WriteNext(int value)
    {
        WriteComma();
        WriteInt(value);
    }

    private void WriteInt(int value) => _writer.Write(value.ToString(Inv));

    private void WriteFloat(float value) => _writer.Write(value.ToString("G6", Inv));

    private static string BuildHeader()
    {
        var sb = new StringBuilder(8192);
        sb.Append("time_s,season,winter,scope,colony_id,alive,born,died");
        sb.Append(",deaths_killed,deaths_starved,deaths_virus,deaths_toxic,deaths_age,deaths_burn,mean_lifespan_s");

        string[] actions = Enum.GetNames(typeof(EntityAction));
        for (int a = 0; a < ActionCount; a++)
        {
            string name = SnakeCase(actions[a]);
            sb.Append(',').Append(name).Append("_started");
            sb.Append(',').Append(name).Append("_completed");
            sb.Append(',').Append(name).Append("_failed");
            sb.Append(',').Append(name).Append("_interrupted");
        }

        sb.Append(",plans_started,plans_completed,brain_share,instinct_share,plan_share,relocations");
        sb.Append(",attacks_own,attacks_foreign,attacks_player,foreign_harm_ticks,warmth_draw_ticks");
        sb.Append(",mean_fill,rest_at_nest_share,storage_share,night");
        sb.Append(",ledger_residual,fitness_p10,fitness_p50,fitness_p90,fitness_max,fitness_outliers,avg_fitness,avg_entropy");
        for (int i = 0; i < FlowColumns.Length; i++) sb.Append(',').Append(FlowColumns[i].Name);
        for (int i = 0; i < StockColumns.Length; i++) sb.Append(',').Append(StockColumns[i]);
        sb.Append(",attack_fail_no_target,attack_fail_target_lost,attack_fail_low_energy,attack_fail_lurk_timeout");
        string[] sources = Enum.GetNames(typeof(CommandSource));
        for (int k = 0; k < SourceActions.Length; k++)
        {
            string name = SnakeCase(SourceActions[k].ToString());
            for (int i = 0; i < SourceCount; i++)
                sb.Append(',').Append(name).Append("_started_").Append(SnakeCase(sources[i]));
        }
        sb.Append(",scarcity,nest_hunger,migration_pressure,quorum_weight,diet_mean,ghrelin_mean,hungry_share");
        for (int p = 0; p < PlanCatalog.Count; p++)
        {
            string name = SnakeCase(PlanCatalog.Names[p]);
            sb.Append(",plan_").Append(name).Append("_started,plan_").Append(name).Append("_completed");
        }
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
