using System.Collections;
using UnityEngine;

/// <summary>
/// Musuh melayang (jetpack). Idle -> Windup (telegraph) -> Dash -> Recovery -> kembali ke titik awal.
/// Kompatibel dengan EnemyStats (stun, knockback, flash merah, time stop) dan PlayerController.
/// Butuh state Animator bernama: Idle, Windup, Dash.
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDasher : MonoBehaviour
{
    [Header("Target")]
    public LayerMask playerLayer;
    [Tooltip("Dash dimulai kalau player sedekat ini (horizontal).")]
    public float triggerDistance = 5f;
    [Tooltip("Player harus dalam jarak vertikal ini.")]
    public float triggerHeight = 3f;
    [Tooltip("Musuh menembus badan player (tidak saling dorong). Damage tetap lewat hit check.")]
    public bool passThroughPlayer = true;

    [Header("Idle Hover")]
    public float bobAmount = 0.15f;
    public float bobSpeed = 3f;

    [Header("Chase")]
    [Tooltip("Musuh terus mengejar player selama player dalam jarak ini. 0 = tanpa batas.")]
    public float detectRange = 15f;
    public float chaseSpeed = 3f;
    [Tooltip("Berhenti mendekat kalau sudah sedekat ini.")]
    public float stopDistance = 1.5f;
    [Tooltip("Ikut naik/turun mengikuti player.")]
    public bool chaseVertically = true;
    [Tooltip("Melayang sedikit di atas titik tengah player.")]
    public float chaseHeightOffset = 0.5f;

    [Header("Dash")]
    public float windupTime = 0.5f;       // telegraph, player bisa menghindar / menginterupsi
    public float dashSpeed = 12f;
    public float dashDuration = 0.35f;
    public float recoveryTime = 0.5f;
    public float dashCooldown = 1.5f;
    [Range(0f, 1f)]
    [Tooltip("0 = dash lurus horizontal. 1 = dash tepat ke arah player (termasuk naik/turun).")]
    public float verticalAim = 0.5f;
    [Tooltip("Layer Ground/Wall. Dash berhenti kalau menabrak.")]
    public LayerMask obstacleLayer;
    public float wallCheckDistance = 0.1f;

    [Header("Damage")]
    public float dashDamage = 15f;
    public float hitRadius = 0.5f;
    [Tooltip("Dash langsung berhenti setelah mengenai player.")]
    public bool endDashOnHit = true;

    [Header("Visual")]
    public Animator animator;             // kosong = cari otomatis di child
    public Transform visual;              // child yang berisi sprite

    private EnemyStats stats;
    private Rigidbody2D rb;
    private Collider2D col;
    private PlayerController player;
    private Collider2D playerCol;

    private bool isDashing;
    private bool inWindup;
    private bool inRecovery;
    private float nextDashTime;
    private Vector2 dashDir = Vector2.right;

    private float bobTime;
    private Vector3 visualBasePos;
    private string currentAnim = "";

    void Start()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (visual == null) visual = animator != null ? animator.transform : transform;
        visualBasePos = visual.localPosition;

        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerCol = player.GetComponent<Collider2D>();
            if (passThroughPlayer) IgnorePlayerCollision();
        }

        PlayAnim("Idle");
    }

    void IgnorePlayerCollision()
    {
        Collider2D[] mine = GetComponentsInChildren<Collider2D>();
        Collider2D[] theirs = player.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D m in mine)
            foreach (Collider2D t in theirs)
                if (m != null && t != null) Physics2D.IgnoreCollision(m, t, true);
    }

    Vector2 AimPoint
    {
        get
        {
            if (playerCol != null) return playerCol.bounds.center;
            return player.transform.position;
        }
    }

    void Update()
    {
        if (stats.IsDead) return;

        // Animasi ikut berhenti saat time stop
        if (animator != null) animator.speed = TimeStop.Active ? 0f : 1f;
        if (TimeStop.Active) return;

        if (!isDashing && !inWindup) Hover();

        if (stats.IsStunned || isDashing || inWindup || inRecovery) return;
        if (player == null || player.IsDead) return;
        if (Time.time < nextDashTime) return;

        Vector2 to = AimPoint - (Vector2)transform.position;
        if (Mathf.Abs(to.x) <= triggerDistance && Mathf.Abs(to.y) <= triggerHeight)
            StartCoroutine(DashRoutine());
    }

    void FixedUpdate()
    {
        // Stun / time stop: jangan sentuh velocity supaya knockback EnemyStats bisa berjalan
        if (stats.IsDead || TimeStop.Active || stats.IsStunned) return;

        if (isDashing)
            rb.linearVelocity = dashDir * dashSpeed;
        else if (inWindup || inRecovery)
            rb.linearVelocity = Vector2.zero;
        else
            Chase();
    }

    // Terus mengejar player (juga saat cooldown), kecuali sedang windup / dash / recovery
    void Chase()
    {
        if (player == null || player.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target = AimPoint + Vector2.up * chaseHeightOffset;
        Vector2 to = target - rb.position;
        if (!chaseVertically) to.y = 0f;

        if (detectRange > 0f && to.magnitude > detectRange)
        {
            rb.linearVelocity = Vector2.zero;   // player terlalu jauh
            return;
        }

        if (to.magnitude <= stopDistance)
            rb.linearVelocity = Vector2.zero;
        else
            rb.linearVelocity = to.normalized * chaseSpeed;
    }

    // ---------- Dash ----------
    IEnumerator DashRoutine()
    {
        // ---------- Windup ----------
        inWindup = true;
        PlayAnim("Windup");

        float t = 0f;
        while (t < windupTime)
        {
            if (stats.IsDead || stats.IsStunned) { CancelDash(); yield break; }
            if (TimeStop.Active) { yield return null; continue; }

            // Terus membidik player selama windup
            if (player != null && !player.IsDead)
            {
                Vector2 to = AimPoint - (Vector2)transform.position;
                to.y *= verticalAim;
                if (to.sqrMagnitude > 0.0001f) dashDir = to.normalized;
                if (Mathf.Abs(dashDir.x) > 0.05f) Face(dashDir.x >= 0f ? 1 : -1);
            }

            t += Time.deltaTime;
            yield return null;
        }

        inWindup = false;

        // ---------- Dash ----------
        isDashing = true;
        PlayAnim("Dash");
        bool hasHit = false;
        float d = 0f;

        while (d < dashDuration)
        {
            if (stats.IsDead || stats.IsStunned) { CancelDash(); yield break; }
            if (TimeStop.Active) { yield return null; continue; }

            if (WallAhead()) break;

            if (!hasHit)
            {
                Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
                Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, playerLayer);
                foreach (Collider2D c in hits)
                {
                    PlayerController p = c.GetComponentInParent<PlayerController>();
                    if (p == null) continue;

                    // Player lagi dash / i-frame: dash menembus, damage tidak "terpakai"
                    if (!p.IsInvincible)
                    {
                        p.TakeDamage(dashDamage, transform.position);
                        hasHit = true;
                    }
                    break;
                }

                if (hasHit && endDashOnHit) break;
            }

            d += Time.deltaTime;
            yield return null;
        }

        // ---------- Recovery ----------
        isDashing = false;
        inRecovery = true;
        rb.linearVelocity = Vector2.zero;
        PlayAnim("Idle");
        Face(1);

        float r = 0f;
        while (r < recoveryTime)
        {
            if (stats.IsDead) { CancelDash(); yield break; }
            if (!TimeStop.Active) r += Time.deltaTime;
            yield return null;
        }

        inRecovery = false;
        nextDashTime = Time.time + dashCooldown;
    }

    void CancelDash()
    {
        isDashing = false;
        inWindup = false;
        inRecovery = false;
        nextDashTime = Time.time + dashCooldown;
        PlayAnim("Idle");
        Face(1);
    }

    bool WallAhead()
    {
        if (obstacleLayer.value == 0 || col == null) return false;

        Bounds b = col.bounds;
        float dist = Mathf.Max(b.extents.x, b.extents.y) + wallCheckDistance;
        RaycastHit2D hit = Physics2D.Raycast(b.center, dashDir, dist, obstacleLayer);
        return hit.collider != null && !hit.collider.isTrigger;
    }

    // ---------- Visual ----------
    void PlayAnim(string state)
    {
        if (animator == null || currentAnim == state) return;
        currentAnim = state;
        animator.Play(state, 0, 0f);
    }

    void Face(int dir)
    {
        if (visual == null) return;
        Vector3 s = visual.localScale;
        s.x = Mathf.Abs(s.x) * dir;   // -1 = menghadap kiri
        visual.localScale = s;
    }

    void Hover()
    {
        if (visual == transform) return;
        bobTime += Time.deltaTime * bobSpeed;
        visual.localPosition = visualBasePos + Vector3.up * Mathf.Sin(bobTime) * bobAmount;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(triggerDistance * 2f, triggerHeight * 2f, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
