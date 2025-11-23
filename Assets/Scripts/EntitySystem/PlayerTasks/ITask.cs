using UnityEngine;
using System;
public interface ITask
{
    Action<int> completeTask { get; set; }
    void PhaseManager();
    void StartTask(Vector3 parentPosition);
    void CompleteTask();
}
