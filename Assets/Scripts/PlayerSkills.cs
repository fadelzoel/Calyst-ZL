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
    public GameObject wavePrefab;          
    public float waveHitRadius = 0.6f;
    public Transform firePoint;
    public float waveSpeed = 18f;
    public float waveLifetime = 0.6f;
    public int waveDamage = 20;
    public float skill1OverloadCost = 8f;
    [Tooltip("Wave fired during time stop: how far it travels before hanging in the air.")]
    public float waveTimeStopHangDistance = 1.5f;

    [Header("Katana Skill 2: Judgement Cut")]
    public GameObject judgementSlashPrefab; // <-- ADD THIS
    public float judgementSlashDuration = 0.25f;
    public bool randomJudgementAngle = true;
    [Tooltip("Every enemy inside this radius gets cut.")]
    public float judgementRange = 6f;
    public int judgementHits = 4;
    [Tooltip("Sheath pose before the cuts land. Targets are held in place.")]
    public float judgementWindup = 0.45f;
    public float judgementHitInterval = 0.08f;
    public float judgementRecovery = 0.25f;
    public int damagePerSlash = 12;
    public float skill2OverloadCost = 15f;
    public LayerMask enemyLayers;


    [Header("Chrono-Spear Skill 1: Spear Throw (1 ammo)")]
    public GameObject spearPrefab;
    public int spearAmmo = 1;
    public int spearMaxAmmo = 1;
    public float spearThrowCooldown = 12f;
    public float spearThrowOverloadCost = 10f;
    public float spearSpeed = 25f;
    public float spearMaxRange = 25f;
    public int spearDamage = 15;
    [Tooltip("How long a hit enemy is frozen.")]
    public float spearFreezeTime = 4f;
    [Tooltip("A spear stuck anywhere returns by itself after this many seconds.")]
    public float spearAutoReturnTime = 30f;
    public float spearPickupRadius = 1.2f;
    [Tooltip("Thrown during time stop: how far it travels before hanging in the air.")]
    public float spearTimeStopHangDistance = 1.5f;

    [Header("Chrono-Spear Skill 2: Time Platform (air only)")]
    public float spearPlatformCooldown = 3f;
    public float spearPlatformOverloadCost = 12f;
    public Vector2 platformSize = new Vector2(2.2f, 0.25f);
    [Tooltip("Speed it glides forward, only while time is stopped.")]
    public float platformSpeed = 8f;
    public float platformLifetime = 12f;
    public int maxPlatforms = 2;
    [Tooltip("Layer name given to platforms. Must be part of the player's Ground Layer mask.")]
    public string platformLayerName = "Ground";

    // Raw "a skill routine is running" flag. A burnout overrides it so she is never held in mid-air.
    private bool usingSkill;
    public bool IsUsingSkill
    {
        get { return usingSkill && !IsBurnedOut; }
        private set { usingSkill = value; }
    }

    private float burnoutTimer;
    /// <summary>True while she is helpless after Overload maxed out.</summary>
    public bool IsBurnedOut => burnoutTimer > 0f;

    [Header("Ultimate: Time Stop (toggle)")]
    [Tooltip("Everything is frozen and Caly plays the cast animation for this long before she can move.")]
    public float timeStopCastLock = 2f;
    [Tooltip("Overload added instantly when you cast.")]
    public float timeStopOverloadCost = 10f;
    [Tooltip("Overload added per second while time is stopped. 8/s = about 11s until max.")]
    public float timeStopOverloadPerSecond = 8f;
    [Tooltip("Short delay after time resumes before you can cast again.")]
    public float timeStopCooldown = 0.5f;

    [Header("Sanity & Burnout")]
    [Tooltip("Goes up by 1 every time Overload hits 100%. Hitting maxSanity + 1 (8) = game reset.")]
    public int sanity = 0;
    public int maxSanity = 7;
    [Tooltip("Player is stunned this long (air or ground) when Overload maxes out.")]
    public float overloadStunDuration = 3.5f;
    [Tooltip("Player can't be hit while helpless from burnout.")]
    public bool burnoutInvincible = true;

    public bool IsTimeStopped => TimeStop.Active;
    /// <summary>True during the cast animation, when she can't move or cancel yet.</summary>
    public bool IsTimeStopCasting { get; private set; }

    /// <summary>Walls/ground that stick spears and block platforms.</summary>
    public LayerMask StickLayers => movement != null ? movement.groundLayer : (LayerMask)0;

    private float ultimateTimer;
    private bool timeStopEndRequested;

    private PlayerController movement;
    private PlayerCombat combat;

    private float baseMoveSpeed;
    private readonly HashSet<IDamageable> hitThisSlash = new HashSet<IDamageable>();
    private readonly List<TimePlatform> platforms = new List<TimePlatform>();
    private Material lineMat;

    // On-screen notice
    private string noticeText;
    private float noticeUntil;
    private GUIStyle noticeStyle;

    void OnDisable()
    {
        if (TimeStop.Active) TimeStop.End();
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
        if (skill1Timer > 0f) skill1Timer -= Time.unscaledDeltaTime;
        if (skill2Timer > 0f) skill2Timer -= Time.unscaledDeltaTime;
        if (ultimateTimer > 0f && !TimeStop.Active) ultimateTimer -= Time.unscaledDeltaTime;
        if (burnoutTimer > 0f) burnoutTimer -= Time.deltaTime;

        // Overload slowly drains back to whatever the equipped weapon weighs on its own
        if (overloadDecayPerSecond > 0f && !IsUsingSkill && !TimeStop.Active)
        {
            float floor = WeaponBaseLoad();
            if (overloadMeter > floor)
            {
                overloadMeter = Mathf.Max(floor, overloadMeter - overloadDecayPerSecond * Time.unscaledDeltaTime);
                UpdateOverloadSpeedPenalty();
            }
        }
    }

    // ---------- Notice (simple on-screen text) ----------
    public void ShowNotice(string text)
    {
        noticeText = text;
        noticeUntil = Time.unscaledTime + 1.5f;
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(noticeText) || Time.unscaledTime > noticeUntil) return;

        if (noticeStyle == null)
        {
            noticeStyle = new GUIStyle(GUI.skin.label);
            noticeStyle.alignment = TextAnchor.MiddleCenter;
            noticeStyle.fontSize = 24;
            noticeStyle.fontStyle = FontStyle.Bold;
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 40f), noticeText, noticeStyle);
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
                CastSpearThrow();
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
                StartCoroutine(KatanaSkill2_JudgementCut());
                break;
            case WeaponType.ArcClaw:
                Debug.Log("Arc-Claw Skill 2 (Short-Circuit) - Ready to wire!");
                break;
            case WeaponType.ChronoSpear:
                CastSpearPlatform();
                break;
        }
    }

    public void OnUltimate(InputValue value)
    {
        if (!value.isPressed) return;

        // Already stopped: press again to end it (not allowed during the cast animation)
        if (TimeStop.Active)
        {
            if (!IsTimeStopCasting) timeStopEndRequested = true;
            return;
        }

        if (ultimateTimer > 0f || !CanUseSkill()) return;

        StartCoroutine(TimeStopRoutine());
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

    IEnumerator QuickCast(float lockTime)
    {
        IsUsingSkill = true;
        movement.Stun(lockTime);
        yield return new WaitForSeconds(lockTime);
        IsUsingSkill = false;
    }

    Material LineMat()
    {
        if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
        return lineMat;
    }

    // ---------- Katana Skill 1: Ranged Crescent Energy Wave ----------
    IEnumerator KatanaSkill1_EnergyWave()
    {
        skill1Timer = skill1Cooldown;
        AddOverload(skill1OverloadCost);
        movement.PlayTrigger("KatanaSkill1");
        StartCoroutine(QuickCast(0.5f));

        // Brief windup so the wave spawns as her blade swings forward
        yield return new WaitForSeconds(0.15f);

        int dir = movement.Facing;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.right * dir * 0.6f;

        GameObject wave;
        bool hasPrefab = wavePrefab != null;
        if (hasPrefab)
        {
            wave = Instantiate(wavePrefab, spawnPos, Quaternion.identity);
            Vector3 scale = wave.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dir;
            wave.transform.localScale = scale;
        }
        else
        {
            wave = new GameObject("KatanaEnergyWave");
            wave.transform.position = spawnPos;
        }

        EnergyWaveVFX waveVfx = wave.AddComponent<EnergyWaveVFX>();
        waveVfx.Init(dir, hasPrefab);

        bool firedDuringStop = TimeStop.Active;

        movement.Stun(0.12f);
        yield return new WaitForSeconds(0.08f);
        IsUsingSkill = false;

        float elapsed = 0f;
        float flown = 0f;
        while (elapsed < waveLifetime && wave != null)
        {
            float dt = Time.deltaTime;
            float step = waveSpeed * dt;
            bool frozen = false;

            if (!TimeStop.Active)
            {
                firedDuringStop = false;
            }
            else if (!firedDuringStop)
            {
                frozen = true;
            }
            else
            {
                float left = waveTimeStopHangDistance - flown;
                if (left <= 0f) frozen = true;
                else step = Mathf.Min(step, left);
            }

            waveVfx.isFrozen = frozen;

            if (!frozen)
            {
                wave.transform.position += Vector3.right * (dir * step);
                flown += step;

                Collider2D hit = Physics2D.OverlapCircle(wave.transform.position, waveHitRadius, enemyLayers);
                if (hit != null)
                {
                    IDamageable target = hit.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        target.TakeDamage(waveDamage);
                        waveVfx.SpawnImpactBurst(wave.transform.position);
                        Debug.Log($"Katana Skill 1 hit {hit.name} for {waveDamage}!");
                        Destroy(wave);
                        yield break;
                    }
                }

                elapsed += dt;
            }

            yield return null;
        }

        if (wave != null) Destroy(wave);
    }

    
    GameObject CreateEnergyWaveVisual(Vector3 spawnPos, int dir)
    {
        GameObject root = new GameObject("KatanaEnergyWave");
        root.transform.position = spawnPos;
        root.transform.localScale = new Vector3(dir, 1f, 1f);

        // Tapered width curve: sharp needle tips at both ends, thick center
        AnimationCurve bladeTaper = new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0.05f)
        );

        int arcPoints = 15;
        float arcHeight = 0.85f;
        float arcCurveDepth = 0.42f;

        // 1. Outer Cyan Aura Arc
        GameObject outerObj = new GameObject("OuterAura");
        outerObj.transform.SetParent(root.transform, false);
        LineRenderer outer = outerObj.AddComponent<LineRenderer>();
        outer.useWorldSpace = false;
        outer.positionCount = arcPoints;
        outer.widthCurve = bladeTaper;
        outer.widthMultiplier = 0.28f;
        outer.sortingOrder = 18;
        outer.material = LineMat();
        outer.startColor = new Color(0.1f, 0.75f, 1f, 0.85f);
        outer.endColor = new Color(0.1f, 0.75f, 1f, 0.85f);

        // 2. Inner White-Hot Slash Core
        GameObject coreObj = new GameObject("InnerCore");
        coreObj.transform.SetParent(root.transform, false);
        LineRenderer core = coreObj.AddComponent<LineRenderer>();
        core.useWorldSpace = false;
        core.positionCount = arcPoints;
        core.widthCurve = bladeTaper;
        core.widthMultiplier = 0.11f;
        core.sortingOrder = 19;
        core.material = LineMat();
        core.startColor = Color.white;
        core.endColor = Color.white;

        for (int i = 0; i < arcPoints; i++)
        {
            float t = i / (float)(arcPoints - 1);          // 0 -> 1
            float angle = Mathf.Lerp(-65f, 65f, t) * Mathf.Deg2Rad;
            float x = (Mathf.Cos(angle) - 0.42f) * (arcCurveDepth / 0.58f);
            float y = Mathf.Sin(angle) * arcHeight;

            outer.SetPosition(i, new Vector3(x, y, 0f));
            core.SetPosition(i, new Vector3(x + 0.03f, y * 0.92f, 0f));
        }

        // 3. Motion Afterimage Trail
        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = 0.75f;
        trail.endWidth = 0.05f;
        trail.sortingOrder = 17;
        trail.material = LineMat();
        trail.startColor = new Color(0.25f, 0.85f, 1f, 0.45f);
        trail.endColor = new Color(0.1f, 0.5f, 1f, 0f);

        return root;
    }

    void SpawnWaveImpact(Vector3 pos, int dir)
    {
        GameObject burst = new GameObject("WaveImpactBurst");
        burst.transform.position = pos;

        // Cross slash flash on impact
        for (int i = 0; i < 2; i++)
        {
            GameObject lineObj = new GameObject("Slash" + i);
            lineObj.transform.SetParent(burst.transform, false);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = 0.14f;
            lr.endWidth = 0.02f;
            lr.sortingOrder = 22;
            lr.material = LineMat();
            lr.startColor = Color.white;
            lr.endColor = new Color(0.2f, 0.85f, 1f, 0.8f);

            float ang = (i == 0 ? 32f : -32f) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(ang) * dir, Mathf.Sin(ang), 0f) * 0.85f;
            lr.SetPosition(0, -d);
            lr.SetPosition(1, d);
        }

        Destroy(burst, 0.14f);
    }

    // ---------- Katana Skill 2: Judgement Cut ----------
    // ---------- Katana Skill 2: Judgement Cut (Vergil Buildup) ----------
    IEnumerator KatanaSkill2_JudgementCut()
    {
        // Mark every enemy in range first; no targets = no cost
        hitThisSlash.Clear();
        List<IDamageable> targets = new List<IDamageable>();

        foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, judgementRange, enemyLayers))
        {
            IDamageable t = col.GetComponentInParent<IDamageable>();
            if (t != null && hitThisSlash.Add(t))
                targets.Add(t);
        }

        if (targets.Count == 0)
        {
            ShowNotice("No targets in range");
            yield break;
        }

        IsUsingSkill = true;
        skill2Timer = skill2Cooldown;
        AddOverload(skill2OverloadCost);

        float total = judgementWindup + judgementHits * judgementHitInterval + judgementRecovery;
        movement.Stun(total);
        movement.PlayTrigger("Judgement");

        // Hold the targets still while she sheathes
        foreach (IDamageable t in targets)
        {
            EnemyStats es = t as EnemyStats;
            if (es != null) es.Stun(total);
        }

        // --- Vergil Buildup: Dimensional Sphere + Enemy Spatial Lock-On Reticles ---
        GameObject buildupRoot = new GameObject("JudgementBuildup_VFX");
        LineRenderer outerDome = CreateCircleRenderer(buildupRoot.transform, "OuterDome", 64, 0.05f, new Color(0.2f, 0.65f, 1f, 0.55f));
        LineRenderer implodingRing = CreateCircleRenderer(buildupRoot.transform, "ImplodingRing", 64, 0.1f, new Color(0.75f, 0.95f, 1f, 0.95f));

        List<LineRenderer[]> targetReticles = new List<LineRenderer[]>();
        foreach (IDamageable t in targets)
        {
            LineRenderer lineA = CreateLinePair(buildupRoot.transform, "LockA");
            LineRenderer lineB = CreateLinePair(buildupRoot.transform, "LockB");
            targetReticles.Add(new[] { lineA, lineB });
        }

        float w = 0f;
        while (w < judgementWindup)
        {
            w += Time.deltaTime;
            float u = Mathf.Clamp01(w / judgementWindup);
            float easeIn = u * u;

            Vector3 center = transform.position;

            // Outer dimensional dome pulses subtly, inner ring collapses onto Caly
            UpdateCircle(outerDome, center, judgementRange * Mathf.Lerp(1.05f, 0.96f, easeIn));
            UpdateCircle(implodingRing, center, Mathf.Lerp(judgementRange, 0.25f, easeIn));
            implodingRing.startWidth = Mathf.Lerp(0.04f, 0.16f, easeIn);
            implodingRing.endWidth = implodingRing.startWidth;

            // Tighten & spin spatial rift crosshairs on every marked enemy
            float reticleSize = Mathf.Lerp(1.35f, 0.35f, easeIn);
            float spinAngle = Mathf.Lerp(0f, 135f, easeIn) * Mathf.Deg2Rad;
            Color lockColor = Color.Lerp(new Color(0.2f, 0.75f, 1f, 0.85f), Color.white, easeIn);

            for (int idx = 0; idx < targets.Count; idx++)
            {
                Component c = targets[idx] as Component;
                LineRenderer[] pair = targetReticles[idx];
                if (c == null)
                {
                    pair[0].enabled = false;
                    pair[1].enabled = false;
                    continue;
                }

                Vector3 ePos = c.transform.position;
                Vector3 dirA = new Vector3(Mathf.Cos(spinAngle), Mathf.Sin(spinAngle), 0f) * reticleSize;
                Vector3 dirB = new Vector3(-dirA.y, dirA.x, 0f);

                pair[0].startColor = pair[0].endColor = lockColor;
                pair[0].SetPosition(0, ePos - dirA);
                pair[0].SetPosition(1, ePos + dirA);

                pair[1].startColor = pair[1].endColor = lockColor;
                pair[1].SetPosition(0, ePos - dirB);
                pair[1].SetPosition(1, ePos + dirB);
            }

            yield return null;
        }

        Destroy(buildupRoot);
        // -------------------------------------------------------------------------

        // The cuts land on everyone, one after another
        for (int i = 0; i < judgementHits; i++)
        {
            foreach (IDamageable t in targets)
            {
                Component c = t as Component;
                if (c == null) continue; // already destroyed

                t.TakeDamage(damagePerSlash);
                SpawnSlashLine(c.transform.position);
            }

            Debug.Log($"Judgement Cut {i + 1}/{judgementHits}");
            yield return new WaitForSeconds(judgementHitInterval);
        }

        yield return new WaitForSeconds(judgementRecovery);
        IsUsingSkill = false;
    }

    LineRenderer CreateCircleRenderer(Transform parent, string objName, int segments, float width, Color color)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sortingOrder = 22;
        lr.material = LineMat();
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    void UpdateCircle(LineRenderer lr, Vector3 center, float radius)
    {
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f));
        }
    }

    LineRenderer CreateLinePair(Transform parent, string objName)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;
        lr.sortingOrder = 23;
        lr.material = LineMat();
        return lr;
    }

    void SpawnSlashLine(Vector3 center)
    {
        if (judgementSlashPrefab != null)
        {
            Quaternion rot = randomJudgementAngle
                ? Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
                : Quaternion.identity;

            GameObject slash = Instantiate(judgementSlashPrefab, center, rot);
            Destroy(slash, judgementSlashDuration);
            return;
        }

        // Fallback placeholder if no prefab is assigned
        float ang = Random.Range(0f, 180f) * Mathf.Deg2Rad;
        Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.9f;

        GameObject g = new GameObject("JudgementSlash");
        LineRenderer l = g.AddComponent<LineRenderer>();
        l.positionCount = 2;
        l.useWorldSpace = true;
        l.startWidth = 0.07f;
        l.endWidth = 0.07f;
        l.sortingOrder = 20;
        l.material = LineMat();
        l.startColor = Color.white;
        l.endColor = Color.cyan;
        l.SetPosition(0, center - d);
        l.SetPosition(1, center + d);
        Destroy(g, 0.15f);
    }

    // ---------- Chrono-Spear Skill 1: Spear Throw ----------
    void CastSpearThrow()
    {
        if (spearAmmo <= 0)
        {
            ShowNotice("Spear not retrieved");
            return;
        }

        spearAmmo--;
        skill1Timer = spearThrowCooldown;
        AddOverload(spearThrowOverloadCost);

        int dir = movement.Facing;
        Vector3 pos = firePoint != null ? firePoint.position : transform.position + Vector3.right * dir * 0.6f;

        GameObject go;
        if (spearPrefab != null)
        {
            go = Instantiate(spearPrefab, pos, Quaternion.identity);
            Vector3 scale = go.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dir;
            go.transform.localScale = scale;
        }
        else
        {
            go = new GameObject("ChronoSpear");
            go.transform.position = pos;
        }

        SpearProjectile proj = go.GetComponent<SpearProjectile>();
        if (proj == null) proj = go.AddComponent<SpearProjectile>();
        proj.Init(this, dir);

        movement.PlayTrigger("SpearThrow");
        StartCoroutine(QuickCast(0.15f));
    }

    /// <summary>Called by the spear when it gets picked up or auto-returns.</summary>
    public void OnSpearReturned()
    {
        spearAmmo = Mathf.Min(spearAmmo + 1, spearMaxAmmo);
    }

    // ---------- Chrono-Spear Skill 2: Time Platform ----------
    void CastSpearPlatform()
    {
        if (movement.IsGrounded)
        {
            ShowNotice("Need to be in air");
            return;
        }

        skill2Timer = spearPlatformCooldown;
        AddOverload(spearPlatformOverloadCost);

        Vector2 pos = movement.groundCheck != null
            ? (Vector2)movement.groundCheck.position
            : (Vector2)transform.position + Vector2.down;
        pos.x = transform.position.x;
        pos.y -= platformSize.y * 0.5f + 0.05f;

        TimePlatform p = TimePlatform.Spawn(this, pos, movement.Facing);

        platforms.RemoveAll(x => x == null);
        platforms.Add(p);
        while (platforms.Count > maxPlatforms)
        {
            if (platforms[0] != null) Destroy(platforms[0].gameObject);
            platforms.RemoveAt(0);
        }

        movement.AirBoost(0f); // settle her onto the new platform
        movement.PlayTrigger("SpearPlatform");
        StartCoroutine(QuickCast(0.1f));
    }

    // ---------- Ultimate: Time Stop (toggle) ----------
    IEnumerator TimeStopRoutine()
    {
        timeStopEndRequested = false;

        // Pay the cast cost first. If that alone maxes Overload it is a burnout and time never stops.
        AddOverload(timeStopOverloadCost);
        if (IsBurnedOut)
        {
            ultimateTimer = timeStopCooldown;
            yield break;
        }

        TimeStop.Begin();
        IsTimeStopCasting = true;
        IsUsingSkill = true;
        movement.Stun(timeStopCastLock);
        movement.PlayTrigger("TimeStop");

        // Cast animation (cut short if a burnout ends the stop early)
        float t = 0f;
        while (t < timeStopCastLock && !timeStopEndRequested && !movement.IsDead)
        {
            t += Time.deltaTime;
            yield return null;
        }

        IsUsingSkill = false;
        IsTimeStopCasting = false;

        while (!timeStopEndRequested && !movement.IsDead)
        {
            AddOverload(timeStopOverloadPerSecond * Time.unscaledDeltaTime);
            yield return null;
        }

        IsUsingSkill = false;
        IsTimeStopCasting = false;
        TimeStop.End();                  // stored knockback is released here
        ultimateTimer = timeStopCooldown;
    }

    // ---------- Overload & Weapon Weight Rules (From GDD) ----------
    void AddOverload(float amount)
    {
        // Chrono-Spear builds Overload 2x faster per your GDD
        float multiplier = (currentWeapon == WeaponType.ChronoSpear) ? 2f : 1f;
        overloadMeter = Mathf.Clamp(overloadMeter + (amount * multiplier), 0f, 100f);

        if (overloadMeter >= 100f)
            OnOverloadMaxed();

        UpdateOverloadSpeedPenalty();
    }

    void OnOverloadMaxed()
    {
        sanity = Mathf.Min(sanity + 1, maxSanity + 1);
        overloadMeter = WeaponBaseLoad();   // bar resets (0 on Katana)
        Debug.Log($"OVERLOAD MAXED! Sanity: {sanity}/{maxSanity}");

        // Burnout: helpless, but falls normally (no mid-air freeze) and can't be hit while down
        burnoutTimer = overloadStunDuration;
        movement.Stun(overloadStunDuration);
        if (burnoutInvincible) movement.GrantInvincibility(overloadStunDuration);
        movement.PlayTrigger("Burnout");
        ShowNotice("OVERLOAD!");

        // Maxing out also ends the time stop
        if (TimeStop.Active) timeStopEndRequested = true;

        if (sanity > maxSanity)
            OnSanityBroken();
    }

    void OnSanityBroken()
    {
        // TODO (later): reset the game / reload the scene
        Debug.Log("SANITY BROKEN! Game reset goes here.");
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
                // Keep whatever the bar is; Update() drains it gradually toward 0
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
        float tier;
        if (overloadMeter >= 80f)      tier = 0.5f;
        else if (overloadMeter >= 60f) tier = 0.72f;
        else if (overloadMeter >= 20f) tier = 0.89f;
        else                           tier = 1f;

        float weapon = currentWeapon == WeaponType.ArcClaw ? arcClawSpeedMultiplier : 1f;
        movement.moveSpeed = baseMoveSpeed * tier * weapon;
    }
}