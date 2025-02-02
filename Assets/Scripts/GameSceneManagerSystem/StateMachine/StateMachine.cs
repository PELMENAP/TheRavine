using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

public class StateMachine<TInitializer> : IDisposable
{
    public StateMachine(int _standardStateMachineTickTime, params IState<TInitializer>[] states)
    {
        this._standardStateMachineTickTime = _standardStateMachineTickTime;
        _states = new Dictionary<Type, IState<TInitializer>>(states.Length);
        foreach (var state in states)
            _states.Add(state.GetType(), state);
    }

    private int _standardStateMachineTickTime;
    private IState<TInitializer> _currentState;
    private readonly Dictionary<Type, IState<TInitializer>> _states;
    private bool _isTicking;

    public void SwitchState<TState>() where TState : IState<TInitializer>
    {
        _isTicking = false;
        TryExitPreviousState<TState>();
        GetNewState<TState>();
        TryEnterNewState<TState>();
        TryTickNewState<TState>();
    }

    private void TryExitPreviousState<TState>() where TState : IState<TInitializer>
    {
        if (_currentState is IExitable exitable)
            exitable.OnExit();
    }

    private void GetNewState<TState>() where TState : IState<TInitializer>
    {
        var newState = GetState<TState>();
        _currentState = newState;
    }

    private void TryEnterNewState<TState>() where TState : IState<TInitializer>
    {
        if (_currentState is IEnterable enterable)
            enterable.OnEnter();
    }

    private void TryTickNewState<TState>() where TState : IState<TInitializer>
    {
        if (_currentState is ITickable tickable)
        {
            _isTicking = true;
            StartTick(tickable).Forget();
        }
    }

    private async UniTaskVoid StartTick(ITickable tickable)
    {
        while (_isTicking)
        {
            tickable.OnTick();
            await UniTask.Delay(_standardStateMachineTickTime);
        }
    }

    private TState GetState<TState>() where TState : IState<TInitializer>
    {
        if (_states.TryGetValue(typeof(TState), out var state))
            return (TState)state;
        throw new InvalidOperationException($"State {typeof(TState).Name} not found.");
    }
    public void StopTicking() => _isTicking = false;

    public void Dispose()
    {
        _states.Clear();
    }
}
