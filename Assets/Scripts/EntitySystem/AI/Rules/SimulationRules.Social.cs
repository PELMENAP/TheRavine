using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField, FrameField] private float comfortMinStress = 0.25f;
    [SerializeField] private float comfortRelief = 0.5f;
    [SerializeField] private float comfortSelfShare = 0.3f;
    [SerializeField] private float comfortEnergyCost = 2f;
    [SerializeField] private float comfortDuration = 3f;
    [SerializeField] private float comfortPairCooldown = 20f;
    [SerializeField] private float comfortPairWindow = 120f;
    [SerializeField] private float comfortPairCap = 0.6f;
    [SerializeField] private float comfortTieEpsilon = 0.05f;
    [SerializeField] private float comfortReward = 0.5f;
    [SerializeField] private float feedRelief = 0.5f;
    [SerializeField] private float giveSelfShare = 0.5f;
    [SerializeField] private float helpReciprocalWindow = 60f;
    [SerializeField] private float giveTransferLoss = 0f;
    [SerializeField] private float restReliefRate = 0.004f;
    [SerializeField] private float restReliefCap = 0.012f;
    [SerializeField] private int helpLogSize = 8;
    [SerializeField] private float helpBalanceTau = 300f;

    public float ComfortMinStress => comfortMinStress;
    public float ComfortRelief => comfortRelief;
    public float ComfortSelfShare => comfortSelfShare;
    public float ComfortEnergyCost => comfortEnergyCost;
    public float ComfortDuration => comfortDuration;
    public float ComfortPairCooldown => comfortPairCooldown;
    public float ComfortPairWindow => comfortPairWindow;
    public float ComfortPairCap => comfortPairCap;
    public float ComfortTieEpsilon => comfortTieEpsilon;
    public float ComfortReward => comfortReward;
    public float FeedRelief => feedRelief;
    public float GiveSelfShare => giveSelfShare;
    public float HelpReciprocalWindow => helpReciprocalWindow;
    public float GiveTransferLoss => giveTransferLoss;
    public float RestReliefRate => restReliefRate;
    public float RestReliefCap => restReliefCap;
    public int HelpLogSize => helpLogSize;
    public float HelpBalanceTau => helpBalanceTau;

    private void ValidateSocial()
    {
        comfortMinStress = Mathf.Clamp01(comfortMinStress);
        comfortRelief = Mathf.Clamp01(comfortRelief);
        comfortSelfShare = Mathf.Max(comfortSelfShare, 0f);
        comfortEnergyCost = Mathf.Max(comfortEnergyCost, 0f);
        comfortDuration = Mathf.Max(comfortDuration, 0f);
        comfortPairCooldown = Mathf.Max(comfortPairCooldown, 0f);
        comfortPairWindow = Mathf.Max(comfortPairWindow, 1f);
        comfortPairCap = Mathf.Max(comfortPairCap, 0f);
        comfortTieEpsilon = Mathf.Max(comfortTieEpsilon, 0f);
        comfortReward = Mathf.Max(comfortReward, 0f);
        feedRelief = Mathf.Clamp01(feedRelief);
        giveSelfShare = Mathf.Max(giveSelfShare, 0f);
        helpReciprocalWindow = Mathf.Max(helpReciprocalWindow, 0f);
        giveTransferLoss = Mathf.Clamp01(giveTransferLoss);
        restReliefRate = Mathf.Max(restReliefRate, 0f);
        restReliefCap = Mathf.Max(restReliefCap, 0f);
        helpLogSize = Mathf.Clamp(helpLogSize, 1, HelpLog.MaxSize);
        helpBalanceTau = Mathf.Max(helpBalanceTau, 1f);
    }
}
