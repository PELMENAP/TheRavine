using System.IO;

public class SharedBrainSnapshot : ISerializableNeuralModel
{
    public SharedHierarchicalBrain Brain { get; }

    internal SharedBrainSnapshot(SharedHierarchicalBrain brain) => Brain = brain;

    private const uint Magic   = 0x4E414C45;
    private const int  Version = 2;

    public byte[] Serialize()
    {
        byte[] coordLstm = Brain.CoordLSTM.Serialize();
        byte[] coordMlp  = Brain.Coordinator.Serialize();

        int goalCount = SharedHierarchicalBrain.GoalCount;
        var execLstm = new byte[goalCount][];
        var execMlp  = new byte[goalCount][];
        for (int i = 0; i < goalCount; i++)
        {
            execLstm[i] = Brain.ExecLSTMs[i].Serialize();
            execMlp[i]  = Brain.Executors[i].Serialize();
        }

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(Version);

            WriteBlock(bw, coordLstm);
            WriteBlock(bw, coordMlp);
            for (int i = 0; i < goalCount; i++)
            {
                WriteBlock(bw, execLstm[i]);
                WriteBlock(bw, execMlp[i]);
            }

            return ms.ToArray();
        }
    }

    public static SharedBrainSnapshot Deserialize(byte[] data)
    {
        if (data == null || data.Length < 8)
        {
            UnityEngine.Debug.LogError("Снапшот мозга: файл пуст или обрезан");
            return null;
        }

        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            uint magic = br.ReadUInt32();
            if (magic != Magic)
            {
                UnityEngine.Debug.LogError(
                    "Снапшот мозга: формат версии 1 (без directional head) несовместим с текущей архитектурой, переобучение обязательно");
                return null;
            }

            int version = br.ReadInt32();
            if (version != Version)
            {
                UnityEngine.Debug.LogError($"Снапшот мозга: версия {version}, ожидалась {Version}");
                return null;
            }

            var coordLstm = LSTMMemory.Deserialize(ReadBlock(br));
            var coordMlp  = DelayedPerceptron.Deserialize(ReadBlock(br));

            int goalCount = SharedHierarchicalBrain.GoalCount;
            var execLstms = new LSTMMemory[goalCount];
            var executors = new DelayedPerceptron[goalCount];
            for (int i = 0; i < goalCount; i++)
            {
                execLstms[i] = LSTMMemory.Deserialize(ReadBlock(br));
                executors[i] = DelayedPerceptron.Deserialize(ReadBlock(br));
            }

            var brain = SharedHierarchicalBrain.FromModels(coordLstm, coordMlp, execLstms, executors);
            return brain != null ? new SharedBrainSnapshot(brain) : null;
        }
    }

    private static void WriteBlock(BinaryWriter bw, byte[] block)
    {
        bw.Write(block.Length);
        bw.Write(block);
    }

    private static byte[] ReadBlock(BinaryReader br)
    {
        int len = br.ReadInt32();
        return br.ReadBytes(len);
    }
}

public class SharedBrainSnapshotFactory : INeuralModelFactory<SharedBrainSnapshot>
{
    public SharedBrainSnapshot Deserialize(byte[] data) => SharedBrainSnapshot.Deserialize(data);
}
