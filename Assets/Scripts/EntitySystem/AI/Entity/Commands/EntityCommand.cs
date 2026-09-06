using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public abstract class EntityCommand : ICommand
{
    protected readonly EntityModel model;
    private CancellationTokenSource cts;

    public EntityCommandStatus Status { get; private set; } = EntityCommandStatus.Completed;

    protected EntityCommand(EntityModel m) => model = m;

    public virtual bool CanExecute() => true;

    public async UniTask ExecuteAsync()
    {
        var decision = model.Brain.ActiveDecision;
        var local    = new CancellationTokenSource();
        cts    = local;
        Status = EntityCommandStatus.Started;

        float reward;
        var watchdog = WatchdogAsync(decision, local.Token);

        try
        {
            Status = EntityCommandStatus.Running;
            reward = await RunAsync(decision, local.Token);
            Status = EntityCommandStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            Status = EntityCommandStatus.Interrupted;
            reward = InterruptionReward;
        }
        catch (Exception)
        {
            Status = EntityCommandStatus.Failed;
            reward = FailureReward;
        }
        finally
        {
            local.Cancel();
            await watchdog;
            local.Dispose();
            cts = null;
        }

        model.Brain.CompleteDecision(decision.ExecDecisionId, reward, SimulationClock.Time, Status);
    }

    private async UniTask WatchdogAsync(BrainDecision decision, CancellationToken token)
    {
        float end = decision.EndTime;
        while (!token.IsCancellationRequested && SimulationClock.Time < end)
            await UniTask.Yield(PlayerLoopTiming.Update);

        if (!token.IsCancellationRequested) Cancel();
    }

    protected abstract UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct);

    protected virtual float InterruptionReward => -0.1f;
    protected virtual float FailureReward => -0.2f;

    public void Cancel()
    {
        var local = cts;
        if (local != null && !local.IsCancellationRequested) local.Cancel();
    }
}