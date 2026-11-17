using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnemyStats : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 50f; // Change this for every enemy!

    [Header("Stun Settings")]
    [Tooltip("Used when Stun() is called without a duration.")]
    public float stunDuration = 0.5f;
    public bool canBeStunned = true; // Bosses might have armor and ignore stuns

    [Header("Knockback Settings")]
    public bool canBeKnockedBack = true;
    [Tooltip("Brief hit-stun after a knockback. AI should not move while IsStunned.")]
    public float knockbackStunTime = 0.15f;

    [Header("Hit Feedback")]
    public float hitFlashDuration = 0.1f;
    public Color hitFlashColor = Color.white;
    public Color stunColor = Color.yellow;

    [Header("Death")]
    public float deathFadeTime = 0.4f;
    [Tooltip("Untick to just deactivate the object instead of destroying it.")]
    public bool destroyOnDeath = true;

    [Header("Events (optional, hook up sounds, drops, UI...)")]
    public UnityEvent onDamaged;
    public UnityEvent onStunned;
    public UnityEvent onDied;

    public float CurrentHealth { get; private set; }
    public float HealthPercent => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsDead { get; private set; }

    /// <summary>True while stunned or in knockback hit-stun. Enemy AI should check this.</summary>
    public bool IsStunned => stunTimer > 0f;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Color originalColor = Color.white;
    private RigidbodyConstraints2D originalConstraints;

    private float stunTimer;
    private float flashTimer;
    private bool isFrozen;

    void Awake()
    {
        CurrentHealth = maxHealth;
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        if (sr != null) originalColor = sr.color;
        if (rb != null) originalConstraints = rb.constraints;
    }

    void Update()
    {
        if (IsDead) return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f) RefreshColor();
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) EndStun();
        }
    }

    // ---------- Damage ----------
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        Debug.Log($"{gameObject.name} took {amount} damage! HP left: {CurrentHealth}");

        flashTimer = hitFlashDuration;
        RefreshColor();
        onDamaged.Invoke();

        if (CurrentHealth <= 0f)
            Die();
    }

    // ---------- Stun ----------
    public void Stun() => Stun(stunDuration);

    public void Stun(float duration)
    {
        if (IsDead || !canBeStunned || duration <= 0f) return;

        // Never shorten a stun that's already running
        stunTimer = Mathf.Max(stunTimer, duration);

        if (!isFrozen && rb != null)
        {
            originalConstraints = rb.constraints;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            isFrozen = true;
        }

        RefreshColor();
        onStunned.Invoke();
    }

    void EndStun()
    {
        stunTimer = 0f;

        if (isFrozen && rb != null)
            rb.constraints = originalConstraints;

        isFrozen = false;
        RefreshColor();
    }

    // ---------- Knockback ----------
    public void Knockback(Vector2 direction, float force)
    {
        if (IsDead || !canBeKnockedBack || isFrozen || rb == null || force <= 0f) return;

        rb.linearVelocity = direction.normalized * force;
        stunTimer = Mathf.Max(stunTimer, knockbackStunTime);
    }

    // ---------- Death ----------
    void Die()
    {
        IsDead = true;
        EndStun();

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Debug.Log($"{gameObject.name} was destroyed!");
        onDied.Invoke();
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        if (sr != null && deathFadeTime > 0f)
        {
            float t = 0f;
            while (t < deathFadeTime)
            {
                t += Time.deltaTime;
                Color c = originalColor;
                c.a = Mathf.Lerp(originalColor.a, 0f, t / deathFadeTime);
                sr.color = c;
                yield return null;
            }
        }

        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // ---------- Sprite color (one place so flash and stun don't fight) ----------
    void RefreshColor()
    {
        if (sr == null) return;

        if (flashTimer > 0f) sr.color = hitFlashColor;
        else if (isFrozen) sr.color = stunColor;
        else sr.color = originalColor;
    }
}