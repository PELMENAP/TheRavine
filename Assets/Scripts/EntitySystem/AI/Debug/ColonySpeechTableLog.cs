using System;
using System.Globalization;
using System.IO;
using System.Text;

using TheRavine.Base;

public sealed class ColonySpeechTableLog : IDisposable
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly StringBuilder _cells = new(4096);
    private StreamWriter _writer;

    public ColonySpeechTableLog(string stamp)
    {
        try
        {
            string dir = NeuralModelStorage.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(Path.Combine(dir, $"speech_tables_{stamp}.csv"), false, new UTF8Encoding(false));
            _writer.WriteLine("time_s,colony_id,arm,tokens,cells");
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"ColonySpeechTableLog: не удалось открыть файл: {e.Message}");
            _writer?.Dispose();
            _writer = null;
        }
    }

    public void Write(float time, ColonyState colony, ColonySpeechTables tables)
    {
        if (_writer == null || colony == null || tables == null) return;

        _cells.Clear();
        long total = tables.AppendSparse(_cells);
        if (total == 0) return;

        try
        {
            _writer.Write(time.ToString("G6", Inv));
            _writer.Write(',');
            _writer.Write(colony.ColonyId.ToString(Inv));
            _writer.Write(',');
            _writer.Write(ColonyArms.Name(colony.Arm));
            _writer.Write(',');
            _writer.Write(total.ToString(Inv));
            _writer.Write(',');
            _writer.WriteLine(_cells.ToString());
            _writer.Flush();
        }
        catch (Exception e)
        {
            RavineLog.Warning($"ColonySpeechTableLog: запись остановлена: {e.Message}");
            Dispose();
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }
}
