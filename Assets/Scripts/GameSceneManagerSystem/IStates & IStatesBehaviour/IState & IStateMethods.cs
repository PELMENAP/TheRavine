using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IState<out TInitializer> {
    TInitializer Initializer {get; }
}

public interface IEnterable {
    void OnEnter();
}

public interface ITickable {
    void OnTick();
}

public interface IExitable {
    void OnExit();   
}