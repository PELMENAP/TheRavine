using System;
using System.Globalization;
using System.IO;
using System.Text;

using TheRavine.Base;

public sealed class BrainDistanceLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly int _limit;
    private readonly ColonyState[] _colonies;
    private readonly double[] _norms;
    private StreamWriter _writer;

    public BrainDistanceLog(string stamp, int limit)
    {
        _limit = Math.Max(limit, 2);
        _colonies = new ColonyState[_limit];
        _norms = new double[_limit];
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(Path.Combine(dir, $"brain_distance_{stamp}.csv"), false, new UTF8Encoding(false));
            _writer.WriteLine("time_s,colony_a,colony_b,distance");
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"BrainDistanceLog: не удалось открыть файл: {e.Message}");
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void Write(float time, ColonyRegistry colonies, int count)
    {
        if (_writer == null) return;

        int n = 0;
        for (int c = 0; c < count && n < _limit; c++)
        {
            var colony = colonies[c];
            if (colony.IsWild || colony.Brain == null) continue;
            _colonies[n] = colony;
            _norms[n] = colony.Brain.ExecutorNormSq();
            n++;
        }

        try
        {
            string stamp = time.ToString("G6", Inv);
            for (int a = 0; a < n; a++)
            {
                for (int b = a + 1; b < n; b++)
                {
                    double norm = Math.Sqrt(_norms[a] * _norms[b]);
                    if (!(norm > 0d)) continue;
                    double distance = 1d - _colonies[a].Brain.ExecutorDot(_colonies[b].Brain) / norm;
                    _writer.Write(stamp);
                    _writer.Write(',');
                    _writer.Write(_colonies[a].ColonyId.ToString(Inv));
                    _writer.Write(',');
                    _writer.Write(_colonies[b].ColonyId.ToString(Inv));
                    _writer.Write(',');
                    _writer.WriteLine(((float)distance).ToString("G6", Inv));
                }
            }
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"BrainDistanceLog: запись остановлена: {e.Message}");
            Dispose();
        }
        for (int i = 0; i < n; i++) _colonies[i] = null;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }
}
