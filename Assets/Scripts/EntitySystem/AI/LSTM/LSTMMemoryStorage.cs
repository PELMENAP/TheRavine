using System;
using System.IO;

public class LSTMMemoryFactory : INeuralModelFactory<LSTMMemory>
{
    public LSTMMemory Deserialize(byte[] data)
    {
        return LSTMMemory.Deserialize(data);
    }
}

public partial class LSTMMemory : ISerializableNeuralModel
{
    public byte[] Serialize()
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(inputSize);
            bw.Write(hiddenSize);

            WriteMatrix(bw, Wf);
            WriteMatrix(bw, Wi);
            WriteMatrix(bw, Wo);
            WriteMatrix(bw, Wc);

            WriteArray(bw, bf);
            WriteArray(bw, bi);
            WriteArray(bw, bo);
            WriteArray(bw, bc);

            return ms.ToArray();
        }
    }

    private static void WriteArray(BinaryWriter bw, float[] arr)
    {
        bw.Write(arr.Length);
        for (int i = 0; i < arr.Length; i++)
            bw.Write(arr[i]);
    }

    private static void WriteMatrix(BinaryWriter bw, float[,] mat)
    {
        int rows = mat.GetLength(0);
        int cols = mat.GetLength(1);
        bw.Write(rows);
        bw.Write(cols);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                bw.Write(mat[i, j]);
    }

    public static LSTMMemory Deserialize(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            int inputSize = br.ReadInt32();
            int hiddenSize = br.ReadInt32();

            var lstm = new LSTMMemory(inputSize, hiddenSize);

            ReadMatrix(br, lstm.Wf);
            ReadMatrix(br, lstm.Wi);
            ReadMatrix(br, lstm.Wo);
            ReadMatrix(br, lstm.Wc);

            ReadArray(br, lstm.bf);
            ReadArray(br, lstm.bi);
            ReadArray(br, lstm.bo);
            ReadArray(br, lstm.bc);

            return lstm;
        }
    }

    private static void ReadArray(BinaryReader br, float[] arr)
    {
        int len = br.ReadInt32();
        if (len != arr.Length)
            throw new Exception("Array size mismatch");
        for (int i = 0; i < arr.Length; i++)
            arr[i] = br.ReadSingle();
    }

    private static void ReadMatrix(BinaryReader br, float[,] mat)
    {
        int rows = br.ReadInt32();
        int cols = br.ReadInt32();
        if (rows != mat.GetLength(0) || cols != mat.GetLength(1))
            throw new Exception("Matrix size mismatch");
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                mat[i, j] = br.ReadSingle();
    }
}