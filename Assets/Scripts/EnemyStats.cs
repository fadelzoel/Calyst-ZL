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
    [Tooltip("How long a knockback slide plays before a stun freezes the enemy in place.")]
    public float knockbackSlideTime = 0.12f;

    [Header("Time Stop")]
    public Color timeStopColor = new Color(0.6f, 0.75f, 1f, 1f);
    [Tooltip("Cap on the knockback speed stored while time is stopped (hits stack up to this).")]
    public float maxStoredKnockback = 30f;
    [Tooltip("How long the released knockback slides before any leftover stun freezes the enemy.")]
    public float releaseSlideTime = 0.3f;
    public float releaseStunTime = 0.4f;

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

    private float knockbackEndTime;
    private bool freezeWhenSlideEnds;

    // Time stop state
    private bool timeFrozen;
    private Vector2 savedVelocity;
    private Vector2 storedKnockback;
    private RigidbodyConstraints2D preTimeConstraints;

    void Awake()
    {
        CurrentHealth = maxHealth;
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        if (sr != null) originalColor = sr.color;
        if (rb != null) originalConstraints = rb.constraints;
    }

    void OnEnable()
    {
        TimeStop.Started += OnTimeStopStarted;
        TimeStop.Ended += OnTimeStopEnded;

        // Spawned while time is stopped
        if (TimeStop.Active) OnTimeStopStarted();
    }

    void OnDisable()
    {
        TimeStop.Started -= OnTimeStopStarted;
        TimeStop.Ended -= OnTimeStopEnded;
    }

    void Update()
    {
        if (IsDead) return;

        // Flash keeps running during time stop so hits still give feedback
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f) RefreshColor();
        }

        // Stun is paused while time is stopped
        if (timeFrozen) return;

        if (stunTimer > 0f)
        {
            // A stun that arrived mid-knockback freezes once the slide is over
            if (freezeWhenSlideEnds && !isFrozen && Time.time >= knockbackEndTime)
                Freeze();

            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) EndStun();
        }
    }

    // ---------- Time stop ----------
    void OnTimeStopStarted()
    {
        if (IsDead || timeFrozen) return;

        timeFrozen = true;
        storedKnockback = Vector2.zero;

        if (rb != null)
        {
            savedVelocity = isFrozen ? Vector2.zero : rb.linearVelocity;
            preTimeConstraints = isFrozen ? originalConstraints : rb.constraints;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        RefreshColor();
    }

    void OnTimeStopEnded()
    {
        if (!timeFrozen) return;
        timeFrozen = false;
        if (IsDead) return;

        if (rb != null)
        {
            bool wasStunFrozen = isFrozen;

            // If a stun froze us, keep FreezeAll; otherwise restore the normal constraints
            if (!isFrozen) rb.constraints = preTimeConstraints;

            if (storedKnockback.sqrMagnitude > 0.01f)
            {
                // Release the stored hits: break the stun-freeze so we can actually fly
                if (isFrozen)
                {
                    rb.constraints = originalConstraints;
                    isFrozen = false;
                }

                rb.linearVelocity = Vector2.ClampMagnitude(storedKnockback, maxStoredKnockback);
                knockbackEndTime = Time.time + releaseSlideTime;
                stunTimer = Mathf.Max(stunTimer, releaseStunTime);
                freezeWhenSlideEnds = wasStunFrozen; // leftover stun re-freezes after the slide
            }
            else if (!isFrozen)
            {
                rb.linearVelocity = savedVelocity;
            }
        }

        storedKnockback = Vector2.zero;
        RefreshColor();
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
        freezeWhenSlideEnds = true;

        // If a knockback slide is still playing, Update() will freeze us when it ends
        if (Time.time >= knockbackEndTime)
            Freeze();

        RefreshColor();
        onStunned.Invoke();
    }

    void Freeze()
    {
        if (isFrozen || rb == null) return;

        originalConstraints = timeFrozen ? preTimeConstraints : rb.constraints;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        isFrozen = true;
        RefreshColor();
    }

    void EndStun()
    {
        stunTimer = 0f;
        freezeWhenSlideEnds = false;

        if (isFrozen && rb != null)
            rb.constraints = originalConstraints;

        isFrozen = false;
        RefreshColor();
    }

    // ---------- Knockback ----------
    public void Knockback(Vector2 direction, float force)
    {
        if (IsDead || !canBeKnockedBack || rb == null || force <= 0f) return;

        // Time stopped: don't move, just bank the hit. Released when time resumes.
        if (timeFrozen)
        {
            storedKnockback = Vector2.ClampMagnitude(storedKnockback + direction.normalized * force, maxStoredKnockback);
            return;
        }

        if (isFrozen) return;

        rb.linearVelocity = direction.normalized * force;
        knockbackEndTime = Time.time + knockbackSlideTime;
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

    // ---------- Sprite color (one place so flash, stun and time stop don't fight) ----------
    void RefreshColor()
    {
        if (sr == null) return;

        if (flashTimer > 0f) sr.color = hitFlashColor;
        else if (timeFrozen) sr.color = timeStopColor;
        else if (isFrozen) sr.color = stunColor;
        else sr.color = originalColor;
    }
}