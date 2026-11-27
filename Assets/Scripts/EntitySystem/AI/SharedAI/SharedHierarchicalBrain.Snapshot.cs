using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class SharedHierarchicalBrain
{
    internal static SharedHierarchicalBrain FromModels(
        LSTMMemory reservoir, DelayedPerceptron coordinator, DelayedPerceptron[] executors)
    {
        if (reservoir == null || coordinator == null || executors == null || executors.Length != GoalCount)
        {
            RavineLog.Error("Снапшот мозга некорректен: состав моделей не совпадает, загрузка отменена");
            return null;
        }

        for (int i = 0; i < GoalCount; i++)
        {
            if (executors[i] == null)
            {
                RavineLog.Error($"Снапшот мозга некорректен: exec-модель {i} отсутствует, загрузка отменена");
                return null;
            }

            int[] sizes  = executors[i].LayerSizes;
            int actual   = sizes[sizes.Length - 1];
            int expected = ActionSubsets[i].Length + 1 + ExecAux(i);
            if (actual != expected)
            {
                RavineLog.Error($"Снапшот мозга несовместим: exec {i} имеет {actual} выходов, ожидалось {expected}, загрузка отменена; инкрементируйте SharedBrainSnapshot.Version при изменении выходов");
                return null;
            }
        }

        return new SharedHierarchicalBrain(reservoir, coordinator, executors);
    }

    public void AddWeightNoise(float relativeSigma)
    {
        CompletePending();
        coordinator.AddWeightNoise(relativeSigma);
        for (int g = 0; g < GoalCount; g++) executors[g].AddWeightNoise(relativeSigma);
    }

    public double ExecutorDot(SharedHierarchicalBrain other)
    {
        CompletePending();
        other.CompletePending();
        double dot = 0d;
        for (int g = 0; g < GoalCount; g++) dot += executors[g].WeightDot(other.executors[g]);
        return dot;
    }

    public double ExecutorNormSq()
    {
        CompletePending();
        double sum = 0d;
        for (int g = 0; g < GoalCount; g++) sum += executors[g].WeightNormSq();
        return sum;
    }

    public float ExecutorCosineDistance(SharedHierarchicalBrain other)
    {
        if (other == null) return float.NaN;
        double norm = Math.Sqrt(ExecutorNormSq() * other.ExecutorNormSq());
        return norm > 0d ? (float)(1d - ExecutorDot(other) / norm) : float.NaN;
    }

    public SharedBrainSnapshot ToSnapshot()
    {
        CompletePending();
        return new SharedBrainSnapshot(this);
    }

    public static SharedHierarchicalBrain FromSnapshot(byte[] data, int inputSize, int lstmHidden)
        => FromSnapshot(SharedBrainSnapshot.Deserialize(data), inputSize, lstmHidden);

    public static SharedHierarchicalBrain FromSnapshot(SharedBrainSnapshot snapshot, int inputSize, int lstmHidden, bool wideSocial = false)
    {
        if (snapshot == null || snapshot.Brain == null) return null;

        if (!snapshot.Brain.MatchesArchitecture(inputSize, lstmHidden, wideSocial))
        {
            RavineLog.Error($"Архитектура снапшота ({snapshot.Brain.InputSize}/{snapshot.Brain.LstmHidden}, wideSocial={snapshot.Brain.WideSocialExecutor}) " +
                            $"не совпадает с текущей ({inputSize}/{lstmHidden}, wideSocial={wideSocial}), загрузка отменена");
            snapshot.Brain.Dispose();
            return null;
        }

        return snapshot.Brain;
    }

    public bool MatchesArchitecture(int inputSize, int lstmHidden, bool wideSocial = false)
    {
        if (InputSize != inputSize || LstmHidden != lstmHidden || WideSocialExecutor != wideSocial) return false;

        int combined = inputSize + lstmHidden;
        if (!SizesMatch(CoordLayerSizes, BuildCoordSizes(combined))) return false;

        for (int i = 0; i < GoalCount; i++)
            if (!SizesMatch(ExecLayerSizes[i], BuildExecSizes(combined, i, wideSocial))) return false;

        return true;
    }

    private static bool SizesMatch(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}