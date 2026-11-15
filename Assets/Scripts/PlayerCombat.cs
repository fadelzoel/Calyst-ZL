using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public interface IDamageable
{
    void TakeDamage(int amount);
}

[RequireComponent(typeof(PlayerMovement))]
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

    public bool IsAttacking => isAttacking;

    private PlayerMovement movement;
    private int comboStep = 0;
    private bool isAttacking = false;
    private float lastAttackEndTime;
    private float attackBufferCounter;
    private float attackPointOffsetX;
    private bool attackPressed;

    private readonly HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    void Start()
    {
        movement = GetComponent<PlayerMovement>();

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
        UpdateAttackPoint();

        if (attackPressed)
            attackBufferCounter = inputBufferTime;
        else
            attackBufferCounter -= Time.deltaTime;

        attackPressed = false;

        if (!isAttacking && attackBufferCounter > 0f)
        {
            if (Time.time - lastAttackEndTime > comboResetTime)
                comboStep = 0;

            StartCoroutine(AttackRoutine());
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

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        attackBufferCounter = 0f;

        comboStep = (comboStep % 3) + 1;
        bool isFinisher = comboStep == 3;
        Debug.Log("Attack Combo Step: " + comboStep);

        // Dash forward into the hit
        float speed = comboLungeSpeeds.Length >= comboStep ? comboLungeSpeeds[comboStep - 1] : 0f;
        movement.Lunge(speed, isFinisher ? finisherLungeDuration : lungeDuration);

        if (comboStep == 3 && movement != null)
        {
            movement.Lunge(12f, 0.15f); // (Speed, Duration)
        }

        // animator.SetInteger("ComboStep", comboStep);
        // animator.SetTrigger("Attack");

        yield return new WaitForSeconds(hitDelay);
        DealDamage(comboStep);

        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - hitDelay));

        // Finisher recovery: she can't move, jump, turn, or attack for a moment
        if (isFinisher)
        {
            movement.Stun(finisherStunTime);
            yield return new WaitForSeconds(finisherStunTime);
        }

        isAttacking = false;
        lastAttackEndTime = Time.time;
    }

    void DealDamage(int step)
    {
        alreadyHit.Clear();

        float multiplier = comboDamageMultipliers.Length >= step ? comboDamageMultipliers[step - 1] : 1f;
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
                damageable.TakeDamage(damage);
                if (step == 3)
                {
                    EnemyHealth enemyScript = target.GetComponent<EnemyHealth>();
                    if (enemyScript != null)
                    {
                        enemyScript.Stun(0.5f); // Stun for 0.5 seconds
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