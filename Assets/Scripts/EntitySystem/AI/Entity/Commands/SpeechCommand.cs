using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;

public class SpeechCommand : EntityCommand
{
    private CancellationTokenSource _cts;
    private int  _generation;
    private bool _playing;
    private bool _failed;

    public SpeechCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.SpeechEnergyCost);

        model.Broadcast(decision.Speech);
        ApplySignal(r);

        string hash = SpeechComponent.Encode(decision.Speech);
        DialogSystem.Instance.OnSpeechSend((IDialogSender)model.Motor, hash);

        if (_cts == null || _cts.IsCancellationRequested)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        _failed = false;
        PlayAsync(hash, ++_generation, _cts.Token).Forget();
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (_playing) return EntityCommandStatus.Running;
        return _failed ? Fail() : Complete();
    }

    private void ApplySignal(SimulationRules r)
    {
        switch (decision.Signal)
        {
            case SpeechSignal.Alarm:
                if (model.HasDirectThreat) model.Nest?.RaiseAlarm(r.AlarmCryAmount);
                break;
            case SpeechSignal.Food:
                if (model.TryGetFoodSite(out float2 site)) model.RecruitNearby(site, r.RecruitCount, r.SpeechRadius);
                break;
            case SpeechSignal.FollowMe:
                model.RecruitNearby(model.Position2D, r.RecruitCount, r.SpeechRadius);
                break;
        }
    }

    protected override void OnCancel()
    {
        if (_playing) _cts?.Cancel();
        _playing = false;
    }

    private async UniTaskVoid PlayAsync(string hash, int generation, CancellationToken ct)
    {
        _playing = true;
        try
        {
            await model.Speech.PlayAsync(
                hash, model.Stats.Hp, model.Stats.En,
                0f, 0f, model.LastActionIndex, model.CachedNearestDistance, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (generation == _generation) _failed = true;
        }
        finally
        {
            if (generation == _generation) _playing = false;
        }
    }
}
