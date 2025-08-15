using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using NaughtyAttributes;

using Random = TheRavine.Extensions.RavineRandom;
using TMPro;
using R3;
public class Entity2D : MonoBehaviour, IDialogListener, IDialogSender
{
    [Header("Визуал")]
    [SerializeField] private TextMeshPro text;
    [SerializeField] private GameObject prefab;

    [Header("Параметры")]

    private ReactiveProperty<float> maxHealth = new(200), maxEnergy = new(200);
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentEnergy;
    [SerializeField] private float energyRegenRate = 5f;
    
    [Header("Передвижение")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float energyCostPerSecondMoving = 5f;
    [SerializeField] private float energyCostPerSecondRunning = 10f;
    
    [Header("Обнаружение")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private LayerMask entityLayer;
    [SerializeField] private LayerMask foodLayer;
    
    [Header("Атака")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float attackEnergyCost = 15f, reproduceEnergyCost = 20f, reproduceHealthCost = 10f;
    
    [Header("Поведение")]
    [SerializeField] private float wanderRadius = 5f;
    [SerializeField] private float minWanderTime = 1f;
    [SerializeField] private float maxWanderTime = 3f;
    [SerializeField] private float idleTime = 2f;
    
    private bool isMoving = false;
    private bool isAttacking = false;
    private bool canAttack = true;
    private Vector2 targetPosition;
    private CancellationTokenSource actionCts;
    
    private List<Vector2> pointsOfInterest = new List<Vector2>();
    private int maxPointsOfInterest = 5;
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private InputVectorizer inputVectorizer;
    private DelayedPerceptron delayedPerceptron;
    private int currentState, timeOfDay;
    
    // Перечисление для возможных действий
    public enum EntityAction
    {
        Idle,           // Стоять
        Wander,         // Бродить без цели
        RememberPoint,  // Запомнить текущую позицию как точку интереса
        GoToPoint,      // Идти в случайную точку интереса
        Attack,         // Нападать на сущность
        Flee,           // Убегать от сущности
        Eat,            // Есть еду
        Reproduce,      // Размножаться
        Speech,
    }
    
    // Текущее действие
    private EntityAction currentAction = EntityAction.Idle;
    
    public string file = "default";
    [Button]
    private async void Save()
    {
        await DelayedPerceptronStorage.SaveAsync(delayedPerceptron, file);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;
        
        actionCts = new CancellationTokenSource();
        
        currentHealth = maxHealth.Value / 2;
        currentEnergy = maxEnergy.Value / 2;

        inputVectorizer = new(maxHealth, maxEnergy);
    }

    public void SetUp(DelayedPerceptron other)
    {
        delayedPerceptron = new DelayedPerceptron(other);

        BehaviorLoopAsync(actionCts.Token).Forget();
        
        RegenerateEnergyAsync(actionCts.Token).Forget();
    }

    public void SetUpAsNew()
    {
        delayedPerceptron = new(inputVectorizer.GetVectorSize(), 16, 16, 16, Enum.GetValues(typeof(EntityAction)).Length);

        BehaviorLoopAsync(actionCts.Token).Forget();
        
        RegenerateEnergyAsync(actionCts.Token).Forget();
    }
    
    private void OnDisable()
    {
        actionCts?.Cancel();
        actionCts?.Dispose();
        actionCts = new CancellationTokenSource();
    }
    
    private async UniTaskVoid BehaviorLoopAsync(CancellationToken cancellationToken)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await PerformRandomAction(cancellationToken);
            
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: cancellationToken);
        }
    }
    
    private int predictedIndex;
    private async UniTask PerformRandomAction(CancellationToken cancellationToken)
    {
        timeOfDay = (timeOfDay + 1) % 24;

        float indanger = 0f;
        if(currentEnergy < 50 || currentHealth < 50) indanger += 0.25f;
        if(currentEnergy < 25 || currentHealth < 25) indanger += 0.25f;
        if(currentEnergy < 10 || currentHealth < 10) indanger += 0.5f;

        float timetobreed = 0f;
        if(currentEnergy > reproduceEnergyCost && currentHealth > reproduceHealthCost) timetobreed += 0.5f;
        if(currentEnergy > reproduceEnergyCost + 50 && currentHealth > reproduceHealthCost + 50) timetobreed += 0.5f;

        float[] input = inputVectorizer.Vectorize(currentHealth, currentEnergy, predictedIndex, currentState, timeOfDay, true, indanger, timetobreed);
        predictedIndex = delayedPerceptron.Predict(input);

        EntityAction randomAction = (EntityAction)predictedIndex;
        text.text = randomAction.ToString() + " \n" + ((int)currentHealth).ToString() + "|" + ((int)currentEnergy).ToString();
        await PerformAction(randomAction, cancellationToken);

        bool theSame = true;
        for(int i = 0; i < delayedPerceptron.DelayedList.Count / 2; i++)
        {
            var firstComponent = delayedPerceptron.DelayedList[0];
            var currentComponent = delayedPerceptron.DelayedList[i];
            if(currentComponent.Predicted != firstComponent.Predicted || currentComponent.Evaluation != firstComponent.Evaluation)
            {
                theSame = false;
                break;
            } 
        }

        if(theSame && delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.4f;
    }
    
    public async UniTask PerformAction(EntityAction action, CancellationToken cancellationToken)
    {
        if (currentHealth <= 0)
        {
            Die();
        }
        if (!CanPerformAction(action))
        {
            if(delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.3f;
            currentHealth -= 5;
            return;
        }
        
        currentAction = action;
        
        switch (action)
        {
            case EntityAction.Idle:
                await IdleAsync(cancellationToken);
                break;
                
            case EntityAction.Wander:
                await WanderAsync(cancellationToken);
                break;
                
            case EntityAction.RememberPoint:
                RememberCurrentPosition();
                break;
                
            case EntityAction.GoToPoint:
                await GoToPointOfInterestAsync(cancellationToken);
                break;
                
            case EntityAction.Attack:
                await AttackNearbyEntityAsync(cancellationToken);
                break;
                
            case EntityAction.Flee:
                await FleeFromNearbyEntityAsync(cancellationToken);
                break;
                
            case EntityAction.Eat:
                await EatNearbyFoodAsync(cancellationToken);
                break;

            case EntityAction.Reproduce:
                await ReproduceAsync(cancellationToken);
                break;

            case EntityAction.Speech:
                await SpeechAsync(cancellationToken);
                break;
        }
    }

    private async UniTask ReproduceAsync(CancellationToken cancellationToken)
    {
        if(delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.8f;
        
        Vector2 randomDirection = Random.GetInsideCircle().normalized;
        Vector2 wanderTarget = (Vector2)transform.position + randomDirection * wanderRadius / 2;

        GameObject nextGeneration = Instantiate(prefab, (Vector3)wanderTarget, Quaternion.identity, this.transform.parent);
        Entity2D entity2D = nextGeneration.GetComponent<Entity2D>();
        entity2D.SetUp(delayedPerceptron);

        currentEnergy -= reproduceEnergyCost;
        currentHealth -= reproduceHealthCost;

        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: cancellationToken);
    }

    public float GetDialogDistance()
    {
        return dialogDistance;
    }
    public Vector3 GetCurrentPosition(){
        return transform.position;
    }
    public void OnDialogGetRequire()
    {
        playerText.interactable = true;
        playerText.Select();
        playerText.text = "";
    }

    private async UniTask SpeechAsync(CancellationToken cancellationToken)
    {

        currentEnergy -= 5f;

        await UniTask.Yield(cancellationToken);
    }
    
    private bool CanPerformAction(EntityAction action)
    {
        switch (action)
        {
            case EntityAction.Attack:
                return currentEnergy >= attackEnergyCost && canAttack;
                
            case EntityAction.Wander:
                return currentEnergy > 0;
            case EntityAction.Flee:
                return currentEnergy > 0;
            case EntityAction.Speech:
                return currentEnergy > 0;
                
            case EntityAction.GoToPoint:
                return pointsOfInterest.Count > 0 && currentEnergy > 0;
                
            case EntityAction.Eat:
                return FindNearbyFood();
                
            case EntityAction.Idle:
                return true;
            case EntityAction.RememberPoint:
                return true;
            case EntityAction.Reproduce:
                return currentEnergy >= reproduceEnergyCost && currentHealth >= reproduceHealthCost;

            
                
            default:
                return true;
        }
    }
    
    private async UniTask IdleAsync(CancellationToken cancellationToken)
    {
        isMoving = false;
        rb.velocity = Vector2.zero;
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        
        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    private async UniTask WanderAsync(CancellationToken cancellationToken)
    {
        Vector2 randomDirection = Random.GetInsideCircle().normalized;
        Vector2 wanderTarget = (Vector2)transform.position + randomDirection * wanderRadius;
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.yellow;
        
        float wanderTime = Random.RangeFloat(minWanderTime, maxWanderTime);
        await MoveToPositionAsync(wanderTarget, moveSpeed, wanderTime, energyCostPerSecondMoving, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    private void RememberCurrentPosition()
    {
        Vector2 currentPos = transform.position;
        
        if (pointsOfInterest.Count >= maxPointsOfInterest)
        {
            pointsOfInterest.RemoveAt(0);
        }

        if(pointsOfInterest.Count > 0 && Vector2.Distance(pointsOfInterest[0], currentPos) < 10f)
        {
            return;
        }
        
        pointsOfInterest.Add(currentPos);
        StartCoroutine(FlashColor(Color.cyan, 0.3f));
    }
    
    private async UniTask GoToPointOfInterestAsync(CancellationToken cancellationToken)
    {
        if (pointsOfInterest.Count == 0)
        {
            return;
        }
        int randomIndex = Random.RangeInt(0, pointsOfInterest.Count);
        Vector2 targetPoint = pointsOfInterest[randomIndex];
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.blue;
        
        await MoveToPositionAsync(targetPoint, moveSpeed, 5f, energyCostPerSecondMoving, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    private async UniTask AttackNearbyEntityAsync(CancellationToken cancellationToken)
    {
        GameObject nearestEntity = FindNearbyEntity();
        
        if (nearestEntity == null)
        {
            return;
        }
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        
        Entity2D targetEntity = nearestEntity.GetComponent<Entity2D>();
        Vector2 targetPosition = nearestEntity.transform.position;
        
        await MoveToPositionAsync(targetPosition, moveSpeed, 2f, energyCostPerSecondMoving, cancellationToken);
        
        if(transform == null || nearestEntity == null) return;
        if (Vector2.Distance(transform.position, nearestEntity.transform.position) <= attackRange && canAttack)
        {
            currentEnergy -= attackEnergyCost;
            
            isAttacking = true;
            canAttack = false;
            
            if (targetEntity != null)
            {
                targetEntity.TakeDamage(attackDamage);
                currentHealth += attackEnergyCost * 2;
            }
            
            await UniTask.Delay(TimeSpan.FromSeconds(attackCooldown), cancellationToken: cancellationToken);
            
            isAttacking = false;
            canAttack = true;
        }
        
        spriteRenderer.color = originalColor;
    }
    private async UniTask FleeFromNearbyEntityAsync(CancellationToken cancellationToken)
    {
        GameObject nearestEntity = FindNearbyEntity();
        
        if (nearestEntity == null)
        {
            return;
        }
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.green;
        Vector2 fleeDirection = ((Vector2)transform.position - (Vector2)nearestEntity.transform.position).normalized;
        Vector2 fleeTarget = (Vector2)transform.position + fleeDirection * (detectionRadius * 1.5f);
        
        await MoveToPositionAsync(fleeTarget, runSpeed, 2f, energyCostPerSecondRunning, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }

    private async UniTask EatNearbyFoodAsync(CancellationToken cancellationToken)
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.magenta;

        currentHealth = Mathf.Min(currentHealth + 20f, maxHealth.Value);
        currentEnergy = Mathf.Min(currentEnergy - 10f, maxEnergy.Value);

        spriteRenderer.color = originalColor;

        await UniTask.Yield();
    }
    
    private async UniTask MoveToPositionAsync(Vector2 position, float speed, float maxMoveDuration, float energyCostPerSecond, CancellationToken cancellationToken)
    {
        isMoving = true;
        float startTime = Time.time;
        float journeyDuration = 0f;
        
        while (Vector2.Distance(transform.position, position) > 0.1f && journeyDuration < maxMoveDuration)
        {
            if (currentEnergy <= 0)
            {
                isMoving = false;
                rb.velocity = Vector2.zero;
                return;
            }
            
            Vector2 direction = ((Vector2)position - (Vector2)transform.position).normalized;
            rb.velocity = direction * speed;
            
            currentEnergy -= energyCostPerSecond * Time.deltaTime;
            currentEnergy = Mathf.Max(0, currentEnergy);
            
            journeyDuration = Time.time - startTime;
            
            await UniTask.Yield(cancellationToken);
        }
        
        isMoving = false;
        rb.velocity = Vector2.zero;
    }
    
    private async UniTaskVoid RegenerateEnergyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!isMoving && !isAttacking && currentEnergy < maxEnergy.Value)
            {
                currentEnergy += energyRegenRate * Time.deltaTime;
                currentEnergy = Mathf.Min(currentEnergy, maxEnergy.Value);
            }

            if(currentEnergy < 5f)
            {
                currentHealth -= 15f;
                currentEnergy += 5f;
            } 
            
            await UniTask.Yield(cancellationToken);
        }
    }
    private GameObject FindNearbyEntity()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius, entityLayer);
        
        GameObject nearestEntity = null;
        float minDistance = float.MaxValue;
        
        foreach (Collider2D col in colliders)
        {
            // Игнорируем себя
            if (col.gameObject == gameObject)
                continue;
            
            float distance = Vector2.Distance(transform.position, col.transform.position);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestEntity = col.gameObject;
            }
        }
        
        return nearestEntity;
    }
    private bool FindNearbyFood()
    {
        // Поиск объектов еды в радиусе
        // Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius, foodLayer);
        
        // GameObject nearestFood = null;
        // float minDistance = float.MaxValue;
        
        // foreach (Collider2D col in colliders)
        // {
        //     float distance = Vector2.Distance(transform.position, col.transform.position);
            
        //     if (distance < minDistance)
        //     {
        //         minDistance = distance;
        //         nearestFood = col.gameObject;
        //     }
        // }
        
        return true;
    }
    
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        StartCoroutine(FlashColor(Color.red, 0.2f));
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        actionCts?.Cancel();
        
        spriteRenderer.color = Color.gray;
        
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        Destroy(this.gameObject);
    }
    private System.Collections.IEnumerator FlashColor(Color flashColor, float duration)
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = originalColor;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = Color.cyan;
        foreach (Vector2 point in pointsOfInterest)
        {
            Gizmos.DrawSphere(point, 0.3f);
        }
    }
}