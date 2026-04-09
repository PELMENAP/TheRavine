using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using R3;
using TMPro;
using UnityEngine;
using Random = TheRavine.Extensions.RavineRandom;

[RequireComponent(typeof(Rigidbody))]
public class Entity : MonoBehaviour, IDialogListener, IDialogSender
{
    [Header("Визуал")]
    [SerializeField] private TextMeshPro label;
    [SerializeField] private GameObject  prefab;

    [Header("Параметры")]
    private ReactiveProperty<float> maxHealth = new(200f);
    private ReactiveProperty<float> maxEnergy = new(200f);

    [SerializeField] private float currentHealth;
    [SerializeField] private float currentEnergy;
    [SerializeField] private float energyRegenRate = 5f;

    [Header("Передвижение")]
    [SerializeField] private float moveSpeed              = 3f;
    [SerializeField] private float runSpeed               = 5f;
    [SerializeField] private float energyCostPerSecondMoving  = 5f;
    [SerializeField] private float energyCostPerSecondRunning = 10f;

    [Header("Обнаружение")]
    [SerializeField] private float     detectionRadius = 5f;
    [SerializeField] private LayerMask entityLayer;
    [SerializeField] private LayerMask foodLayer;

    [Header("Атака")]
    [SerializeField] private float attackRange      = 1.5f;
    [SerializeField] private float attackDamage     = 10f;
    [SerializeField] private float attackCooldown   = 1f;
    [SerializeField] private float attackEnergyCost = 15f;

    [Header("Размножение")]
    [SerializeField] private float reproduceEnergyCost = 20f;
    [SerializeField] private float reproduceHealthCost = 10f;

    [Header("Блуждание")]
    [SerializeField] private float wanderRadius  = 5f;
    [SerializeField] private float minWanderTime = 1f;
    [SerializeField] private float maxWanderTime = 3f;
    [SerializeField] private float idleTime      = 2f;

    private SharedHierarchicalBrain _sharedBrain;
    private InputVectorizer         _vectorizer;
    private EntityBrainContext      _ctx;

    private Rigidbody _rb;
    [SerializeField] private SpriteRenderer _sr;
    private CancellationTokenSource _cts;

    private bool    _isMoving;
    private bool    _isAttacking;
    private bool    _canAttack = true;

    private string  _ownSpeech   = "";
    private string  _otherSpeech = "";

    private int     _lastActionIndex;
    private int     _timeOfDay;
    private float[] _lastInput;

    private readonly List<Vector2> _pointsOfInterest = new();
    private const int MaxPoints = 5;

    public event Action<Entity> OnDied;
    public event Action<Entity> OnReproduceRequest;

    public enum EntityAction
    {
        Idle = 0, Wander = 1, RememberPoint = 2, GoToPoint = 3,
        Attack = 4, Flee = 5, Eat = 6, Reproduce = 7, Speech = 8,
        Mimic = 9, Rest = 10, Threaten = 11, ShareFood = 12, 
    }

    public EntityBrainContext BrainContext => _ctx;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.freezeRotation         = true;

        currentHealth = maxHealth.Value / 2f;
        currentEnergy = maxEnergy.Value / 2f;

        DialogSystem.Instance.AddDialogListener(this);
    }
    public void Inject(
        SharedHierarchicalBrain brain,
        EntityBrainContext ctx)
    {
        _sharedBrain   = brain;
        _vectorizer    = new InputVectorizer(maxHealth, maxEnergy);
        _ctx           = ctx;
    }
    public void SetUpAsNew()
    {
        _cts = new CancellationTokenSource();
        StartLoops();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ctx?.ResetMemory();
        Die();
    }

    private void OnDestroy()
    {
        DialogSystem.Instance.RemoveDialogListener(this);
        _vectorizer?.Dispose();
    }

    private void StartLoops()
    {
        BehaviorLoopAsync(_cts.Token).Forget();
        RegenerateEnergyAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid BehaviorLoopAsync(CancellationToken ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: ct);
        while (!ct.IsCancellationRequested)
        {
            await PerformOneStepAsync(ct);
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: ct);
        }
    }

    private async UniTask PerformOneStepAsync(CancellationToken ct)
    {
        _timeOfDay = (_timeOfDay + 1) % 24;

        float inDanger    = ComputeDangerLevel();
        float timeToBreed = ComputeBreedReadiness();
        float enemyDist   = FindNearestEntityDistance();
        float foodDist    = FindNearestFoodDistance();

        _lastInput = _vectorizer.Vectorize(
            currentHealth, currentEnergy,
            _lastActionIndex, _timeOfDay,
            inDanger, timeToBreed,
            _otherSpeech, enemyDist, foodDist);

        _otherSpeech = "";

        int actionIndex  = _sharedBrain.Predict(_lastInput, _ctx);
        _lastActionIndex = actionIndex;
        // coordinatorEntropy = _sharedBrain.GetCoordinatorEntropy(_ctx);

        var action = (EntityAction)actionIndex;

        if (label != null)
            label.text = $"{_ctx.CurrentGoal} - {action}\n"
                       + $"{(int)currentHealth} HP / {(int)currentEnergy} ENERGY\n"
                       + _ownSpeech;

        if (!CanPerformAction(action))
        {
            _sharedBrain.GiveReward(0.25f, _ctx);
            currentHealth -= 3f;
            return;
        }

        await PerformAction(action, ct);
    }

    private async UniTask PerformAction(EntityAction action, CancellationToken ct)
    {
        if (currentHealth <= 0) { Die(); return; }

        switch (action)
        {
            case EntityAction.Idle:          await IdleAsync(ct);          break;
            case EntityAction.Wander:        await WanderAsync(ct);        break;
            case EntityAction.RememberPoint: RememberCurrentPosition();    break;
            case EntityAction.GoToPoint:     await GoToPointAsync(ct);     break;
            case EntityAction.Attack:        await AttackAsync(ct);        break;
            case EntityAction.Flee:          await FleeAsync(ct);          break;
            case EntityAction.Eat:           await EatAsync(ct);           break;
            case EntityAction.Reproduce:     await ReproduceAsync(ct);     break;
            case EntityAction.Speech:        await SpeechAsync(ct);        break;
            case EntityAction.Mimic:         await MimicAsync(ct);         break;
            case EntityAction.Rest:          await RestAsync(ct);          break;
            case EntityAction.Threaten:      await ThreatenAsync(ct);      break;
            case EntityAction.ShareFood:     await ShareFoodAsync(ct);     break;
        }
    }

    private async UniTask IdleAsync(CancellationToken ct)
    {
        _isMoving          = false;
        _rb.linearVelocity = Vector2.zero;
        _sharedBrain.GiveReward(0.5f, _ctx);
        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: ct);
    }

    private async UniTask WanderAsync(CancellationToken ct)
    {
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle; 
        Vector3 dir = new Vector3(randomCircle.x, 0, randomCircle.y).normalized;
        
        Vector3 target = transform.position + dir * wanderRadius;
        await MoveToAsync(target, moveSpeed, Random.RangeFloat(minWanderTime, maxWanderTime),
            energyCostPerSecondMoving, ct);
    }

    private void RememberCurrentPosition()
    {
        Vector2 pos = transform.position;
        if (_pointsOfInterest.Count >= MaxPoints) _pointsOfInterest.RemoveAt(0);

        if (_pointsOfInterest.Count > 0 && Vector2.Distance(_pointsOfInterest[0], pos) < 10f)
        {
            _sharedBrain.GiveReward(0.3f, _ctx);
            return;
        }

        _pointsOfInterest.Add(pos);
        _sharedBrain.GiveReward(0.65f, _ctx);
        StartCoroutine(FlashColor(Color.cyan, 0.3f));
    }

    private async UniTask GoToPointAsync(CancellationToken ct)
    {
        if (_pointsOfInterest.Count == 0) return;
        int     idx    = Random.RangeInt(0, _pointsOfInterest.Count);
        Vector2 target = _pointsOfInterest[idx];
        Color   orig   = _sr.color;
        _sr.color      = Color.blue;
        await MoveToAsync(target, moveSpeed, 5f, energyCostPerSecondMoving, ct);
        _sr.color = orig;
        _sharedBrain.GiveReward(0.55f, _ctx);
    }

    private async UniTask AttackAsync(CancellationToken ct)
    {
        GameObject target = FindNearbyEntity();
        if (target == null) { _sharedBrain.GiveReward(0.2f, _ctx); return; }

        Color orig = _sr.color;
        _sr.color  = Color.red;
        await MoveToAsync(target.transform.position, moveSpeed, 2f, energyCostPerSecondMoving, ct);

        if (target != null &&
            Vector2.Distance(transform.position, target.transform.position) <= attackRange &&
            _canAttack)
        {
            currentEnergy -= attackEnergyCost;
            _isAttacking   = true;
            _canAttack     = false;

            var victim = target.GetComponent<Entity>();
            if (victim != null)
            {
                victim.TakeDamage(attackDamage);
                currentHealth += attackEnergyCost * 1.5f;
                _sharedBrain.GiveReward(0.9f, _ctx);
            }
            else { _sharedBrain.GiveReward(0.4f, _ctx); }

            await UniTask.Delay(TimeSpan.FromSeconds(attackCooldown), cancellationToken: ct);
            _isAttacking = false;
            _canAttack   = true;
        }
        else { _sharedBrain.GiveReward(0.3f, _ctx); }

        _sr.color = orig;
    }

    private async UniTask FleeAsync(CancellationToken ct)
    {
        DialogSystem.Instance.UpdateListenerPosition(this);
        GameObject nearest = FindNearbyEntity();
        if (nearest == null) { _sharedBrain.GiveReward(0.3f, _ctx); return; }

        Color   orig   = _sr.color;
        _sr.color      = Color.green;
        Vector2 away   = ((Vector2)transform.position - (Vector2)nearest.transform.position).normalized;
        Vector2 target = (Vector2)transform.position + away * detectionRadius * 1.5f;
        await MoveToAsync(target, runSpeed, 2f, energyCostPerSecondRunning, ct);

        float dist = 1f;
        if(nearest != null)
        {
            dist = Vector2.Distance(transform.position, nearest.transform.position);
        }
        _sharedBrain.GiveReward(Mathf.Clamp01(dist / detectionRadius), _ctx);
        _sr.color = orig;
    }

    private async UniTask EatAsync(CancellationToken ct)
    {
        Color orig = _sr.color;
        _sr.color  = Color.magenta;

        Collider2D food = Physics2D.OverlapCircle(transform.position, detectionRadius, foodLayer);
        if (food != null)
        {
            currentHealth = Mathf.Min(currentHealth + 30f, maxHealth.Value);
            currentEnergy = Mathf.Min(currentEnergy + 20f, maxEnergy.Value);
            _sharedBrain.GiveReward(0.85f, _ctx);
            Destroy(food.gameObject);
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth + 5f,  maxHealth.Value);
            currentEnergy = Mathf.Min(currentEnergy + 5f,  maxEnergy.Value);
            _sharedBrain.GiveReward(0.35f, _ctx);
        }

        _sr.color = orig;
        await UniTask.Yield(ct);
    }

    private async UniTask ReproduceAsync(CancellationToken ct)
    {
        currentEnergy -= reproduceEnergyCost;
        currentHealth -= reproduceHealthCost;

        OnReproduceRequest?.Invoke(this);

        _sharedBrain.GiveReward(0.8f, _ctx);
        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: ct);
    }

    private async UniTask SpeechAsync(CancellationToken ct)
    {
        _ownSpeech = _vectorizer.HashFloatArray(_lastInput);
        DialogSystem.Instance.OnSpeechSend(this, _ownSpeech);
        currentEnergy -= 5f;
        _sharedBrain.GiveReward(0.55f, _ctx);
        await UniTask.Yield(ct);
    }

    private async UniTask MimicAsync(CancellationToken ct)
    {
        GameObject nearest = FindNearbyEntity();
        if (nearest == null) { _sharedBrain.GiveReward(0.2f, _ctx); return; }

        var other = nearest.GetComponent<Entity>();
        if (other == null) { _sharedBrain.GiveReward(0.2f, _ctx); return; }

        int mimicAction = other._lastActionIndex;

        float reward = 0.3f + other._ctx.CoordMLP.AverageEntropy * 0.2f;
        _sharedBrain.GiveReward(reward, _ctx);

        Color orig = _sr.color;
        _sr.color  = Color.Lerp(_sr.color, other._sr.color, 0.5f);

        _lastActionIndex = mimicAction;
        await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
        _sr.color = orig;
    }

    private async UniTask RestAsync(CancellationToken ct)
    {
        bool isNight = _timeOfDay >= 20 || _timeOfDay <= 5;

        if (!isNight)
        {
            _sharedBrain.GiveReward(0.1f, _ctx); // штраф за сон днём
            await UniTask.Yield(ct);
            return;
        }

        _isMoving          = false;
        _rb.linearVelocity = Vector2.zero;
        Color orig = _sr.color;
        _sr.color  = new Color(0.3f, 0.3f, 0.6f);

        float elapsed = 0f;
        float restDuration = 3f;
        while (elapsed < restDuration && !ct.IsCancellationRequested)
        {
            currentHealth = Mathf.Min(currentHealth + 5f * Time.deltaTime, maxHealth.Value);
            currentEnergy = Mathf.Min(currentEnergy + 8f * Time.deltaTime, maxEnergy.Value);
            elapsed       += Time.deltaTime;
            await UniTask.Yield(ct);
        }

        _sr.color = orig;
        _sharedBrain.GiveReward(0.7f, _ctx);
    }

    private async UniTask ThreatenAsync(CancellationToken ct)
    {
        GameObject target = FindNearbyEntity();
        if (target == null) { _sharedBrain.GiveReward(0.2f, _ctx); return; }

        float dist = Vector2.Distance(transform.position, target.transform.position);
        if (dist > attackRange * 2f) { _sharedBrain.GiveReward(0.15f, _ctx); return; }

        Color orig = _sr.color;
        _sr.color  = new Color(1f, 0.5f, 0f);

        DialogSystem.Instance.OnSpeechSend(this, "THREAT:" + _vectorizer.HashFloatArray(_lastInput));
        currentEnergy -= 3f;

        float threatReward = dist < attackRange ? 0.6f : 0.4f;
        _sharedBrain.GiveReward(threatReward, _ctx);

        await UniTask.Delay(TimeSpan.FromSeconds(0.8f), cancellationToken: ct);
        _sr.color = orig;
    }

    private async UniTask ShareFoodAsync(CancellationToken ct)
    {
        if (currentHealth < 80f) { _sharedBrain.GiveReward(0.1f, _ctx); return; }

        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, detectionRadius, entityLayer);
        Entity     best = null;
        float        minH = float.MaxValue;

        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;
            var other = col.GetComponent<Entity>();
            if (other != null && other.currentHealth < minH)
            {
                minH = other.currentHealth;
                best = other;
            }
        }

        if (best == null || minH > currentHealth * 0.8f)
        {
            _sharedBrain.GiveReward(0.25f, _ctx);
            return;
        }

        float transfer = Mathf.Min(20f, currentHealth - 60f);
        currentHealth      -= transfer;
        best.currentHealth  = Mathf.Min(best.currentHealth + transfer, best.maxHealth.Value);

        StartCoroutine(FlashColor(Color.yellow, 0.3f));
        best.StartCoroutine(best.FlashColor(Color.yellow, 0.3f));

        float needFactor = 1f - Mathf.Clamp01(minH / best.maxHealth.Value);
        _sharedBrain.GiveReward(0.5f + needFactor * 0.35f, _ctx);

        await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: ct);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        StartCoroutine(FlashColor(Color.red, 0.2f));
        if (currentHealth <= 0) Die();
    }
    private void Die()
    {
        _cts?.Cancel();
        if (_sr) _sr.color = Color.gray;
        if (_rb) { _rb.linearVelocity = Vector2.zero; }

        DialogSystem.Instance.RemoveDialogListener(this);
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }

    public float   GetDialogDistance()  => 20f;
    public Vector3 GetCurrentPosition() => transform ? transform.position : Vector3.zero;
    public void    OnSpeechGet(IDialogSender sender, string message) => _otherSpeech = message;
    public void    OnDialogGetRequire() { }

    private bool CanPerformAction(EntityAction action) => action switch
    {
        EntityAction.Attack    => currentEnergy >= attackEnergyCost && _canAttack,
        EntityAction.Wander    => currentEnergy > 0,
        EntityAction.Flee      => currentEnergy > 0,
        EntityAction.Speech    => currentEnergy > 0,
        EntityAction.GoToPoint => _pointsOfInterest.Count > 0 && currentEnergy > 0,
        EntityAction.Reproduce => currentEnergy >= reproduceEnergyCost && currentHealth >= reproduceHealthCost,
        _                      => true,
    };

    private float ComputeDangerLevel()
    {
        float d = 0f;
        if (currentEnergy < 50 || currentHealth < 50)  d += 0.25f;
        if (currentEnergy < 25 || currentHealth < 25)  d += 0.25f;
        if (currentEnergy < 10 || currentHealth < 10)  d += 0.5f;
        return d;
    }

    private float ComputeBreedReadiness()
    {
        float b = 0f;
        if (currentEnergy > reproduceEnergyCost && currentHealth > reproduceHealthCost)           b += 0.5f;
        if (currentEnergy > reproduceEnergyCost + 50 && currentHealth > reproduceHealthCost + 50) b += 0.5f;
        return b;
    }

    private async UniTask MoveToAsync(Vector3 target, float speed, float maxDuration, 
    float energyCost, CancellationToken ct)
    {
        _isMoving = true;
        float startTime = Time.time;

        while (Vector3.Distance(transform.position, target) > 0.1f && 
            Time.time - startTime < maxDuration)
        {
            if (currentEnergy <= 0) break;

            Vector3 direction = (target - transform.position).normalized;
            direction.y = 0;

            _rb.linearVelocity = direction * speed;
            currentEnergy = Mathf.Max(0, currentEnergy - energyCost * Time.deltaTime);
            await UniTask.Yield(ct);
        }

        _isMoving = false;
        _rb.linearVelocity = Vector3.zero;
    }

    private async UniTaskVoid RegenerateEnergyAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_isMoving && !_isAttacking && currentEnergy < maxEnergy.Value)
                currentEnergy = Mathf.Min(currentEnergy + energyRegenRate * Time.deltaTime, maxEnergy.Value);

            if (currentEnergy < 5f) { currentHealth -= 15f; currentEnergy += 5f; }

            await UniTask.Yield(ct);
        }
    }

    private GameObject FindNearbyEntity()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, detectionRadius, entityLayer);
        GameObject best = null;
        float minD = float.MaxValue;

        foreach (var col in cols)
        {
            if (col.gameObject == gameObject) continue;
            // Расстояние в 3D считается так же, но Vector3.Distance
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < minD) { minD = d; best = col.gameObject; }
        }
        return best;
    }

    private float FindNearestEntityDistance()
    {
        var go = FindNearbyEntity();
        return go != null ? Vector2.Distance(transform.position, go.transform.position) : -1f;
    }

    private float FindNearestFoodDistance()
    {
        Collider2D food = Physics2D.OverlapCircle(transform.position, detectionRadius, foodLayer);
        return food != null ? Vector2.Distance(transform.position, food.transform.position) : -1f;
    }

    private IEnumerator FlashColor(Color flash, float duration)
    {
        Color orig = _sr.color;
        _sr.color  = flash;
        yield return new WaitForSeconds(duration);
        _sr.color  = orig;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        foreach (var p in _pointsOfInterest) Gizmos.DrawSphere(p, 0.3f);
    }
}