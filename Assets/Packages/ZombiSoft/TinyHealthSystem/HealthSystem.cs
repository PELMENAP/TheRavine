using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviour
{
    public static HealthSystem Instance;

    public Image currentHealthBar;
    public Image currentHealthGlobe;
    public Text healthText;
    public float hitPoint = 100f;
    public float maxHitPoint = 100f;

    public Image currentManaBar;
    public Image currentManaGlobe;
    public Text manaText;
    public float manaPoint = 100f;
    public float maxManaPoint = 100f;

    public bool Regenerate = true;
    public float regen = 0.1f;
    private float timeleft = 0.0f;  // Left time for current interval
    public float regenUpdateInterval = 1f;

    public bool GodMode;

    //==============================================================
    // Awake
    //==============================================================
    void Awake()
    {
        Instance = this;
    }

    //==============================================================
    // Awake
    //==============================================================
    void Start()
    {
        UpdateGraphics();
        timeleft = regenUpdateInterval;
    }

    //==============================================================
    // Update
    //==============================================================
    void Update()
    {
        if (Regenerate)
            Regen();
    }

    //==============================================================
    // Regenerate Health & Mana
    //==============================================================
    private void Regen()
    {
        timeleft -= Time.deltaTime;

        if (timeleft <= 0.0) // Interval ended - update health & mana and start new interval
        {
            // Debug mode
            if (GodMode)
            {
                HealDamage(maxHitPoint);
                RestoreMana(maxManaPoint);
            }
            else
            {
                HealDamage(regen);
                RestoreMana(regen);
            }
            UpdateGraphics();
            timeleft = regenUpdateInterval;
        }
    }

    //==============================================================
    // Health Logic
    //==============================================================
    private void UpdateHealthBar()
    {
        float ratio = hitPoint / maxHitPoint;
        currentHealthBar.rectTransform.localPosition = new Vector3(currentHealthBar.rectTransform.rect.width * ratio - currentHealthBar.rectTransform.rect.width, 0, 0);
        healthText.text = hitPoint.ToString("0") + "/" + maxHitPoint.ToString("0");
    }

    public void TakeDamage(float Damage)
    {
        hitPoint -= Damage;
        if (hitPoint < 1)
            hitPoint = 0;

        UpdateGraphics();
    }

    public void HealDamage(float Heal)
    {
        hitPoint += Heal;
        if (hitPoint > maxHitPoint)
            hitPoint = maxHitPoint;

        UpdateGraphics();
    }
    public void SetMaxHealth(float max)
    {
        maxHitPoint += (int)(maxHitPoint * max / 100);

        UpdateGraphics();
    }

    //==============================================================
    // Mana Logic
    //==============================================================
    private void UpdateManaBar()
    {
        float ratio = manaPoint / maxManaPoint;
        currentManaBar.rectTransform.localPosition = new Vector3(currentManaBar.rectTransform.rect.width * ratio - currentManaBar.rectTransform.rect.width, 0, 0);
        manaText.text = manaPoint.ToString("0") + "/" + maxManaPoint.ToString("0");
    }

    public void UseMana(float Mana)
    {
        manaPoint -= Mana;
        if (manaPoint < 1) // Mana is Zero!!
            manaPoint = 0;

        UpdateGraphics();
    }

    public void RestoreMana(float Mana)
    {
        manaPoint += Mana;
        if (manaPoint > maxManaPoint)
            manaPoint = maxManaPoint;

        UpdateGraphics();
    }
    public void SetMaxMana(float max)
    {
        maxManaPoint += (int)(maxManaPoint * max / 100);

        UpdateGraphics();
    }

    //==============================================================
    // Update all Bars & Globes UI graphics
    //==============================================================
    private void UpdateGraphics()
    {
        UpdateHealthBar();
        UpdateManaBar();
    }
}
