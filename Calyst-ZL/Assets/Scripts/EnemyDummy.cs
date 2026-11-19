using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDummy : MonoBehaviour
{
    [Header("Target")]
    public LayerMask playerLayer;
    public float detectRange = 8f;

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    [Tooltip("Stops walking when this close horizontally.")]
    public float stopDistance = 1.1f;

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
    private PlayerController player;
    private Transform visual;
    private Vector3 visualBaseScale;

    private bool isAttacking;
    private float nextAttackTime;
    private int attackDir = 1;

    void Start()
    {
        stats = GetComponent<EnemyStats>();
        rb = GetComponent<Rigidbody2D>();

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        visual = sr != null ? sr.transform : transform;
        visualBaseScale = visual.localScale;

        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
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

        // Don't touch velocity while stunned, so knockback can play out
        if (stats.IsStunned) return;

        if (isAttacking || player == null || player.IsDead)
        {
            SetX(0f);
            return;
        }

        float dx = player.transform.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        if (dist > detectRange || dist <= stopDistance)
        {
            SetX(0f);
            return;
        }

        SetX(Mathf.Sign(dx) * moveSpeed);
    }

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
            if (stats.IsDead || stats.IsStunned)
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

        foreach (Collider2D col in hits)
        {
            PlayerController p = col.GetComponentInParent<PlayerController>();
            if (p != null)
            {
                p.TakeDamage(attackDamage);
                break;
            }
        }

        // Recovery
        yield return new WaitForSeconds(recoveryTime);

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