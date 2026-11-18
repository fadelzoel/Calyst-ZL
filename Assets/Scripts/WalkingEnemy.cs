using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WalkingEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public bool startFacingRight = false;

    [Header("Wall Detection")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.25f;
    public LayerMask groundLayer;

    [Header("Contact Damage")]
    public int contactDamage = 10;
    public float damageCooldown = 0.8f;

    private Rigidbody2D rb;
    private EnemyStats stats;
    private int direction;
    private float nextDamageTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<EnemyStats>();
        direction = startFacingRight ? 1 : -1;
        ApplyFacing();
    }

    void FixedUpdate()
    {
        // EnemyStats automatically sets IsStunned = true during knockback hit-stun and stuns
        if (stats != null && (stats.IsDead || stats.IsStunned)) return;

        // Flip only when hitting a wall (ignores ledges so it can fall)
        if (HitWall())
            Flip();

        // Keep vertical velocity untouched so gravity pulls it off ledges
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
    }

    bool HitWall()
    {
        Vector2 origin = wallCheck != null ? (Vector2)wallCheck.position : (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, groundLayer);
        return hit.collider != null;
    }

    void Flip()
    {
        direction *= -1;
        ApplyFacing();
    }

    void ApplyFacing()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void OnCollisionEnter2D(Collision2D collision) => TryDamagePlayer(collision.gameObject);
    void OnCollisionStay2D(Collision2D collision) => TryDamagePlayer(collision.gameObject);

    void TryDamagePlayer(GameObject target)
    {
        if (TimeStop.Active) return;
        if (stats != null && (stats.IsDead || stats.IsStunned)) return;
        if (Time.time < nextDamageTime) return;
        if (!target.CompareTag("Player")) return;

        PlayerController pc = target.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(contactDamage);
            nextDamageTime = Time.time + damageCooldown;
            return;
        }

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(contactDamage);
            nextDamageTime = Time.time + damageCooldown;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = wallCheck != null ? wallCheck.position : transform.position;
        int dir = Application.isPlaying ? direction : (startFacingRight ? 1 : -1);
        Gizmos.DrawLine(origin, origin + Vector3.right * dir * wallCheckDistance);
    }
}