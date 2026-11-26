using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using TheRavine.Base;
public sealed class SimulationLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int DeathCount  = (int)DeathCause.Wind + 1;
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
        EntityAction.MoveTo, EntityAction.Attack, EntityAction.Eat, EntityAction.Rest,
        EntityAction.Signal, EntityAction.Take, EntityAction.Put,
    };

    private static readonly int OffPlanKinds = OffSourceStarts + SourceActions.Length * SourceCount;
    private const int EatFailCount  = (int)ColonyStats.EatFail.Count;
    private const int TakeFailCount = (int)ColonyStats.TakeFail.Count;
    private static readonly int OffFeedFails = OffPlanKinds + PlanCatalog.Count * 2;
    private static readonly int OffBirthFails = OffFeedFails + EatFailCount + TakeFailCount;
    private static readonly int Stride = OffBirthFails + (int)ColonyStats.BirthFailure.Count;

    private const int ResidualColumns = 8;

    private static readonly (string Name, Func<EnergyFlows, float> Get)[] FlowColumns =
    {
        ("flow_food_spawn",        f => f.FoodSpawn),
        ("flow_food_maturation",   f => f.FoodMaturation),
        ("flow_child_start",       f => f.ChildStart),
        ("flow_birth_transfer",    f => f.BirthTransfer),
        ("flow_fuel_heat",         f => f.FuelHeat),
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
        ("flow_codex",             f => f.Codex),
        ("flow_outflow",           f => f.Outflow),
    };

    private static readonly string[] StockColumns =
    {
        "stock_entities", "stock_nest", "stock_food", "stock_corpses", "stock_soil", "stock_warmth", "stock_total",
    };

    private static readonly int WorldOnlyColumns = ResidualColumns + FlowColumns.Length + StockColumns.Length;
    private static readonly string WorldOnlyPlaceholder = new string(',', WorldOnlyColumns);
    private static readonly string VirusPlaceholder = new string(',', 11);
    private static readonly string SaturationPlaceholder = new string(',', (int)ColonyArm.Count);
    private static readonly string ConflictPlaceholder = new string(',', GradientConflict.Pairs + GradientConflict.Groups);

    private readonly int[]   _prev          = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly int[]   _delta         = new int[ColonyRegistry.MaxColonies * Stride];
    private readonly float[] _prevLifespan  = new float[ColonyRegistry.MaxColonies];
    private readonly float[] _deltaLifespan = new float[ColonyRegistry.MaxColonies];
    private readonly int[]   _current       = new int[Stride];
    private readonly int[]   _world         = new int[Stride];
    private readonly ColonySpeechTables _worldSpeech = new();
    private readonly VirologyCensus _census = new();
    private readonly float[] _geneSpeed  = new float[ColonyRegistry.MaxColonies];
    private readonly float[] _geneDetect = new float[ColonyRegistry.MaxColonies];
    private readonly int[]   _geneCount  = new int[ColonyRegistry.MaxColonies];
    private long _prevInfections, _prevRecoveries, _prevBlocked;
    private long _infections, _recoveries, _blocked;
    private int  _perturbation;
    private int _previousProposals, _previousAccepted;
    private readonly int[] _previousProposalRefusals = new int[(int)CodexError.Count];
    private const int InteractionCount = (int)ColonyStats.Interaction.Count;
    private readonly double[] _previousInteractions = new double[ColonyRegistry.MaxColonies * InteractionCount];
    private readonly double[] _interactionDelta = new double[ColonyRegistry.MaxColonies * InteractionCount];
    private readonly double[] _worldInteractions = new double[InteractionCount];
    private const float SaturationThreshold = 0.95f;
    private readonly long[] _saturated   = new long[(int)ColonyArm.Count];
    private readonly long[] _activations = new long[(int)ColonyArm.Count];
    private readonly bool    _conflictColumns = SimulationRules.Active.GradientConflictEnabled;
    private readonly double[] _conflictSum = new double[GradientConflict.Pairs + GradientConflict.Groups];
    private readonly int[]    _conflictN   = new int[GradientConflict.Pairs + GradientConflict.Groups];
    private float _prevResidualSum;
    private readonly long[] _previousTraining = new long[ColonyRegistry.MaxColonies * ColonyState.TrainingGroups];
    private readonly long[] _trainingDelta = new long[ColonyRegistry.MaxColonies * ColonyState.TrainingGroups];
    private readonly long[] _worldTraining = new long[ColonyState.TrainingGroups];

    private const int StressBins = 100;
    private const int TickBinsPerMs = 10;
    private const int TickBins = 100 * TickBinsPerMs;
    private readonly int[]   _stressBins  = new int[ColonyRegistry.MaxColonies * StressBins];
    private readonly double[] _stressSum  = new double[ColonyRegistry.MaxColonies];
    private readonly int[]   _stressCount = new int[ColonyRegistry.MaxColonies];
    private readonly int[]   _stressHigh  = new int[ColonyRegistry.MaxColonies];
    private readonly int[]   _worldStressBins = new int[StressBins];
    private readonly int[]   _tickBins = new int[TickBins + 1];
    private long _tickSamples;
    private double _tickMsSum;
    private long _prevAllocated;
    private long _windowAllocated;
    private double _prevSampleTime;
    private double _windowSeconds;
    private int _anomalyLoops, _anomalyFarm, _anomalyLaws;
    private float _anomalyConcentration;
    private readonly string _scenarioName;
    private readonly string _flagSet;
    private readonly ExperimentScenario _scenario;
    private readonly AnomalyDetector _anomalies;
    private bool _runWritten;
    private const int StressSourceCount = (int)ColonyStats.StressSource.Count;
    private static readonly ColonyStats.StressSource[] StressColumnOrder =
    {
        ColonyStats.StressSource.Hunger, ColonyStats.StressSource.Cold, ColonyStats.StressSource.Lonely,
        ColonyStats.StressSource.Task, ColonyStats.StressSource.Threat,
    };
    private readonly double[] _prevStressAdded   = new double[ColonyRegistry.MaxColonies * StressSourceCount];
    private readonly double[] _stressAddedDelta  = new double[ColonyRegistry.MaxColonies * StressSourceCount];
    private readonly double[] _prevStressSeconds = new double[ColonyRegistry.MaxColonies];
    private readonly double[] _stressSecondsDelta = new double[ColonyRegistry.MaxColonies];
    private readonly double[] _prevStressHigh    = new double[ColonyRegistry.MaxColonies];
    private readonly double[] _stressHighDelta   = new double[ColonyRegistry.MaxColonies];
    private readonly double[] _worldStressAdded  = new double[StressSourceCount];
    private double _worldStressSeconds, _worldStressHigh;
    private const int SocialFields = 8;
    private readonly double[] _prevSocial   = new double[ColonyRegistry.MaxColonies * SocialFields];
    private readonly double[] _socialDelta  = new double[ColonyRegistry.MaxColonies * SocialFields];
    private readonly double[] _worldSocial  = new double[SocialFields];
    private readonly double[] _helpAbs      = new double[ColonyRegistry.MaxColonies];
    private readonly int[]    _helpEntries  = new int[ColonyRegistry.MaxColonies];

    private StreamWriter _writer;
    private bool _disposed;
    private string _stamp;

    public bool IsOpen => _writer != null;

    public SimulationLog(string scenarioName = null, string flagSet = null, ExperimentScenario scenario = null, AnomalyDetector anomalies = null)
    {
        _scenarioName = string.IsNullOrEmpty(scenarioName) ? "none" : scenarioName;
        _flagSet = flagSet ?? string.Empty;
        _scenario = scenario;
        _anomalies = anomalies;
        _prevAllocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            string path = Path.Combine(dir, $"simulation_log_{_stamp}.csv");
            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            _writer.WriteLine(BuildHeader(_conflictColumns));
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

    public void NoteTick(long timestampTicks)
    {
        if (_writer == null) return;
        double ms = timestampTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;
        if (!(ms >= 0d)) return;
        int bin = (int)(ms * TickBinsPerMs);
        if (bin > TickBins) bin = TickBins;
        _tickBins[bin]++;
        _tickSamples++;
        _tickMsSum += ms;
    }

    public void Sample(double now, ColonyRegistry colonies, int alive, EnergyLedger ledger, EvolutionService evolution, SpeechBus speech,
        BehaviourDescriptorLog descriptors = null, System.Collections.Generic.List<EntityModel> entities = null, int populationLimit = 0)
    {
        if (_writer == null || colonies == null) return;

        float time   = (float)now;
        float season = SeasonClock.Value(time);
        bool  winter = SeasonClock.IsWinter(time);
        bool  night  = IsNight();

        _windowAllocated = GC.GetAllocatedBytesForCurrentThread() - _prevAllocated;
        _windowSeconds = now - _prevSampleTime;
        if (!_runWritten)
        {
            _runWritten = true;
            WriteRunSidecar(now, colonies);
        }
        SampleStage3(entities);
        SampleStress(entities);
        SampleHelp(entities, time);
        if (_anomalies != null)
        {
            _anomalies.ConsumeWindow(out _anomalyLoops, out _anomalyFarm);
            _anomalyConcentration = _anomalies.SampleConcentration(ServiceLocator.Services.TryGet(out WorldCodexService law) ? law : null, out _anomalyLaws);
        }

        Array.Clear(_world, 0, Stride);
        float worldLifespan = 0f, fill = 0f, rest = 0f, storage = 0f;
        int   members = 0;
        int   civil = 0;
        Array.Clear(_worldTraining, 0, _worldTraining.Length);
        Array.Clear(_worldInteractions, 0, _worldInteractions.Length);
        Array.Clear(_worldStressAdded, 0, _worldStressAdded.Length);
        _worldStressSeconds = 0d;
        _worldStressHigh = 0d;
        Array.Clear(_worldSocial, 0, _worldSocial.Length);

        int n = Math.Min(colonies.Count, ColonyRegistry.MaxColonies);
        for (int c = 0; c < n; c++)
        {
            var colony = colonies[c];
            var s      = colony.Stats;
            Fill(s, _current);
            for (int g = 0; g < ColonyState.TrainingGroups; g++)
            {
                int slot = c * ColonyState.TrainingGroups + g;
                long total = colony.TrainingSteps(g);
                long delta = total - _previousTraining[slot];
                _previousTraining[slot] = total;
                _trainingDelta[slot] = delta;
                _worldTraining[g] += delta;
            }
            for (int k = 0; k < InteractionCount; k++)
            {
                int slot = c * InteractionCount + k;
                double value = s.Interactions[k];
                double delta = value - _previousInteractions[slot];
                _previousInteractions[slot] = value;
                _interactionDelta[slot] = delta;
                _worldInteractions[k] += delta;
            }
            for (int k = 0; k < StressSourceCount; k++)
            {
                int slot = c * StressSourceCount + k;
                double value = s.StressAdded[k];
                double delta = value - _prevStressAdded[slot];
                _prevStressAdded[slot] = value;
                _stressAddedDelta[slot] = delta;
                _worldStressAdded[k] += delta;
            }
            _stressSecondsDelta[c] = s.StressSeconds - _prevStressSeconds[c];
            _prevStressSeconds[c] = s.StressSeconds;
            _stressHighDelta[c] = s.StressHighSeconds - _prevStressHigh[c];
            _prevStressHigh[c] = s.StressHighSeconds;
            for (int k = 0; k < SocialFields; k++)
            {
                int slot = c * SocialFields + k;
                double value = SocialCounter(s, k);
                double delta = value - _prevSocial[slot];
                _prevSocial[slot] = value;
                _socialDelta[slot] = delta;
                _worldSocial[k] += delta;
            }
            _worldStressSeconds += _stressSecondsDelta[c];
            _worldStressHigh += _stressHighDelta[c];
            if (!colony.IsWild) civil += colony.MemberCount;

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

        _worldSpeech.Clear();
        if (speech != null)
            for (int c = 0; c < n; c++) _worldSpeech.Add(speech.TablesOf(colonies[c].Index));

        try
        {
            WriteCommon(time, season, winter, night, "world", -1, alive, _world, 0, worldLifespan, fill * inv, rest * inv, storage * inv);
            WriteWorldOnly(ledger, evolution, residual);
            WriteAttackFails(_world, 0);
            WriteWorldDiagnostics(colonies, n);
            WritePlanKinds(_world, 0);
            WriteFeedFails(_world, 0);
            WriteSpeechMetrics(null, _worldSpeech);
            WriteSaturation(entities);
            WritePredisposition(colonies, n, evolution);
            if (_conflictColumns) WriteConflictWorld(colonies, n);
            WriteStage3(null, _world, 0, true);
            WriteTraining(null, 0, civil, populationLimit);
            WriteBirthFailures(_world, 0);
            WriteCodex(ServiceLocator.Services.TryGet(out WorldCodexService codex) ? codex : null, null);
            WriteInteractions(_worldInteractions, 0);
            WriteBaseline(null, _world, 0, _worldInteractions[(int)ColonyStats.Interaction.DeliveredEnergy], civil, colonies, n);
            WriteStressWindow(-1);
            WriteSocialWindow(-1, ledger);
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
                WriteFeedFails(_delta, c * Stride);
                WriteSpeechMetrics(colony, speech?.TablesOf(colony.Index));
                _writer.Write(SaturationPlaceholder);
                WriteComma();
                WriteFloat(evolution != null ? evolution.MeanPredispositionDistance(colony) : 0f);
                if (_conflictColumns) _writer.Write(ConflictPlaceholder);
                WriteStage3(colony, _delta, c * Stride, false);
                WriteTraining(colony, c, civil, populationLimit);
                WriteBirthFailures(_delta, c * Stride);
                WriteCodex(null, colony);
                WriteInteractions(_interactionDelta, c * InteractionCount);
                WriteBaseline(colony, _delta, c * Stride, _interactionDelta[c * InteractionCount + (int)ColonyStats.Interaction.DeliveredEnergy],
                    colony.MemberCount, colonies, n);
                WriteStressWindow(c);
                WriteSocialWindow(c, null);
                _writer.WriteLine();
            }
            for (int c = 0; c < n; c++) speech?.TablesOf(colonies[c].Index)?.Clear();
            _writer.Flush();
            descriptors?.Sample(_stamp, now, entities);
            Array.Clear(_tickBins, 0, _tickBins.Length);
            _tickSamples = 0;
            _tickMsSum = 0d;
            _prevSampleTime = now;
            _prevAllocated = GC.GetAllocatedBytesForCurrentThread();
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
        for (int i = 0; i < EatFailCount; i++)  dst[OffFeedFails + i] = s.EatFails[i];
        for (int i = 0; i < TakeFailCount; i++) dst[OffFeedFails + EatFailCount + i] = s.TakeFails[i];
        for (int i = 0; i < (int)ColonyStats.BirthFailure.Count; i++) dst[OffBirthFails + i] = s.BirthFailures[i];
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

    private void WriteFeedFails(int[] d, int o)
    {
        for (int i = 0; i < EatFailCount + TakeFailCount; i++) WriteNext(d[o + OffFeedFails + i]);
    }

    private void WritePredisposition(ColonyRegistry colonies, int n, EvolutionService evolution)
    {
        double sum = 0d;
        int members = 0;
        if (evolution != null)
        {
            for (int c = 0; c < n; c++)
            {
                int m = colonies[c].MemberCount;
                if (m < 2) continue;
                sum += evolution.MeanPredispositionDistance(colonies[c]) * m;
                members += m;
            }
        }
        WriteComma();
        if (members > 0) WriteFloat((float)(sum / members));
    }

    private void WriteSaturation(System.Collections.Generic.List<EntityModel> entities)
    {
        Array.Clear(_saturated, 0, _saturated.Length);
        Array.Clear(_activations, 0, _activations.Length);
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null || e.IsDisposed || e.Colony == null || e.Brain == null) continue;
                var ctx = e.Brain.Context;
                if (ctx == null) continue;
                int arm = (int)e.Colony.Arm;
                ctx.CoordMLP.CountSaturated(SaturationThreshold, ref _saturated[arm], ref _activations[arm]);
                for (int g = 0; g < ctx.ExecMLPs.Length; g++)
                    ctx.ExecMLPs[g].CountSaturated(SaturationThreshold, ref _saturated[arm], ref _activations[arm]);
            }
        }
        for (int a = 0; a < _saturated.Length; a++)
        {
            WriteComma();
            if (_activations[a] > 0) WriteFloat((float)_saturated[a] / _activations[a]);
        }
    }

    private void WriteSpeechMetrics(ColonyState colony, ColonySpeechTables tables)
    {
        WriteComma();
        if (colony != null) _writer.Write(ColonyArms.Name(colony.Arm));
        WriteComma();
        WriteFloat(tables != null ? tables.MutualInformationBits() : 0f);
        WriteComma();
        WriteFloat(tables != null ? tables.ListenerShift() : 0f);
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

    private static string BuildHeader(bool conflict)
    {
        var sb = new StringBuilder(8192);
        sb.Append("time_s,season,winter,scope,colony_id,alive,born,died");
        sb.Append(",deaths_killed,deaths_starved,deaths_virus,deaths_toxic,deaths_age,deaths_burn,deaths_wind,mean_lifespan_s");

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
        sb.Append(",eat_fail_no_food,eat_fail_claim,eat_fail_empty,eat_fail_large_no_partner");
        sb.Append(",take_fail_no_food,take_fail_kind,take_fail_claim");
        sb.Append(",arm,speech_mi_bits,listener_shift");
        for (int a = 0; a < (int)ColonyArm.Count; a++) sb.Append(",h_saturated_").Append(ColonyArms.Name((ColonyArm)a));
        sb.Append(",predisposition_dist");
        if (conflict)
        {
            for (int a = 0; a < GradientConflict.Groups; a++)
                for (int b = a + 1; b < GradientConflict.Groups; b++)
                    sb.Append(",grad_cos_").Append(CasteName(a)).Append('_').Append(CasteName(b));
            for (int a = 0; a < GradientConflict.Groups; a++) sb.Append(",grad_norm_").Append(CasteName(a));
        }
        sb.Append(",colony_lr,colony_grad_norm,colony_entropy_reg,perturbation_active,is_wild,strike_success,escape_rate,gene_speed_mean,gene_detect_mean");
        sb.Append(",virus_strains,virus_lineages,virus_strains_new,virus_strains_lost,virus_dominant_strain,virus_dominant_prevalence");
        sb.Append(",virus_immune_dominant_share,virus_mean_integrity,virus_infections,virus_recoveries,virus_infections_blocked");
        sb.Append(",civil_alive,civil_limit,brain_origin,wide_social,train_coord");
        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
            sb.Append(",train_").Append(SnakeCase(((SharedHierarchicalBrain.Goal)g).ToString()));
        for (int i = 0; i < (int)ColonyStats.BirthFailure.Count; i++)
            sb.Append(",birth_fail_").Append(SnakeCase(((ColonyStats.BirthFailure)i).ToString()));
        sb.Append(",codex_nest_stock,codex_nest_threshold,codex_law_count,codex_price_windows,codex_proposals,codex_accepted");
        for (int k = 1; k < (int)CodexError.Count; k++)
            sb.Append(",codex_refused_").Append(SnakeCase(((CodexError)k).ToString()));
        for (int k = 0; k < (int)VarianceAxis.Count; k++)
            sb.Append(",codex_multiplier_").Append(SnakeCase(((VarianceAxis)k).ToString()));
        for (int k = 0; k < InteractionCount; k++)
            sb.Append(",interaction_").Append(SnakeCase(((ColonyStats.Interaction)k).ToString()));
        sb.Append(",tick_ms_mean,tick_ms_p95,gc_alloc_bytes,stress_mean,stress_p90,stress_high_share");
        sb.Append(",social_action_share,delivery_energy_per_agent_min,starvation_death_share,population_capped");
        sb.Append(",anomaly_transfer_loops,anomaly_relief_farm,anomaly_vote_concentration");
        sb.Append(",stress_src_hunger,stress_src_cold,stress_src_lonely,stress_src_task,stress_src_threat,stress_high_time_share");
        sb.Append(",relief_comfort,relief_feed,relief_rest,relief_nest,comfort_started,comfort_completed,give_reciprocal_share,help_balance_asym,flow_social");
        return sb.ToString();
    }

    private void WriteInteractions(double[] values, int offset)
    {
        for (int k = 0; k < InteractionCount; k++)
        {
            WriteComma();
            _writer.Write(values[offset + k].ToString("G9", Inv));
        }
    }

    private void WriteCodex(WorldCodexService codex, ColonyState colony)
    {
        WriteComma();
        if (colony != null && !colony.IsWild) WriteFloat(colony.Nest.Storage);
        WriteComma();
        if (colony != null && !colony.IsWild) WriteFloat(SimulationRules.Active.CodexMinColonyStore);

        if (codex == null)
        {
            for (int k = 0; k < 4 + (int)CodexError.Count - 1 + (int)VarianceAxis.Count; k++) WriteComma();
            return;
        }

        WriteNext(codex.LawCount);
        WriteNext(codex.PriceSamples);
        WriteNext(codex.ProposalsAttempted - _previousProposals);
        WriteNext(codex.ProposalsAccepted - _previousAccepted);
        _previousProposals = codex.ProposalsAttempted;
        _previousAccepted = codex.ProposalsAccepted;
        for (int k = 1; k < (int)CodexError.Count; k++)
        {
            int refused = codex.ProposalRefusals((CodexError)k);
            WriteNext(refused - _previousProposalRefusals[k]);
            _previousProposalRefusals[k] = refused;
        }

        for (int k = 0; k < (int)VarianceAxis.Count; k++)
        {
            WriteComma();
            WriteFloat(codex.EffectiveMultiplier(k));
        }
    }

    private void WriteBirthFailures(int[] delta, int offset)
    {
        for (int i = 0; i < (int)ColonyStats.BirthFailure.Count; i++) WriteNext(delta[offset + OffBirthFails + i]);
    }

    private void WriteTraining(ColonyState colony, int index, int civil, int limit)
    {
        WriteNext(civil);
        WriteNext(limit);
        WriteComma();
        if (colony != null) _writer.Write(colony.BrainOrigin);
        WriteComma();
        if (colony != null) _writer.Write(colony.Brain != null && colony.Brain.WideSocialExecutor ? 1 : 0);
        for (int g = 0; g < ColonyState.TrainingGroups; g++)
        {
            WriteComma();
            long steps = colony == null ? _worldTraining[g] : _trainingDelta[index * ColonyState.TrainingGroups + g];
            _writer.Write(steps.ToString(Inv));
        }
    }

    private void SampleStress(System.Collections.Generic.List<EntityModel> entities)
    {
        Array.Clear(_stressBins, 0, _stressBins.Length);
        Array.Clear(_stressSum, 0, _stressSum.Length);
        Array.Clear(_stressCount, 0, _stressCount.Length);
        Array.Clear(_stressHigh, 0, _stressHigh.Length);
        if (entities == null) return;

        float threshold = SimulationRules.Active.StressHighThreshold;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e == null || e.IsDisposed || e.Colony == null) continue;
            int c = e.Colony.Index;
            if ((uint)c >= (uint)_stressCount.Length) continue;
            float stress = Mathf.Clamp01(e.Stress);
            int bin = Math.Min((int)(stress * StressBins), StressBins - 1);
            _stressBins[c * StressBins + bin]++;
            _stressSum[c] += stress;
            _stressCount[c]++;
            if (stress >= threshold) _stressHigh[c]++;
        }
    }

    private void WriteStress(ColonyState colony, ColonyRegistry colonies, int n)
    {
        int count = 0, high = 0;
        double sum = 0d;
        int[] bins;
        int binOffset;
        if (colony != null)
        {
            int c = colony.Index;
            count = _stressCount[c];
            high = _stressHigh[c];
            sum = _stressSum[c];
            bins = _stressBins;
            binOffset = c * StressBins;
        }
        else
        {
            Array.Clear(_worldStressBins, 0, StressBins);
            for (int c = 0; c < n; c++)
            {
                if (colonies[c].IsWild) continue;
                count += _stressCount[c];
                high += _stressHigh[c];
                sum += _stressSum[c];
                for (int b = 0; b < StressBins; b++) _worldStressBins[b] += _stressBins[c * StressBins + b];
            }
            bins = _worldStressBins;
            binOffset = 0;
        }

        WriteComma();
        if (count > 0) WriteFloat((float)(sum / count));
        WriteComma();
        if (count > 0)
        {
            int target = (int)Math.Ceiling(count * 0.9d);
            int cumulative = 0;
            int bin = 0;
            for (; bin < StressBins; bin++)
            {
                cumulative += bins[binOffset + bin];
                if (cumulative >= target) break;
            }
            WriteFloat((Math.Min(bin, StressBins - 1) + 1) / (float)StressBins);
        }
        WriteComma();
        if (count > 0) WriteFloat(high / (float)count);
    }

    private void WriteBaseline(ColonyState colony, int[] d, int o, double deliveredEnergy, int agents, ColonyRegistry colonies, int n)
    {
        bool world = colony == null;
        if (world)
        {
            WriteComma();
            if (_tickSamples > 0) WriteFloat((float)(_tickMsSum / _tickSamples));
            WriteComma();
            if (_tickSamples > 0)
            {
                long target = (long)Math.Ceiling(_tickSamples * 0.95d);
                long cumulative = 0;
                int bin = 0;
                for (; bin < TickBins; bin++)
                {
                    cumulative += _tickBins[bin];
                    if (cumulative >= target) break;
                }
                WriteFloat((bin + 1) / (float)TickBinsPerMs);
            }
            WriteComma();
            _writer.Write(_windowAllocated.ToString(Inv));
        }
        else _writer.Write(",,,");

        WriteStress(colony, colonies, n);

        int started = 0;
        for (int a = 0; a < ActionCount; a++) started += d[o + OffActions + ActionFields * a];
        WriteComma();
        if (started > 0) WriteFloat(d[o + OffActions + ActionFields * (int)EntityAction.Give] / (float)started);

        WriteComma();
        if (agents > 0 && _windowSeconds > 0d) WriteFloat((float)(deliveredEnergy / agents / (_windowSeconds / 60d)));

        int died = d[o + OffDied];
        WriteComma();
        if (died > 0) WriteFloat(d[o + OffDeaths + (int)DeathCause.Starved] / (float)died);

        WriteComma();
        _writer.Write(d[o + OffBirthFails + (int)ColonyStats.BirthFailure.Capacity] > 0 ? '1' : '0');

        if (world)
        {
            WriteNext(_anomalyLoops);
            WriteNext(_anomalyFarm);
            WriteComma();
            if (_anomalyLaws > 0) WriteFloat(_anomalyConcentration);
        }
        else _writer.Write(",,,");
    }

    private void WriteStressWindow(int colony)
    {
        for (int k = 0; k < StressColumnOrder.Length; k++)
        {
            int source = (int)StressColumnOrder[k];
            WriteComma();
            WriteFloat((float)(colony < 0 ? _worldStressAdded[source] : _stressAddedDelta[colony * StressSourceCount + source]));
        }
        double seconds = colony < 0 ? _worldStressSeconds : _stressSecondsDelta[colony];
        double high = colony < 0 ? _worldStressHigh : _stressHighDelta[colony];
        WriteComma();
        if (seconds > 0d) WriteFloat((float)(high / seconds));
    }

    private static double SocialCounter(ColonyStats s, int field) => field switch
    {
        0 => s.ReliefSum[(int)ColonyStats.ReliefKind.Comfort],
        1 => s.ReliefSum[(int)ColonyStats.ReliefKind.Feed],
        2 => s.ReliefSum[(int)ColonyStats.ReliefKind.Rest],
        3 => s.ReliefSum[(int)ColonyStats.ReliefKind.Nest],
        4 => s.ComfortStarted,
        5 => s.ComfortCompleted,
        6 => s.GiveTransfers,
        _ => s.GiveReciprocal,
    };

    private void SampleHelp(System.Collections.Generic.List<EntityModel> entities, float now)
    {
        Array.Clear(_helpAbs, 0, _helpAbs.Length);
        Array.Clear(_helpEntries, 0, _helpEntries.Length);
        if (entities == null || !ServiceLocator.Services.TryGet(out HelpLog log)) return;

        float tau = SimulationRules.Active.HelpBalanceTau;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (e == null || e.IsDisposed || e.Colony == null || e.ManagerIndex < 0) continue;
            int c = e.Colony.Index;
            if ((uint)c >= (uint)_helpEntries.Length) continue;
            _helpAbs[c] += log.SumAbsBalance(e.ManagerIndex, now, tau, out int n);
            _helpEntries[c] += n;
        }
    }

    private void WriteSocialWindow(int colony, EnergyLedger ledger)
    {
        for (int k = 0; k < 6; k++)
        {
            WriteComma();
            WriteFloat((float)(colony < 0 ? _worldSocial[k] : _socialDelta[colony * SocialFields + k]));
        }

        double transfers = colony < 0 ? _worldSocial[6] : _socialDelta[colony * SocialFields + 6];
        double reciprocal = colony < 0 ? _worldSocial[7] : _socialDelta[colony * SocialFields + 7];
        WriteComma();
        if (transfers > 0d) WriteFloat((float)(reciprocal / transfers));

        double abs = 0d;
        int entries = 0;
        if (colony < 0)
        {
            for (int c = 0; c < _helpAbs.Length; c++)
            {
                abs += _helpAbs[c];
                entries += _helpEntries[c];
            }
        }
        else
        {
            abs = _helpAbs[colony];
            entries = _helpEntries[colony];
        }
        WriteComma();
        if (entries > 0) WriteFloat((float)(abs / entries));

        WriteComma();
        if (colony < 0) WriteFloat(ledger != null ? ledger.Window.Social : 0f);
    }

    private void WriteRunSidecar(double now, ColonyRegistry colonies)
    {
        try
        {
            ref readonly var r = ref SimulationRules.Frame;
            float time = (float)now;
            float period = Mathf.Max(r.SeasonPeriod, 1f);
            int civil = 0, loaded = 0;
            for (int c = 0; c < colonies.Count; c++)
            {
                if (colonies[c].IsWild) continue;
                civil++;
                if (colonies[c].BrainOrigin != "fresh") loaded++;
            }

            var sb = new StringBuilder(1024);
            sb.Append("{\n");
            sb.Append("  \"stamp\": ").Append(JsonText(_stamp)).Append(",\n");
            sb.Append("  \"scenario\": ").Append(JsonText(_scenarioName)).Append(",\n");
            sb.Append("  \"flags\": ").Append(JsonText(_flagSet)).Append(",\n");
            sb.Append("  \"time_s\": ").Append(now.ToString("G9", Inv)).Append(",\n");
            sb.Append("  \"season_value\": ").Append(SeasonClock.Value(time).ToString("G6", Inv)).Append(",\n");
            sb.Append("  \"season_phase\": ").Append((time / period - Mathf.Floor(time / period)).ToString("G6", Inv)).Append(",\n");
            sb.Append("  \"winter\": ").Append(SeasonClock.IsWinter(time) ? "true" : "false").Append(",\n");
            sb.Append("  \"brain_origin\": ").Append(JsonText(civil == 0 ? "none" : loaded == 0 ? "fresh" : loaded == civil ? "loaded" : "mixed")).Append(",\n");
            sb.Append("  \"colonies\": [");

            int ordinal = 0;
            bool first = true;
            for (int c = 0; c < colonies.Count; c++)
            {
                var colony = colonies[c];
                if (!first) sb.Append(',');
                first = false;
                sb.Append("\n    {\"colony_id\": ").Append(colony.ColonyId.ToString(Inv));
                sb.Append(", \"wild\": ").Append(colony.IsWild ? "true" : "false");
                sb.Append(", \"nest_x\": ").Append(colony.Nest.Position.x.ToString("G6", Inv));
                sb.Append(", \"nest_z\": ").Append(colony.Nest.Position.y.ToString("G6", Inv));
                sb.Append(", \"arm\": ").Append(JsonText(ColonyArms.Name(colony.Arm)));
                sb.Append(", \"brain_origin\": ").Append(JsonText(colony.BrainOrigin));
                if (!colony.IsWild)
                {
                    if (_scenario != null && ordinal < _scenario.ColonyCount)
                    {
                        var sc = _scenario.ColonyAt(ordinal);
                        sb.Append(", \"food_multiplier\": ").Append(sc.foodMultiplier.ToString("G6", Inv));
                        sb.Append(", \"food_radius\": ").Append(sc.foodRadius.ToString("G6", Inv));
                        sb.Append(", \"food_start_s\": ").Append(sc.foodStartSeconds.ToString("G6", Inv));
                        sb.Append(", \"food_active_s\": ").Append(sc.foodActiveSeconds.ToString("G6", Inv));
                        sb.Append(", \"food_inactive_s\": ").Append(sc.foodInactiveSeconds.ToString("G6", Inv));
                    }
                    ordinal++;
                }
                sb.Append('}');
            }
            sb.Append("\n  ]\n}\n");

            string path = Path.Combine(NeuralModelStorage.BaseDirectory, $"run_{_stamp}.json");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            RavineLog.Warning($"SimulationLog: не удалось записать run-файл: {e.Message}");
        }
    }

    private static string JsonText(string value)
    {
        var sb = new StringBuilder((value?.Length ?? 0) + 2);
        sb.Append('"');
        if (value != null)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < ' ') sb.Append(' ');
                else sb.Append(ch);
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string CasteName(int caste) => SnakeCase(((Caste)caste).ToString());

    private void SampleStage3(System.Collections.Generic.List<EntityModel> entities)
    {
        Array.Clear(_geneSpeed, 0, _geneSpeed.Length);
        Array.Clear(_geneDetect, 0, _geneDetect.Length);
        Array.Clear(_geneCount, 0, _geneCount.Length);
        if (entities != null)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null || e.IsDisposed || e.Colony == null || e.Brain == null) continue;
                int c = e.Colony.Index;
                if ((uint)c >= (uint)_geneCount.Length) continue;
                var genes = e.Brain.Context.CoordMLP.Params;
                _geneSpeed[c]  += genes.MoveSpeedMul;
                _geneDetect[c] += genes.DetectionRadiusMul;
                _geneCount[c]++;
            }
            _census.Sample(entities);
        }

        _infections = TheRavine.EntityControl.Virology.VirologyEvents.Infections - _prevInfections;
        _recoveries = TheRavine.EntityControl.Virology.VirologyEvents.Recoveries - _prevRecoveries;
        _blocked    = TheRavine.EntityControl.Virology.VirologyEvents.Blocked - _prevBlocked;
        _prevInfections = TheRavine.EntityControl.Virology.VirologyEvents.Infections;
        _prevRecoveries = TheRavine.EntityControl.Virology.VirologyEvents.Recoveries;
        _prevBlocked    = TheRavine.EntityControl.Virology.VirologyEvents.Blocked;

        _perturbation = ServiceLocator.Services.TryGet(out WorldCodexService codex) ? codex.PerturbationActive : 0;
    }

    private void WriteStage3(ColonyState colony, int[] d, int o, bool world)
    {
        if (colony != null)
        {
            var genome = colony.Genome;
            WriteComma(); WriteFloat(genome.BaseLearningRate);
            WriteComma(); WriteFloat(genome.MaxGradientNorm);
            WriteComma(); WriteFloat(genome.EntropyRegularization);
        }
        else _writer.Write(",,,");

        WriteNext(_perturbation);
        WriteComma();
        if (colony != null) _writer.Write(colony.IsWild ? '1' : '0');

        int attempts = d[o + OffActions + ActionFields * (int)EntityAction.Attack];
        float inv = attempts > 0 ? 1f / attempts : 0f;
        int strikes = d[o + OffAttacks + (int)ColonyStats.AttackTarget.Foreign] + d[o + OffAttacks + (int)ColonyStats.AttackTarget.Player];
        int escaped = d[o + OffAttackFails + 1] + d[o + OffAttackFails + 3];
        WriteComma();
        if (attempts > 0) WriteFloat(strikes * inv);
        WriteComma();
        if (attempts > 0) WriteFloat(escaped * inv);

        int count = colony != null ? _geneCount[colony.Index] : 0;
        WriteComma();
        if (count > 0) WriteFloat(_geneSpeed[colony.Index] / count);
        WriteComma();
        if (count > 0) WriteFloat(_geneDetect[colony.Index] / count);

        if (!world)
        {
            _writer.Write(VirusPlaceholder);
            return;
        }

        WriteNext(_census.Strains);
        WriteNext(_census.Lineages);
        WriteNext(_census.StrainsNew);
        WriteNext(_census.StrainsLost);
        WriteComma();
        _writer.Write(_census.Dominant.ToString(Inv));
        WriteComma(); WriteFloat(_census.DominantPrevalence);
        WriteComma(); WriteFloat(_census.ImmuneDominantShare);
        WriteComma(); WriteFloat(_census.MeanIntegrity);
        WriteComma(); _writer.Write(_infections.ToString(Inv));
        WriteComma(); _writer.Write(_recoveries.ToString(Inv));
        WriteComma(); _writer.Write(_blocked.ToString(Inv));
    }

    private void WriteConflictWorld(ColonyRegistry colonies, int n)
    {
        Array.Clear(_conflictSum, 0, _conflictSum.Length);
        Array.Clear(_conflictN, 0, _conflictN.Length);
        for (int c = 0; c < n; c++)
        {
            var conflict = colonies[c].Brain?.Conflict;
            if (conflict == null || !conflict.Fresh) continue;
            for (int i = 0; i < GradientConflict.Pairs; i++) AddConflict(i, conflict.Cos[i]);
            for (int i = 0; i < GradientConflict.Groups; i++) AddConflict(GradientConflict.Pairs + i, conflict.Norm[i]);
            conflict.Consume();
        }
        for (int i = 0; i < _conflictSum.Length; i++)
        {
            WriteComma();
            if (_conflictN[i] > 0) WriteFloat((float)(_conflictSum[i] / _conflictN[i]));
        }
    }

    private void AddConflict(int slot, float value)
    {
        if (float.IsNaN(value)) return;
        _conflictSum[slot] += value;
        _conflictN[slot]++;
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
