using System;
using System.IO;

public class DelayedPerceptronFactory : INeuralModelFactory<DelayedPerceptron>
{
    public DelayedPerceptron Deserialize(byte[] data)
    {
        return DelayedPerceptron.Deserialize(data);
    }
}

public partial class DelayedPerceptron : ISerializableNeuralModel
{
    
    private const string DefaultSavePath = "PerceptronModels";
    public byte[] Serialize()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        int[] layerSizes = new int[_activations.Length];
        for (int i = 0; i < _activations.Length; i++)
        {
            layerSizes[i] = _activations[i].Length;
        }
        
        writer.Write(layerSizes.Length);
        foreach (int size in layerSizes)
        {
            writer.Write(size);
        }
        
        writer.Write(DelaySteps);
        
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            for (int neuron = 0; neuron < _weights[layer].Length; neuron++)
            {
                for (int input = 0; input < _weights[layer][neuron].Length; input++)
                {
                    writer.Write(_weights[layer][neuron][input]);
                }
            }
        }
        
        for (int layer = 0; layer < _biases.Length; layer++)
        {
            for (int neuron = 0; neuron < _biases[layer].Length; neuron++)
            {
                writer.Write(_biases[layer][neuron]);
            }
        }
        
        writer.Write(_delayedList.Count);
        foreach (var item in _delayedList)
        {
            writer.Write(item.Input.Length);
            foreach (float value in item.Input)
            {
                writer.Write(value);
            }
            
            writer.Write(item.Predicted);
            writer.Write(item.Evaluation);
        }
        
        return memoryStream.ToArray();
    }
    
    public static DelayedPerceptron Deserialize(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        using var reader = new BinaryReader(memoryStream);
        
        int layersCount = reader.ReadInt32();
        int[] layerSizes = new int[layersCount];
        
        for (int i = 0; i < layersCount; i++)
        {
            layerSizes[i] = reader.ReadInt32();
        }
        
        int delaySteps = reader.ReadInt32();
        var perceptron = new DelayedPerceptron(
            layerSizes[0], 
            layerSizes[1], 
            layerSizes[2], 
            layerSizes[3], 
            layerSizes[4], 
            delaySteps
        );
        
        for (int layer = 0; layer < perceptron._weights.Length; layer++)
        {
            for (int neuron = 0; neuron < perceptron._weights[layer].Length; neuron++)
            {
                for (int input = 0; input < perceptron._weights[layer][neuron].Length; input++)
                {
                    perceptron._weights[layer][neuron][input] = reader.ReadSingle();
                }
            }
        }
        
        for (int layer = 0; layer < perceptron._biases.Length; layer++)
        {
            for (int neuron = 0; neuron < perceptron._biases[layer].Length; neuron++)
            {
                perceptron._biases[layer][neuron] = reader.ReadSingle();
            }
        }
        
        int delayedCount = reader.ReadInt32();
        for (int i = 0; i < delayedCount; i++)
        {
            int inputSize = reader.ReadInt32();
            float[] input = new float[inputSize];
            
            for (int j = 0; j < inputSize; j++)
            {
                input[j] = reader.ReadSingle();
            }
            
            int predicted = reader.ReadInt32();
            float evaluation = reader.ReadSingle();
            var item = new DelayedItem(input, predicted);
            item.Evaluation = evaluation;
            perceptron._delayedList.Add(item);
        }
        
        return perceptron;
    }
}