using UnityEngine;

public class GPUTest : MonoBehaviour
{
    public ComputeShader computeShader;  // Переменная для хранения Compute Shader
    private ComputeBuffer computeBuffer; // Буфер для данных
    private int[] data;                // Массив данных
    private int kernelHandle, dataSize = 32;           // Размер массива данных

    void Start()
    {
        // Инициализируем массив данных
        data = new int[dataSize];
        for (int i = 0; i < dataSize; i++)
        {
            data[i] = i;  // Пример данных: 0, 1, 2, 3, ..., 63
        }

        // Создаем ComputeBuffer, который будет хранить наши данные
        computeBuffer = new ComputeBuffer(dataSize, sizeof(int));

        // Передаем данные в буфер
        computeBuffer.SetData(data);

        // Получаем индекс ядра (CSMain) из Compute Shader
        kernelHandle = computeShader.FindKernel("CSMain");

        // Передаем буфер в Compute Shader
        computeShader.SetBuffer(kernelHandle, "resultBuffer", computeBuffer);
    }

    void FixedUpdate()
    {
        // Запускаем вычисления на GPU
        computeShader.Dispatch(kernelHandle, dataSize / 32, 1, 1);

        // Считываем результаты обратно на CPU
        computeBuffer.GetData(data);

        for (int i = 0; i < dataSize; i++)
        {
            Debug.Log("Data[" + i + "] = " + data[i]);
        }
        Debug.Log(" ");
    }

    private void OnDestroy()
    {
        // Освобождаем буфер при завершении программы
        computeBuffer.Release();
    }
}
