using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    private List<int> taskCurrent = new List<int>();
    private Dictionary<int, ITask> tasksMap = new Dictionary<int, ITask>();
    public static TaskManager instance;
    private ITask task;

    private void Awake()
    {
        instance = this;
        task = new TaskOfDelivery();
        task.completeTask += DeactivateTask;
        tasksMap[1] = task;
    }

    public void ActivateTask(int ID, Vector3 parentPosition)
    {
        if (taskCurrent.Contains(ID))
        {
            tasksMap[ID].PhaseManager();
        }
        else
        {
            tasksMap[ID].StartTask(parentPosition);
            taskCurrent.Add(ID);
        }
    }

    public void DeactivateTask(int ID)
    {
        taskCurrent.Remove(ID);
    }

    public GameObject IInstantiate(string path)
    {
        return Instantiate(Resources.Load<GameObject>(path));
    }

}
