using System;
using System.IO;

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

    public static LSTMMemory Deserialize(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            int inputSize = br.ReadInt32();
            int hiddenSize = br.ReadInt32();

            var lstm = new LSTMMemory(inputSize, hiddenSize);

            ReadArray(br, lstm.W);
            ReadArray(br, lstm.b);

            return lstm;
        }
    }

    private static void WriteArray(BinaryWriter bw, float[] arr)
    {
        bw.Write(arr.Length);
        for (int i = 0; i < arr.Length; i++)
            bw.Write(arr[i]);
    }

    private static void ReadArray(BinaryReader br, float[] arr)
    {
        int len = br.ReadInt32();
        if (len != arr.Length)
            throw new Exception($"Ошибка десериализации: ожидался размер {arr.Length}, получено {len}");
        
        for (int i = 0; i < arr.Length; i++)
            arr[i] = br.ReadSingle();
    }
}