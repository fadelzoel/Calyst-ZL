using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponType
{
    Katana,
    ArcClaw,
    ChronoSpear
}

[RequireComponent(typeof(PlayerController))]
public class PlayerSkills : MonoBehaviour
{
    [Header("Weapon & Overload State")]
    public WeaponType currentWeapon = WeaponType.Katana;
    [Range(0f, 100f)] public float overloadMeter = 0f;
    [Tooltip("Overload drained per second back down to the weapon's base load. 0 = never drains.")]
    public float overloadDecayPerSecond = 3f;
    [Tooltip("Arc-Claw is heavy: extra run-speed multiplier while it is equipped.")]
    public float arcClawSpeedMultiplier = 0.85f;

    [Header("Cooldowns")]
    public float skill1Cooldown = 5f;
    public float skill2Cooldown = 8f;
    private float skill1Timer;
    private float skill2Timer;

    [Header("Katana Skill 1: Ranged Energy Wave")]
    public Transform firePoint; // Can reuse your existing AttackPoint!
    public float waveSpeed = 18f;
    public float waveLifetime = 0.6f;
    public int waveDamage = 20;
    public float skill1OverloadCost = 8f;

    [Header("Katana Skill 2: Slow-Mo Slash Flurry")]
    public float flurryDuration = 0.45f;
    public int flurryHits = 4;
    public float flurryRange = 1.4f;
    public int damagePerSlash = 12;
    public float slowMoScale = 0.35f;
    public float skill2OverloadCost = 15f;
    public LayerMask enemyLayers;

    public bool IsUsingSkill { get; private set; }

    private PlayerController movement;
    private PlayerCombat combat;

    private float baseMoveSpeed;
    private float baseFixedDeltaTime;
    private bool slowMoActive;
    private readonly HashSet<IDamageable> hitThisSlash = new HashSet<IDamageable>();

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void OnDisable()
    {
        // If we get disabled/destroyed mid-flurry, never leave the game stuck in slow-mo
        if (slowMoActive) RestoreTime();
    }

    void Start()
    {
        movement = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
        baseMoveSpeed = movement.moveSpeed;

        if (firePoint == null && combat != null)
            firePoint = combat.attackPoint;
    }

    void Update()
    {
        // Use unscaledDeltaTime so Slow-Mo doesn't freeze your own cooldown ticks
        if (skill1Timer > 0f) skill1Timer -= Time.unscaledDeltaTime;
        if (skill2Timer > 0f) skill2Timer -= Time.unscaledDeltaTime;

        // Overload slowly drains back to whatever the equipped weapon weighs on its own
        if (overloadDecayPerSecond > 0f && !IsUsingSkill)
        {
            float floor = WeaponBaseLoad();
            if (overloadMeter > floor)
            {
                overloadMeter = Mathf.Max(floor, overloadMeter - overloadDecayPerSecond * Time.unscaledDeltaTime);
                UpdateOverloadSpeedPenalty();
            }
        }
    }

    // ---------- Input Callbacks (New Input System) ----------
    public void OnSkill1(InputValue value)
    {
        if (!value.isPressed) return;
        if (!CanUseSkill() || skill1Timer > 0f) return;

        switch (currentWeapon)
        {
            case WeaponType.Katana:
                StartCoroutine(KatanaSkill1_EnergyWave());
                break;
            case WeaponType.ArcClaw:
                Debug.Log("Arc-Claw Skill 1 (Magnetic Pull) - Ready to wire!");
                break;
            case WeaponType.ChronoSpear:
                Debug.Log("Chrono-Spear Skill 1 (Phase Pierce) - Ready to wire!");
                break;
        }
    }

    public void OnSkill2(InputValue value)
    {
        if (!value.isPressed) return;
        if (!CanUseSkill() || skill2Timer > 0f) return;

        switch (currentWeapon)
        {
            case WeaponType.Katana:
                StartCoroutine(KatanaSkill2_SlowMoFlurry());
                break;
            case WeaponType.ArcClaw:
                Debug.Log("Arc-Claw Skill 2 (Short-Circuit) - Ready to wire!");
                break;
            case WeaponType.ChronoSpear:
                Debug.Log("Chrono-Spear Skill 2 (Temporal Echo) - Ready to wire!");
                break;
        }
    }

    public void OnSwitchWeapon(InputValue value)
    {
        if (!value.isPressed || IsUsingSkill) return;

        // Cycle Katana -> ArcClaw -> ChronoSpear -> Katana
        currentWeapon = (WeaponType)(((int)currentWeapon + 1) % 3);
        ApplyWeaponLoadModifiers();
        Debug.Log($"Switched Weapon to: {currentWeapon} | Overload: {overloadMeter}%");
    }

    bool CanUseSkill()
    {
        if (movement == null || movement.IsDead || movement.IsStunned || movement.IsDashing)
            return false;
        if (IsUsingSkill || (combat != null && combat.IsAttacking))
            return false;
        return true;
    }

    // ---------- Katana Skill 1: Ranged Energy Slash (No Sprite Needed) ----------
    IEnumerator KatanaSkill1_EnergyWave()
    {
        IsUsingSkill = true;
        skill1Timer = skill1Cooldown;
        AddOverload(skill1OverloadCost);

        int dir = movement.Facing;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.right * dir * 0.6f;

        // Generate a clean procedural placeholder slash wave so you can test without sprites
        GameObject wave = new GameObject("KatanaEnergyWave_Placeholder");
        wave.transform.position = spawnPos;

        LineRenderer lr = wave.AddComponent<LineRenderer>();
        lr.positionCount = 3;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.cyan;
        lr.endColor = Color.white;
        lr.useWorldSpace = false;
        // Crescent slash shape
        lr.SetPosition(0, new Vector3(-0.15f * dir, 0.45f, 0f));
        lr.SetPosition(1, new Vector3(0.15f * dir, 0f, 0f));
        lr.SetPosition(2, new Vector3(-0.15f * dir, -0.45f, 0f));

        // Brief cast lock
        movement.Stun(0.12f);
        yield return new WaitForSeconds(0.08f);
        IsUsingSkill = false;

        // Move wave forward and check hits
        float elapsed = 0f;
        while (elapsed < waveLifetime && wave != null)
        {
            float dt = Time.deltaTime;
            wave.transform.position += Vector3.right * (dir * waveSpeed * dt);

            Collider2D hit = Physics2D.OverlapCircle(wave.transform.position, 0.35f, enemyLayers);
            if (hit != null)
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(waveDamage);
                    Debug.Log($"Katana Skill 1 hit {hit.name} for {waveDamage}!");
                    Destroy(wave);
                    yield break;
                }
            }

            elapsed += dt;
            yield return null;
        }

        if (wave != null) Destroy(wave);
    }

    // ---------- Katana Skill 2: Slow-Mo Multi-Slash Flurry ----------
    IEnumerator KatanaSkill2_SlowMoFlurry()
    {
        IsUsingSkill = true;
        skill2Timer = skill2Cooldown;
        AddOverload(skill2OverloadCost);

        int dir = movement.Facing;
        Vector3 center = firePoint != null ? firePoint.position : transform.position + Vector3.right * dir * 0.8f;

        // Trigger localized Slow-Mo
        slowMoActive = true;
        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = baseFixedDeltaTime * slowMoScale;

        // Visual placeholder ring/lines for flurry zone
        GameObject flurryVfx = new GameObject("KatanaFlurry_Placeholder");
        flurryVfx.transform.position = center;

        float interval = flurryDuration / flurryHits;

        for (int i = 0; i < flurryHits; i++)
        {
            // Draw temporary slash line per hit
            LineRenderer slashLine = flurryVfx.GetComponent<LineRenderer>();
            if (slashLine == null) slashLine = flurryVfx.AddComponent<LineRenderer>();
            slashLine.material = new Material(Shader.Find("Sprites/Default"));
            slashLine.startWidth = 0.06f;
            slashLine.endWidth = 0.06f;
            slashLine.startColor = Color.white;
            slashLine.endColor = Color.cyan;
            slashLine.SetPosition(0, center + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0f));
            slashLine.SetPosition(1, center + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.6f, 0.6f), 0f));

            // Deal flurry damage
            hitThisSlash.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, flurryRange, enemyLayers);
            foreach (Collider2D col in hits)
            {
                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target != null && hitThisSlash.Add(target)) // one hit per enemy per slash
                {
                    target.TakeDamage(damagePerSlash);
                    Debug.Log($"Katana Skill 2 Flurry Hit {i + 1}/{flurryHits} on {col.name}!");
                }
            }

            // Wait in REAL time (unscaled) so Caly slashes rapidly while enemies are slowed
            yield return new WaitForSecondsRealtime(interval);
        }

        RestoreTime();

        if (flurryVfx != null) Destroy(flurryVfx);
        IsUsingSkill = false;
    }

    // ---------- Overload & Weapon Weight Rules (From GDD) ----------
    void AddOverload(float amount)
    {
        // Chrono-Spear builds Overload 2x faster per your GDD
        float multiplier = (currentWeapon == WeaponType.ChronoSpear) ? 2f : 1f;
        overloadMeter = Mathf.Clamp(overloadMeter + (amount * multiplier), 0f, 100f);
        UpdateOverloadSpeedPenalty();
    }

    void RestoreTime()
    {
        slowMoActive = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    // Load each weapon carries just by being equipped ("More is Worse")
    float WeaponBaseLoad()
    {
        switch (currentWeapon)
        {
            case WeaponType.ArcClaw:     return 25f; // heavy claw
            case WeaponType.ChronoSpear: return 40f; // unstable spear (~40% per GDD)
            default:                     return 0f;  // Katana
        }
    }

    void ApplyWeaponLoadModifiers()
    {
        switch (currentWeapon)
        {
            case WeaponType.Katana:
                // Back to Katana must end below 30% (Tiang C & Jembatan Paradoks puzzles)
                // and below the 20% slowdown line so Katana feels agile again
                if (overloadMeter > 15f) overloadMeter = 15f;
                break;
            case WeaponType.ArcClaw:
            case WeaponType.ChronoSpear:
                overloadMeter = Mathf.Max(overloadMeter, WeaponBaseLoad());
                break;
        }

        UpdateOverloadSpeedPenalty();
    }

    void UpdateOverloadSpeedPenalty()
    {
        // Progressive slowdown at 20%, 60%, 80% Overload per your GDD
        float tier;
        if (overloadMeter >= 80f)      tier = 0.5f;   // heavy sluggish state
        else if (overloadMeter >= 60f) tier = 0.72f;  // noticeably weighed down
        else if (overloadMeter >= 20f) tier = 0.89f;  // slight load
        else                           tier = 1f;     // full agility

        // Arc-Claw is simply heavier to carry (GDD: slows running)
        float weapon = currentWeapon == WeaponType.ArcClaw ? arcClawSpeedMultiplier : 1f;

        // Scale from the speed set in the Inspector instead of hard-coded numbers
        movement.moveSpeed = baseMoveSpeed * tier * weapon;
    }
}