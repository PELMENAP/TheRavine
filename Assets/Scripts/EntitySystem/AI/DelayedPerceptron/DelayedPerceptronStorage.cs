using System;
using System.IO;

public partial class DelayedPerceptron : ISerializableNeuralModel
{
    public byte[] Serialize()
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(LayerSizes.Length);
            for (int i = 0; i < LayerSizes.Length; i++)
                bw.Write(LayerSizes[i]);

            for (int l = 0; l < _weights.Length; l++)
                for (int n = 0; n < _weights[l].Length; n++)
                    LSTMMemory.WriteArray(bw, _weights[l][n]);

            for (int l = 0; l < _tauWeights.Length; l++)
                for (int n = 0; n < _tauWeights[l].Length; n++)
                    LSTMMemory.WriteArray(bw, _tauWeights[l][n]);

            for (int l = 0; l < _biases.Length; l++)
                LSTMMemory.WriteArray(bw, _biases[l]);

            for (int l = 0; l < _tauBiases.Length; l++)
                LSTMMemory.WriteArray(bw, _tauBiases[l]);

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

            for (int l = 0; l < mlp._weights.Length; l++)
                for (int n = 0; n < mlp._weights[l].Length; n++)
                    LSTMMemory.ReadArray(br, mlp._weights[l][n]);

            for (int l = 0; l < mlp._tauWeights.Length; l++)
                for (int n = 0; n < mlp._tauWeights[l].Length; n++)
                    LSTMMemory.ReadArray(br, mlp._tauWeights[l][n]);

            for (int l = 0; l < mlp._biases.Length; l++)
                LSTMMemory.ReadArray(br, mlp._biases[l]);

            for (int l = 0; l < mlp._tauBiases.Length; l++)
                LSTMMemory.ReadArray(br, mlp._tauBiases[l]);

            return mlp;
        }
    }
}
