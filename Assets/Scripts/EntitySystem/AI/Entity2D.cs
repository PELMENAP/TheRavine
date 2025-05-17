using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using NaughtyAttributes;

using Random = UnityEngine.Random;
using TMPro;
using R3;
public class Entity2D : MonoBehaviour
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
    
    // Состояние сущности
    private bool isMoving = false;
    private bool isAttacking = false;
    private bool canAttack = true;
    private Vector2 targetPosition;
    private CancellationTokenSource actionCts;
    
    // Интерес
    private List<Vector2> pointsOfInterest = new List<Vector2>();
    private int maxPointsOfInterest = 5;
    
    // Компоненты
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
    }
    
    // Текущее действие
    private EntityAction currentAction = EntityAction.Idle;
    
    public string file = "default";
    [Button]
    private async void Save()
    {
        await DelayedPerceptronStorage.SaveAsync(delayedPerceptron, file);
    }

    private async void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;
        
        actionCts = new CancellationTokenSource();
        
        // Инициализация параметров
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
        delayedPerceptron = new(inputVectorizer.GetVectorSize(), 16, 16, Enum.GetValues(typeof(EntityAction)).Length);

        BehaviorLoopAsync(actionCts.Token).Forget();
        
        RegenerateEnergyAsync(actionCts.Token).Forget();
    }
    
    private void OnDisable()
    {
        // Отмена всех задач при отключении объекта
        actionCts?.Cancel();
        actionCts?.Dispose();
        actionCts = new CancellationTokenSource();
    }
    
    // Основной асинхронный цикл поведения
    private async UniTaskVoid BehaviorLoopAsync(CancellationToken cancellationToken)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await PerformRandomAction(cancellationToken);
            
            // Небольшая пауза между действиями
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
        for(int i = 0; i < delayedPerceptron.DelayedList.Count; i++)
        {
            var firstComponent = delayedPerceptron.DelayedList[0];
            var currentComponent = delayedPerceptron.DelayedList[i];
            if(currentComponent.Predicted != firstComponent.Predicted || currentComponent.Evaluation != firstComponent.Evaluation)
            {
                theSame = false;
                break;
            } 
        }
        // string mes = "";
        // for (int i = 0; i < delayedPerceptron.DelayedList.Count; i++)
        // {
        //     mes += delayedPerceptron.DelayedList[i].Evaluation + "   ";
        // }
        // Debug.Log(mes);  
        
        // if(theSame) delayedPerceptron.Train(input, predictedIndex, 0.6f, true);

        if(theSame && delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.4f;
    }
    
    // Выполнение конкретного действия
    public async UniTask PerformAction(EntityAction action, CancellationToken cancellationToken)
    {
        if (currentHealth <= 0)
        {
            Die();
        }
        // Проверка возможности выполнить действие
        if (!CanPerformAction(action))
        {
            if(delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.3f;
            currentHealth -= 5;
            return;
        }
        
        // Обновление текущего действия
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
        }
    }

    private async UniTask ReproduceAsync(CancellationToken cancellationToken)
    {
        if(delayedPerceptron.DelayedList.Count > 0) delayedPerceptron.DelayedList[delayedPerceptron.DelayedList.Count - 1].Evaluation = 0.8f;
        
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector2 wanderTarget = (Vector2)transform.position + randomDirection * wanderRadius / 2;

        GameObject nextGeneration = Instantiate(prefab, (Vector3)wanderTarget, Quaternion.identity, this.transform.parent);
        Entity2D entity2D = nextGeneration.GetComponent<Entity2D>();
        entity2D.SetUp(delayedPerceptron);

        currentEnergy -= reproduceEnergyCost;
        currentHealth -= reproduceHealthCost;

        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: cancellationToken);
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
    
    // Просто стоять и ничего не делать
    private async UniTask IdleAsync(CancellationToken cancellationToken)
    {
        isMoving = false;
        rb.velocity = Vector2.zero;
        
        // Изменение цвета для визуального отображения действия
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        
        await UniTask.Delay(TimeSpan.FromSeconds(idleTime), cancellationToken: cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    // Бродить без цели
    private async UniTask WanderAsync(CancellationToken cancellationToken)
    {
        // Выбор случайной точки в радиусе
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector2 wanderTarget = (Vector2)transform.position + randomDirection * wanderRadius;
        
        // Изменение цвета для визуального отображения действия
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.yellow;
        
        // Движение к случайной точке
        float wanderTime = Random.Range(minWanderTime, maxWanderTime);
        await MoveToPositionAsync(wanderTarget, moveSpeed, wanderTime, energyCostPerSecondMoving, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    // Запомнить текущую позицию как точку интереса
    private void RememberCurrentPosition()
    {
        Vector2 currentPos = transform.position;
        
        // Добавление текущей позиции в список интересных точек
        if (pointsOfInterest.Count >= maxPointsOfInterest)
        {
            pointsOfInterest.RemoveAt(0); // Удаляем самую старую точку
        }

        if(pointsOfInterest.Count > 0 && Vector2.Distance(pointsOfInterest[0], currentPos) < 10f)
        {
            return;
        }
        
        pointsOfInterest.Add(currentPos);
        
        // Визуальная индикация
        StartCoroutine(FlashColor(Color.cyan, 0.3f));
    }
    
    // Идти в случайную точку интереса
    private async UniTask GoToPointOfInterestAsync(CancellationToken cancellationToken)
    {
        if (pointsOfInterest.Count == 0)
        {
            return;
        }
        
        // Выбор случайной точки из списка
        int randomIndex = Random.Range(0, pointsOfInterest.Count);
        Vector2 targetPoint = pointsOfInterest[randomIndex];
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.blue;
        
        // Движение к выбранной точке
        await MoveToPositionAsync(targetPoint, moveSpeed, 5f, energyCostPerSecondMoving, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    // Нападать на ближайшую сущность
    private async UniTask AttackNearbyEntityAsync(CancellationToken cancellationToken)
    {
        // Поиск ближайшей сущности
        GameObject nearestEntity = FindNearbyEntity();
        
        if (nearestEntity == null)
        {
            return;
        }
        
        // Изменение цвета для визуального отображения действия
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        
        // Движение к цели
        Entity2D targetEntity = nearestEntity.GetComponent<Entity2D>();
        Vector2 targetPosition = nearestEntity.transform.position;
        
        // Приближаемся к цели на расстояние атаки
        await MoveToPositionAsync(targetPosition, moveSpeed, 2f, energyCostPerSecondMoving, cancellationToken);
        
        if(transform == null || nearestEntity == null) return;
        // Атака, если достаточно близко
        if (Vector2.Distance(transform.position, nearestEntity.transform.position) <= attackRange && canAttack)
        {
            // Уменьшение энергии
            currentEnergy -= attackEnergyCost;
            
            // Эффект атаки
            isAttacking = true;
            canAttack = false;
            
            // Нанесение урона цели
            if (targetEntity != null)
            {
                targetEntity.TakeDamage(attackDamage);
                currentHealth += attackEnergyCost * 2;
            }
            
            // Кулдаун атаки
            await UniTask.Delay(TimeSpan.FromSeconds(attackCooldown), cancellationToken: cancellationToken);
            
            isAttacking = false;
            canAttack = true;
        }
        
        spriteRenderer.color = originalColor;
    }
    
    // Убегать от ближайшей сущности
    private async UniTask FleeFromNearbyEntityAsync(CancellationToken cancellationToken)
    {
        // Поиск ближайшей сущности
        GameObject nearestEntity = FindNearbyEntity();
        
        if (nearestEntity == null)
        {
            return;
        }
        
        // Изменение цвета для визуального отображения действия
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.green;
        
        // Расчет направления в сторону от сущности
        Vector2 fleeDirection = ((Vector2)transform.position - (Vector2)nearestEntity.transform.position).normalized;
        Vector2 fleeTarget = (Vector2)transform.position + fleeDirection * (detectionRadius * 1.5f);
        
        // Бег в противоположном направлении
        await MoveToPositionAsync(fleeTarget, runSpeed, 2f, energyCostPerSecondRunning, cancellationToken);
        
        spriteRenderer.color = originalColor;
    }
    
    // Есть ближайшую еду
    private async UniTask EatNearbyFoodAsync(CancellationToken cancellationToken)
    {
        // Изменение цвета для визуального отображения действия
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.magenta;
        
        currentHealth = Mathf.Min(currentHealth + 20f, maxHealth.Value);
        currentEnergy = Mathf.Min(currentEnergy - 10f, maxEnergy.Value);
                
        spriteRenderer.color = originalColor;
    }
    
    // Общий метод для движения к позиции
    private async UniTask MoveToPositionAsync(Vector2 position, float speed, float maxMoveDuration, float energyCostPerSecond, CancellationToken cancellationToken)
    {
        isMoving = true;
        float startTime = Time.time;
        float journeyDuration = 0f;
        
        while (Vector2.Distance(transform.position, position) > 0.1f && journeyDuration < maxMoveDuration)
        {
            // Проверка энергии
            if (currentEnergy <= 0)
            {
                // Остановка при истощении энергии
                isMoving = false;
                rb.velocity = Vector2.zero;
                return;
            }
            
            // Расчет направления движения
            Vector2 direction = ((Vector2)position - (Vector2)transform.position).normalized;
            
            // Движение к цели
            rb.velocity = direction * speed;
            
            // Уменьшение энергии
            currentEnergy -= energyCostPerSecond * Time.deltaTime;
            currentEnergy = Mathf.Max(0, currentEnergy);
            
            // Обновление времени движения
            journeyDuration = Time.time - startTime;
            
            await UniTask.Yield(cancellationToken);
        }
        
        // Остановка по завершении
        isMoving = false;
        rb.velocity = Vector2.zero;
    }
    
    // Регенерация энергии со временем
    private async UniTaskVoid RegenerateEnergyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Регенерация энергии только во время бездействия
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
    
    // Поиск ближайшей сущности в радиусе
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
    
    // Поиск ближайшей еды в радиусе
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
    
    // Метод для получения урона
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        
        // Визуальный эффект получения урона
        StartCoroutine(FlashColor(Color.red, 0.2f));
        
        // Проверка смерти
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    // Метод смерти
    private void Die()
    {
        // Отмена всех действий
        actionCts?.Cancel();
        
        // Визуальный эффект
        spriteRenderer.color = Color.gray;
        
        // Отключение компонентов
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        
        
        // Для демонстрации просто отключаем gameObject
        // gameObject.SetActive(false);

        Destroy(this.gameObject);
    }
    
    // Визуальный эффект мигания цветом
    private System.Collections.IEnumerator FlashColor(Color flashColor, float duration)
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = originalColor;
    }
    
    // Для отладки: Показать радиус обнаружения и атаки
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Отображение точек интереса
        Gizmos.color = Color.cyan;
        foreach (Vector2 point in pointsOfInterest)
        {
            Gizmos.DrawSphere(point, 0.3f);
        }
    }
}