using System;
using System.IO;
using Unity.Mathematics;

public partial class LSTMMemory : ISerializableNeuralModel
{
    public byte[] Serialize()
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(inputSize);
            bw.Write(hiddenSize);

            WriteArray(bw, W);
            WriteArray(bw, b);

            return ms.ToArray();
        }
    }

    public const int MaxInputSize = 4096;
    public const int MaxHiddenSize = 1024;

    public static LSTMMemory Deserialize(byte[] data) => Deserialize(data, -1);

    public static LSTMMemory Deserialize(byte[] data, int expectedInputSize)
    {
        if (data == null || data.Length < 8)
            throw new InvalidDataException("Ошибка десериализации: блок LSTM пуст или обрезан");

        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            int inputSize = br.ReadInt32();
            int hiddenSize = br.ReadInt32();

            if (inputSize < 1 || inputSize > MaxInputSize || hiddenSize < 1 || hiddenSize > MaxHiddenSize)
                throw new InvalidDataException($"Ошибка десериализации: размеры LSTM {inputSize}/{hiddenSize} вне допустимых пределов");

            if (expectedInputSize >= 0 && inputSize != expectedInputSize)
                throw new InvalidDataException($"Ошибка десериализации: вход LSTM {inputSize}, ожидался {expectedInputSize}");

            long rows = 4L * hiddenSize;
            long expectedBytes = 8 + 4 + 4 * rows * (inputSize + hiddenSize) + 4 + 4 * rows;
            if (data.Length != expectedBytes)
                throw new InvalidDataException($"Ошибка десериализации: блок LSTM {data.Length} байт, ожидалось {expectedBytes}");

            var lstm = new LSTMMemory(inputSize, hiddenSize);

            ReadArray(br, lstm.W);
            ReadArray(br, lstm.b);

            return lstm;
        }
    }

    internal static void WriteArray(BinaryWriter bw, float[] arr)
    {
        bw.Write(arr.Length);
        for (int i = 0; i < arr.Length; i++)
            bw.Write(arr[i]);
    }

    internal static void ReadArray(BinaryReader br, float[] arr)
    {
        int len = br.ReadInt32();
        if (len != arr.Length)
            throw new InvalidDataException($"Ошибка десериализации: ожидался размер {arr.Length}, получено {len}");
        
        for (int i = 0; i < arr.Length; i++)
        {
            float v = br.ReadSingle();
            if (!math.isfinite(v))
                throw new InvalidDataException("Ошибка десериализации: вес не является конечным числом");

            arr[i] = v;
        }
    }
}