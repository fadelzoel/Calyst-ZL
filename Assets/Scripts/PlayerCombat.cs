using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public interface IDamageable
{
    void TakeDamage(int amount);
}

[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Settings")]
    public Transform attackPoint;
    public float attackRange = 0.5f;
    public LayerMask enemyLayers;
    public int attackDamage = 10;

    [Header("Combo Settings")]
    public float[] comboDamageMultipliers = { 1f, 1f, 1.5f };
    public float hitDelay = 0.08f;
    public float attackDuration = 0.3f;
    public float comboResetTime = 0.8f;
    public float inputBufferTime = 0.2f;

    [Header("Lunge & Finisher")]
    public float[] comboLungeSpeeds = { 3f, 3f, 12f };  // hit 1, 2, 3
    public float lungeDuration = 0.1f;
    public float finisherLungeDuration = 0.15f;
    public float finisherStunTime = 0.35f;              // recovery lag after hit 3
    public float finisherEnemyStun = 0.5f;

    [Header("Air Attack (1 per airtime)")]
    public float airDamageMultiplier = 1.2f;
    [Tooltip("Upward speed set when the air attack starts. Jump force is 12, so keep this lower.")]
    public float airBoostSpeed = 6f;
    [Tooltip("How long she is locked (can't move/turn) during the air attack.")]
    public float airAttackDuration = 0.35f;
    public float airAttackHitDelay = 0.08f;
    public float airEnemyStun = 0.3f;

    [Header("Knockback")]
    public float hitKnockback = 4f;
    public float finisherKnockback = 9f;
    public float airKnockback = 5f;

    public bool IsAttacking => isAttacking;

    private PlayerController movement;
    private int comboStep = 0;
    private bool isAttacking = false;
    private float lastAttackEndTime;
    private float attackBufferCounter;
    private float attackPointOffsetX;
    private bool attackPressed;

    private bool airAttackUsed;
    private float airAttackTime = -10f;

    private readonly HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    void Start()
    {
        movement = GetComponent<PlayerController>();

        if (attackPoint != null)
            attackPointOffsetX = Mathf.Abs(attackPoint.localPosition.x);
    }

    // ---------- Input callback ----------
    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            attackPressed = true;
    }

    // ---------- Logic ----------
    void Update()
    {
        if (movement == null) return;

        UpdateAttackPoint();

        // Landing resets the air attack (small lockout so the frames right after takeoff don't count)
        if (movement.IsGrounded && Time.time > airAttackTime + 0.2f)
            airAttackUsed = false;

        if (attackPressed)
            attackBufferCounter = inputBufferTime;
        else
            attackBufferCounter -= Time.deltaTime;

        attackPressed = false;

        if (movement.IsDead) return;

        bool canAttack = !isAttacking && !movement.IsDashing && !movement.IsStunned;

        if (canAttack && attackBufferCounter > 0f)
        {
            if (movement.IsGrounded)
            {
                if (Time.time - lastAttackEndTime > comboResetTime)
                    comboStep = 0;

                StartCoroutine(AttackRoutine());
            }
            else if (!airAttackUsed)
            {
                StartCoroutine(AirAttackRoutine());
            }
            // else: already used the air attack, buffer just expires (or becomes a ground attack if she lands in time)
        }
    }

    // Moves the attack point to whichever side the player is facing
    void UpdateAttackPoint()
    {
        if (isAttacking || attackPoint == null) return;

        Vector3 pos = attackPoint.localPosition;
        pos.x = attackPointOffsetX * movement.Facing;
        attackPoint.localPosition = pos;
    }

    // ---------- Ground combo ----------
    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        attackBufferCounter = 0f;

        comboStep = (comboStep % 3) + 1;
        bool isFinisher = comboStep == 3;
        Debug.Log("Attack Combo Step: " + comboStep);

        float speed = comboLungeSpeeds.Length >= comboStep ? comboLungeSpeeds[comboStep - 1] : 0f;
        movement.Lunge(speed, isFinisher ? finisherLungeDuration : lungeDuration);

        yield return new WaitForSeconds(hitDelay);

        if (movement.IsDead)
        {
            isAttacking = false;
            yield break;
        }

        float multiplier = comboDamageMultipliers.Length >= comboStep ? comboDamageMultipliers[comboStep - 1] : 1f;
        DealDamage(multiplier, isFinisher ? finisherKnockback : hitKnockback, isFinisher ? finisherEnemyStun : 0f);

        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));

        // Finisher recovery: can't move, jump, turn, or attack for a moment
        if (isFinisher && !movement.IsDead)
        {
            movement.Stun(finisherStunTime);
            yield return new WaitForSeconds(finisherStunTime);
        }

        isAttacking = false;
        lastAttackEndTime = Time.time;
    }

    // ---------- Air attack ----------
    IEnumerator AirAttackRoutine()
    {
        isAttacking = true;
        airAttackUsed = true;
        airAttackTime = Time.time;
        attackBufferCounter = 0f;
        comboStep = 0; // air attack doesn't continue the ground combo

        Debug.Log("Air Attack!");

        // Small hop + lock horizontal control while she swings
        movement.AirBoost(airBoostSpeed);
        movement.Stun(airAttackDuration);

        yield return new WaitForSeconds(airAttackHitDelay);

        if (movement.IsDead)
        {
            isAttacking = false;
            yield break;
        }

        DealDamage(airDamageMultiplier, airKnockback, airEnemyStun);

        yield return new WaitForSeconds(Mathf.Max(0f, airAttackDuration - airAttackHitDelay));

        isAttacking = false;
        lastAttackEndTime = Time.time;
    }

    // ---------- Damage ----------
    void DealDamage(float multiplier, float knockbackForce, float enemyStun)
    {
        if (attackPoint == null) return;

        alreadyHit.Clear();

        int damage = Mathf.RoundToInt(attackDamage * multiplier);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D col in hits)
        {
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform))
                continue;

            GameObject target = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (!alreadyHit.Add(target))
                continue;

            Debug.Log("We hit " + target.name + " for " + damage);

            IDamageable damageable = target.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);

                EnemyStats enemyScript = target.GetComponent<EnemyStats>();
                if (enemyScript != null)
                {
                    // Knockback first: a frozen (stunned) enemy ignores knockback
                    if (knockbackForce > 0f)
                        enemyScript.Knockback(new Vector2(movement.Facing, 0.25f), knockbackForce);

                    if (enemyStun > 0f)
                        enemyScript.Stun(enemyStun);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}