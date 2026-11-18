using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

using TheRavine.Base;

public sealed class LifeEventLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const int FlushEvery = 512;

    private readonly StringBuilder _row = new(512);
    private readonly int[] _geneIndices;
    private StreamWriter _writer;
    private int _pending;
    private bool _disposed;

    public bool IsOpen => _writer != null;

    public LifeEventLog()
    {
        var fields = typeof(GeneticParameters).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var indices = new System.Collections.Generic.List<int>(fields.Length);
        var names = new StringBuilder(1024);
        for (int i = 0; i < fields.Length; i++)
        {
            if (IsColonyGene(i)) continue;
            indices.Add(i);
            names.Append(',').Append(fields[i].Name);
        }
        _geneIndices = indices.ToArray();

        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"life_log_{DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv)}.csv");
            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            _writer.WriteLine("event,time_s,agent_id,parent_id,mate_id,colony_id,cause,age_s" + names);
            _writer.Flush();
            Application.quitting += Dispose;
        }
        catch (Exception e)
        {
            RavineLog.Warning($"LifeEventLog: не удалось открыть файл лога: {e.Message}");
            _writer?.Dispose();
            _writer = null;
        }
    }

    private static bool IsColonyGene(int index)
        => index == GeneticParameters.IdxBaseLearningRate
        || index == GeneticParameters.IdxMaxGradientNorm
        || index == GeneticParameters.IdxEntropyRegularization;

    public void Birth(EntityModel child, EntityModel parent, EntityModel mate)
    {
        if (_writer == null || child == null || child.Colony == null) return;

        _row.Clear();
        _row.Append("birth,").Append(((float)SimulationClock.TimeD).ToString("G6", Inv)).Append(',');
        _row.Append(child.EntityId.ToString(Inv)).Append(',');
        _row.Append(parent != null ? parent.EntityId.ToString(Inv) : "").Append(',');
        _row.Append(mate != null ? mate.EntityId.ToString(Inv) : "").Append(',');
        _row.Append(child.Colony.ColonyId.ToString(Inv)).Append(",,0");

        var genes = child.Brain.Context.CoordMLP.Params;
        var span = GeneticParameters.AsReadOnlySpan(in genes);
        for (int i = 0; i < _geneIndices.Length; i++)
            _row.Append(',').Append(span[_geneIndices[i]].ToString("G6", Inv));
        Write();
    }

    public void Death(EntityModel model, DeathCause cause)
    {
        if (_writer == null || model == null) return;

        _row.Clear();
        _row.Append("death,").Append(((float)SimulationClock.TimeD).ToString("G6", Inv)).Append(',');
        _row.Append(model.EntityId.ToString(Inv)).Append(",,,");
        _row.Append(model.Colony != null ? model.Colony.ColonyId.ToString(Inv) : "").Append(',');
        _row.Append(cause.ToString()).Append(',').Append(model.TimeAlive.ToString("G6", Inv));
        Write();
    }

    private void Write()
    {
        try
        {
            _writer.WriteLine(_row.ToString());
            if (++_pending < FlushEvery) return;
            _pending = 0;
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"LifeEventLog: запись остановлена: {e.Message}");
            Dispose();
        }
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
