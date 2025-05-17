using System.Collections;
using System.Collections.Generic;
using UnityEngine;


using Cysharp.Threading.Tasks;

public class SimpleEntityController : MonoBehaviour
{
    private DelayedPerceptron delayedPerceptron;
    public string file;
    public List<Entity2D> entities;

    public bool newmode;
    private async void Start()
    {
        if(newmode)
        {
            BehaviorLoopAsync().Forget();
            return;
        }
        delayedPerceptron = await DelayedPerceptronStorage.LoadAsync(file);

        foreach (var item in entities)
        {
            item.SetUp(delayedPerceptron);
        }
    }

    private async UniTaskVoid BehaviorLoopAsync()
    {
        await UniTask.Delay(3000);
        

        foreach (var item in entities)
        {
            item.SetUpAsNew();
        }
    }
}
