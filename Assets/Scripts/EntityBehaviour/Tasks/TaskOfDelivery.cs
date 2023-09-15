using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskOfDelivery : ITask
{
    private const string Task_path = "UnluckyDelivery", Task_path1 = "QuestBot", Task_path2 = "Box";
    public Action<int> completeTask { get; set; }
    private GameObject task;
    private Vector3 position, direction;
    public void StartTask(Vector3 parentPosition)
    {
        task = TaskManager.instance.IInstantiate(Task_path);
        direction = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f);
        direction.Normalize();
        task.transform.position = position = parentPosition + direction * UnityEngine.Random.Range(190, 200);
        task.GetComponent<TaskRequire>().findOfDelivery += Phase1;
    }
    public void PhaseManager()
    {
        task = TaskManager.instance.IInstantiate(Task_path2);
        direction = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f);
        direction.Normalize();
        task.transform.position = position + direction * UnityEngine.Random.Range(40, 60);
    }
    public void CompleteTask()
    {
        completeTask?.Invoke(1);
    }

    private void Phase1()
    {
        task = TaskManager.instance.IInstantiate(Task_path1);
        direction = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f);
        direction.Normalize();
        task.transform.position = position + direction * UnityEngine.Random.Range(100, 120);
    }
}
