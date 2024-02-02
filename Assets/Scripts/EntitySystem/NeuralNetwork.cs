using System;
using UnityEngine;

public class NeuralNetwork : MonoBehaviour
{
    private float[] inputs; // Входные данные
    private float[,] hiddenWeights; // Веса скрытого слоя
    private float[] hidden; // Выходы скрытого слоя
    private float[,] outputWeights; // Веса выходного слоя
    private float[] outputs; // Выходные данные

    private int inputSize;
    private int hiddenSize;
    private int outputSize;

    public NeuralNetwork(int inputSize, int hiddenSize, int outputSize)
    {
        this.inputSize = inputSize;
        this.hiddenSize = hiddenSize;
        this.outputSize = outputSize;

        inputs = new float[inputSize];
        hiddenWeights = new float[inputSize, hiddenSize];
        hidden = new float[hiddenSize];
        outputWeights = new float[hiddenSize, outputSize];
        outputs = new float[outputSize];

        InitializeWeights();
    }

    private void InitializeWeights()
    {
        // Инициализация весов случайными значениями
        for (int i = 0; i < inputSize; i++)
            for (int j = 0; j < hiddenSize; j++)
                hiddenWeights[i, j] = UnityEngine.Random.Range(-1f, 1f);

        for (int i = 0; i < hiddenSize; i++)
            for (int j = 0; j < outputSize; j++)
                outputWeights[i, j] = UnityEngine.Random.Range(-1f, 1f);
    }

    public float[] FeedForward(float[] inputValues)
    {
        inputs = inputValues;

        // Вычисление значений скрытого слоя
        for (int i = 0; i < hiddenSize; i++)
        {
            hidden[i] = 0;
            for (int j = 0; j < inputSize; j++)
                hidden[i] += inputs[j] * hiddenWeights[j, i];
            hidden[i] = Sigmoid(hidden[i]);
        }

        // Вычисление выходных значений
        for (int i = 0; i < outputSize; i++)
        {
            outputs[i] = 0;
            for (int j = 0; j < hiddenSize; j++)
                outputs[i] += hidden[j] * outputWeights[j, i];
            outputs[i] = Sigmoid(outputs[i]);
        }

        return outputs;
    }

    private float Sigmoid(float x)
    {
        return 1 / (1 + Mathf.Exp(-x));
    }
}

