using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDummy : MonoBehaviour
{
    [Header("Target")]
    public LayerMask playerLayer;
    public float detectRange = 8f;
    [Tooltip("Player must be within this vertical distance to be chased.")]
    public float detectHeight = 3f;

    [Header("Chase")]
    public float moveSpeed = 2.5f;
    [Tooltip("Stops walking when this close horizontally.")]
    public float stopDistance = 1.1f;

    [Header("Patrol")]
    public float patrolSpeed = 1.5f;
    [Tooltip("Max distance from the start point before turning around. 0 = unlimited.")]
    public float patrolRadius = 4f;
    [Tooltip("Set this to your Ground/Wall layer(s) for instant wall turns. Leave empty to rely on the stuck check only.")]
    public LayerMask obstacleLayer;
    public float wallCheckDistance = 0.15f;
    [Tooltip("Turns around if it was pushing into something and not moving for this long.")]
    public float stuckTime = 0.2f;

    [Header("Attack")]
    public float attackDamage = 10f;
    [Tooltip("Starts an attack when the player is within this distance.")]
    public float attackTriggerDistance = 1.4f;
    public float attackRange = 0.8f;      // size of the hit circle
    public float attackOffset = 0.9f;     // how far in front of the dummy the hit lands
    public float windupTime = 0.4f;       // telegraph, player can dodge or interrupt
    public float recoveryTime = 0.4f;
    public float attackCooldown = 1f;
    public float windupScale = 1.2f;      // squash/stretch telegraph

    private EnemyStats stats;
    private Rigidbody2D rb;
    private Collider2D col;
    private PlayerController player;
    private Transform visual;
    private Vector3 visualBaseScale;

    private bool isAttacking;
    private float nextAttackTime;
    private int attackDir = 1;

    private int patrolDir = 1;
    private float startX;
    private float stuckTimer;

    void Start()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        visual = sr != null ? sr.transform : transform;
        visualBaseScale = visual.localScale;

        startX = transform.position.x;
        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (TimeStop.Active) return;
        if (stats.IsDead || stats.IsStunned || isAttacking) return;
        if (player == null || player.IsDead) return;
        if (Time.time < nextAttackTime) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist <= attackTriggerDistance)
            StartCoroutine(AttackRoutine());
    }

    void FixedUpdate()
    {
        if (stats.IsDead) return;

        // Frozen in time, or stunned: don't touch velocity so knockback can play out
        if (TimeStop.Active || stats.IsStunned)
        {
            stuckTimer = 0f;
            return;
        }

        if (isAttacking)
        {
            SetX(0f);
            stuckTimer = 0f;
            return;
        }

        if (player == null || player.IsDead)
        {
            Patrol();
            return;
        }

        float dx = player.transform.position.x - transform.position.x;
        float dy = player.transform.position.y - transform.position.y;
        bool chasing = Mathf.Abs(dx) <= detectRange && Mathf.Abs(dy) <= detectHeight;

        if (!chasing)
        {
            Patrol();
            return;
        }

        stuckTimer = 0f;

        if (Mathf.Abs(dx) <= stopDistance)
            SetX(0f);
        else
            SetX(Mathf.Sign(dx) * moveSpeed);
    }

    // ---------- Patrol ----------
    void Patrol()
    {
        if (patrolSpeed <= 0f)
        {
            SetX(0f);
            return;
        }

        // Turn around at the edge of the patrol radius
        if (patrolRadius > 0f)
        {
            float offset = transform.position.x - startX;
            if (offset > patrolRadius && patrolDir > 0) patrolDir = -1;
            else if (offset < -patrolRadius && patrolDir < 0) patrolDir = 1;
        }

        // Turn around at walls: raycast first, "not actually moving" check as a fallback
        bool blocked = WallAhead(patrolDir);

        if (!blocked)
        {
            if (Mathf.Abs(rb.linearVelocity.x) < patrolSpeed * 0.1f) stuckTimer += Time.fixedDeltaTime;
            else stuckTimer = 0f;

            blocked = stuckTimer >= stuckTime;
        }

        if (blocked)
        {
            patrolDir = -patrolDir;
            stuckTimer = 0f;
        }

        SetX(patrolDir * patrolSpeed);
    }

    bool WallAhead(int dir)
    {
        if (obstacleLayer.value == 0 || col == null) return false;

        Bounds b = col.bounds;
        RaycastHit2D hit = Physics2D.Raycast(b.center, Vector2.right * dir, b.extents.x + wallCheckDistance, obstacleLayer);
        return hit.collider != null && !hit.collider.isTrigger;
    }

    // ---------- Attack ----------
    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        SetX(0f);

        float dx = player.transform.position.x - transform.position.x;
        attackDir = dx >= 0f ? 1 : -1;

        // Wind-up (telegraph)
        visual.localScale = visualBaseScale * windupScale;

        float t = 0f;
        while (t < windupTime)
        {
            if (stats.IsDead)
            {
                CancelAttack();
                yield break;
            }

            // Frozen in time: hold the wind-up where it is
            if (TimeStop.Active)
            {
                yield return null;
                continue;
            }

            if (stats.IsStunned)
            {
                CancelAttack();
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        visual.localScale = visualBaseScale;

        // Hit
        Vector2 center = (Vector2)transform.position + new Vector2(attackDir * attackOffset, 0f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, attackRange, playerLayer);

        foreach (Collider2D c in hits)
        {
            PlayerController p = c.GetComponentInParent<PlayerController>();
            if (p != null)
            {
                p.TakeDamage(attackDamage, transform.position); // position = knockback source
                break;
            }
        }

        // Recovery (pauses during time stop)
        float r = 0f;
        while (r < recoveryTime)
        {
            if (stats.IsDead) { CancelAttack(); yield break; }
            if (!TimeStop.Active) r += Time.deltaTime;
            yield return null;
        }

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
    }

    void CancelAttack()
    {
        visual.localScale = visualBaseScale;
        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
    }

    void SetX(float x)
    {
        rb.linearVelocity = new Vector2(x, rb.linearVelocity.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 center = (Vector2)transform.position + new Vector2((attackDir == 0 ? 1 : attackDir) * attackOffset, 0f);
        Gizmos.DrawWireSphere(center, attackRange);
    }
}