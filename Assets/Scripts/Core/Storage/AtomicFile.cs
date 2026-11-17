using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

public static class AtomicFile
{
    public static void Write(string path, Action<Stream> write)
    {
        string temp = path + ".tmp";

        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                write(stream);

            Commit(temp, path);
        }
        catch
        {
            DeleteQuietly(temp);
            throw;
        }
    }

    public static void WriteAllBytes(string path, byte[] data)
    {
        Write(path, stream => stream.Write(data, 0, data.Length));
    }

    public static async UniTask WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        string temp = path + ".tmp";

        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            Commit(temp, path);
        }
        catch
        {
            DeleteQuietly(temp);
            throw;
        }
    }

    public static bool TryReadAllBytes(string path, long maxBytes, out byte[] data)
    {
        data = null;

        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maxBytes) return false;

        data = File.ReadAllBytes(path);
        return true;
    }

    public static bool IsWithinLimit(string path, long maxBytes)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length <= maxBytes;
    }

    private static void Commit(string temp, string path)
    {
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
