using System;
using System.IO;

public partial class DelayedPerceptron : ISerializableNeuralModel
{
    public byte[] Serialize()
    {
        SyncManaged();

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(LayerSizes.Length);
            for (int i = 0; i < LayerSizes.Length; i++)
                bw.Write(LayerSizes[i]);

            int L = _layout.L;

            for (int part = 0; part < 2; part++)
            {
                for (int l = 0; l < L; l++)
                {
                    int inputs  = _layout.LayerSizes[l];
                    int neurons = _layout.LayerSizes[l + 1];
                    var row = new float[inputs];

                    for (int n = 0; n < neurons; n++)
                    {
                        int wi = _layout.RowIndex(l, n);
                        for (int i = 0; i < inputs; i++)
                            row[i] = _wt[wi + (i << 1) + part];
                        LSTMMemory.WriteArray(bw, row);
                    }
                }
            }

            for (int part = 0; part < 2; part++)
            {
                for (int l = 0; l < L; l++)
                {
                    int neurons = _layout.LayerSizes[l + 1];
                    var col = new float[neurons];
                    for (int n = 0; n < neurons; n++)
                        col[n] = _bt[_layout.BiasIndex(l, n) + part];
                    LSTMMemory.WriteArray(bw, col);
                }
            }

            return ms.ToArray();
        }
    }

    public const int MaxLayerSize = 4096;

    public static DelayedPerceptron Deserialize(byte[] data) => Deserialize(data, null);

    public static DelayedPerceptron Deserialize(byte[] data, int[] expectedSizes)
    {
        if (data == null || data.Length < 4)
            throw new InvalidDataException("Ошибка десериализации: блок перцептрона пуст или обрезан");

        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            int len = br.ReadInt32();
            if (len != 5)
                throw new InvalidDataException($"Ошибка десериализации: ожидалось 5 слоёв, получено {len}");

            if (data.Length < 4 + 4 * len)
                throw new InvalidDataException("Ошибка десериализации: заголовок перцептрона обрезан");

            if (expectedSizes != null && expectedSizes.Length != len)
                throw new InvalidDataException($"Ошибка десериализации: ожидалось {expectedSizes.Length} слоёв, получено {len}");

            var layerSizes = new int[len];
            for (int i = 0; i < len; i++)
            {
                int size = br.ReadInt32();
                if (size < 1 || size > MaxLayerSize)
                    throw new InvalidDataException($"Ошибка десериализации: размер слоя {i} = {size} вне допустимых пределов");

                if (expectedSizes != null && size != expectedSizes[i])
                    throw new InvalidDataException($"Ошибка десериализации: размер слоя {i} = {size}, ожидалось {expectedSizes[i]}");

                layerSizes[i] = size;
            }

            long expectedBytes = 4 + 4L * len + SerializedBodyBytes(layerSizes);
            if (data.Length != expectedBytes)
                throw new InvalidDataException($"Ошибка десериализации: блок перцептрона {data.Length} байт, ожидалось {expectedBytes}");

            var mlp = new DelayedPerceptron(layerSizes);
            var lay = mlp._layout;
            int L   = lay.L;

            for (int part = 0; part < 2; part++)
            {
                for (int l = 0; l < L; l++)
                {
                    int inputs  = lay.LayerSizes[l];
                    int neurons = lay.LayerSizes[l + 1];
                    var row = new float[inputs];

                    for (int n = 0; n < neurons; n++)
                    {
                        LSTMMemory.ReadArray(br, row);
                        int wi = lay.RowIndex(l, n);
                        for (int i = 0; i < inputs; i++)
                            mlp._wt[wi + (i << 1) + part] = row[i];
                    }
                }
            }

            for (int part = 0; part < 2; part++)
            {
                for (int l = 0; l < L; l++)
                {
                    int neurons = lay.LayerSizes[l + 1];
                    var col = new float[neurons];
                    LSTMMemory.ReadArray(br, col);
                    for (int n = 0; n < neurons; n++)
                        mlp._bt[lay.BiasIndex(l, n) + part] = col[n];
                }
            }

            return mlp;
        }
    }

    private static long SerializedBodyBytes(int[] layerSizes)
    {
        long total = 0;
        for (int l = 0; l < layerSizes.Length - 1; l++)
        {
            long inputs  = layerSizes[l];
            long neurons = layerSizes[l + 1];
            total += neurons * (4 + 4 * inputs) + 4 + 4 * neurons;
        }
        return total * 2;
    }
}
