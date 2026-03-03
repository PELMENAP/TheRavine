using System;
using System.Collections.Generic;

/// <summary>
/// Гибридная модель: LSTM обрабатывает временной контекст,
/// DelayedPerceptron принимает решение на основе текущего входа + скрытого состояния LSTM.
///
/// Поток данных:
///   input(N) → LSTM(hiddenSize) → h(t)
///   [input(N) | h(t)] → MLP → action
///
/// Размерности:
///   inputSize    — исходный вектор (например, 64)
///   lstmHidden   — скрытое состояние LSTM (например, 16)
///   combinedSize — inputSize + lstmHidden (например, 80) — вход в MLP
/// </summary>
public class LSTMPerceptronHybrid
{
    private readonly LSTMMemory        _lstm;
    private readonly DelayedPerceptron _mlp;
    private readonly float[]           _combinedInput;
    private readonly int               _inputSize;
    private readonly int               _lstmHiddenSize;
    public LSTMPerceptronHybrid(
        int inputSize,
        int lstmHidden,
        int h1, int h2, int h3,
        int outputSize,
        int delaySteps = 5)
    {
        _inputSize      = inputSize;
        _lstmHiddenSize = lstmHidden;

        _lstm          = new LSTMMemory(inputSize, lstmHidden);
        _combinedInput = new float[inputSize + lstmHidden];

        _mlp = new DelayedPerceptron(
            inputSize + lstmHidden,
            h1, h2, h3,
            outputSize,
            delaySteps);
    }
    public LSTMPerceptronHybrid(LSTMPerceptronHybrid parent)
    {
        _inputSize      = parent._inputSize;
        _lstmHiddenSize = parent._lstmHiddenSize;

        _lstm          = new LSTMMemory(_inputSize, _lstmHiddenSize);
        _combinedInput = new float[_inputSize + _lstmHiddenSize];

        _mlp = new DelayedPerceptron(parent._mlp);
    }
    public int Predict(float[] input, float epsilon = 0.1f)
    {
        BuildCombinedInput(input);
        return _mlp.Predict(_combinedInput, epsilon);
    }

    public List<DelayedItem> DelayedList => _mlp.DelayedList;

    public void ResetMemory() => _lstm.ResetState();

    public GeneticParameters GetGeneticParameters() => _mlp.GetGeneticParameters();

    public float AverageEntropy  => _mlp.AverageEntropy;
    public int   TrainingSteps   => _mlp.TrainingSteps;

    private void BuildCombinedInput(float[] input)
    {
        float[] lstmHidden = _lstm.Step(input);

        Array.Copy(input,      0, _combinedInput, 0,           _inputSize);
        Array.Copy(lstmHidden, 0, _combinedInput, _inputSize,  _lstmHiddenSize);
    }
}