using System;
using System.IO;

public class SharedBrainSnapshot : ISerializableNeuralModel
{
    public SharedHierarchicalBrain Brain { get; }

    internal SharedBrainSnapshot(SharedHierarchicalBrain brain) => Brain = brain;

    private const uint Magic   = 0x4E414C45;
    public const int   Version = 7;

    public byte[] Serialize()
    {
        byte[] reservoir = Brain.Reservoir.Serialize();
        byte[] coordMlp  = Brain.Coordinator.Serialize();

        int goalCount = SharedHierarchicalBrain.GoalCount;
        var execMlp = new byte[goalCount][];
        for (int i = 0; i < goalCount; i++)
            execMlp[i] = Brain.Executors[i].Serialize();

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(Version);

            WriteBlock(bw, reservoir);
            WriteBlock(bw, coordMlp);
            for (int i = 0; i < goalCount; i++)
                WriteBlock(bw, execMlp[i]);

            return ms.ToArray();
        }
    }

    public static bool TryReadVersion(byte[] data, out int version)
    {
        version = 0;
        if (data == null || data.Length < 8 || BitConverter.ToUInt32(data, 0) != Magic) return false;
        version = BitConverter.ToInt32(data, 4);
        return true;
    }

    public static SharedBrainSnapshot Deserialize(byte[] data)
    {
        if (data == null || data.Length < 8)
        {
            RavineLog.Error("Снапшот мозга: файл пуст или обрезан, загрузка отменена");
            return null;
        }

        if (!TryReadVersion(data, out int version))
        {
            RavineLog.Error("Снапшот мозга: неизвестный формат, загрузка отменена, переобучение обязательно");
            return null;
        }

        if (version != Version)
        {
            RavineLog.Error($"Снапшот мозга: версия {version}, ожидалась {Version} (координатор: {PlanCatalog.Count} планов + {SharedHierarchicalBrain.CoordAux} aux стайности, вход {InputVectorizer.VectorSize}, {ActionCatalog.Count} действий), сохранённый мозг несовместим, загрузка отменена, переобучение обязательно");
            return null;
        }

        try
        {
            return ReadModels(data);
        }
        catch (Exception ex)
        {
            RavineLog.Error($"Снапшот мозга v{version}: повреждённые данные ({ex.Message}), загрузка отменена");
            return null;
        }
    }

    private static SharedBrainSnapshot ReadModels(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            br.ReadUInt32();
            br.ReadInt32();

            var reservoir = LSTMMemory.Deserialize(ReadBlock(br));
            var coordMlp  = DelayedPerceptron.Deserialize(ReadBlock(br));

            int goalCount = SharedHierarchicalBrain.GoalCount;
            var executors = new DelayedPerceptron[goalCount];
            for (int i = 0; i < goalCount; i++)
                executors[i] = DelayedPerceptron.Deserialize(ReadBlock(br));

            var brain = SharedHierarchicalBrain.FromModels(reservoir, coordMlp, executors);
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
        if (len < 0 || len > br.BaseStream.Length - br.BaseStream.Position)
            throw new EndOfStreamException($"блок {len} байт выходит за конец файла");
        return br.ReadBytes(len);
    }
}

public class SharedBrainSnapshotFactory : INeuralModelFactory<SharedBrainSnapshot>
{
    public SharedBrainSnapshot Deserialize(byte[] data) => SharedBrainSnapshot.Deserialize(data);
}
