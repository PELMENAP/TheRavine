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

    public static DelayedPerceptron Deserialize(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            int len = br.ReadInt32();
            if (len != 5)
                throw new Exception($"Ошибка десериализации: ожидалось 5 слоёв, получено {len}");

            var layerSizes = new int[len];
            for (int i = 0; i < len; i++)
                layerSizes[i] = br.ReadInt32();

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
}