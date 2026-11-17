using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;

    [Header("Jump Settings")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float jumpCutMultiplier = 0.5f;
    public bool allowBunnyHop = false;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float checkRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Facing")]
    [Tooltip("Tick this if your sprite art faces RIGHT by default. Untick if it faces LEFT.")]
    public bool spriteFacesRight = true;

    [Header("Dash Settings")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.6f;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Invincibility Frames")]
    [Tooltip("Seconds you are immune after taking damage.")]
    public float invincibilityDuration = 1f;
    public float hitFlashDuration = 0.15f;

    /// <summary>1 = facing right, -1 = facing left. Read by PlayerCombat.</summary>
    public int Facing { get; private set; } = 1;

    public bool IsStunned => stunTimer > 0f;
    public bool IsDead { get; private set; }
    public bool IsDashing => isDashing;

    /// <summary>True while dashing or during post-hit i-frames.</summary>
    public bool IsInvincible => isDashing || isHurtInvincible;

    // Components
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private PlayerCombat combat;
    private CapsuleCollider2D capsule;
    private float baseOffsetX;

    // Movement state
    private bool isGrounded;
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool jumpReleased;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    // Dash state
    private float dashTimer;
    private float dashCooldownTimer;
    private bool isDashing;
    public bool IsGrounded => isGrounded;

    // Lunge / stun state
    private float lungeTimer;
    private float lungeSpeed;
    private float stunTimer;

    // Health state
    private bool isHurtInvincible;
    private bool isHitFlashing;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        combat = GetComponent<PlayerCombat>();

        capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
            baseOffsetX = capsule.offset.x;

        currentHealth = maxHealth;
    }

    // ---------- Input callbacks ----------
    public void OnMove(InputValue value)
    {
        moveInput = IsDead ? Vector2.zero : value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (IsDead) return;

        if (value.isPressed)
        {
            jumpPressed = true;
            jumpHeld = true;
        }
        else
        {
            jumpReleased = true;
            jumpHeld = false;
        }
    }

    public void OnDash(InputValue value)
    {
        if (IsDead) return;

        if (value.isPressed
            && dashCooldownTimer <= 0f
            && !isDashing
            && !IsStunned
            && (combat == null || !combat.IsAttacking))
        {
            StartDash();
        }
    }

    // ---------- Update ----------
    void Update()
    {
        

        if (!IsDead)
        {
            UpdateFacing();
            HandleJump();
        }

        jumpPressed = false;
        jumpReleased = false;

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        float xVel;

        if (IsDead)
        {
            xVel = 0f;
        }
        else if (isDashing)
        {
            xVel = dashSpeed * Facing;
            dashTimer -= Time.fixedDeltaTime;

            if (dashTimer <= 0f)
                EndDash();
        }
        else if (lungeTimer > 0f)
        {
            xVel = lungeSpeed * Facing;
            lungeTimer -= Time.fixedDeltaTime;
        }
        else if (stunTimer > 0f)
        {
            xVel = 0f;
            stunTimer -= Time.fixedDeltaTime;
        }
        else if (combat != null && combat.IsAttacking)
        {
            xVel = 0f; // rooted during the combo (lunges above still move her)
        }
        else
        {
            xVel = moveInput.x * moveSpeed;
        }

        rb.linearVelocity = new Vector2(xVel, rb.linearVelocity.y);
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
    }

    // ---------- Facing ----------
    void UpdateFacing()
    {
        if (isDashing) return; // lock direction during dash
        if (combat != null && combat.IsAttacking) return;

        if (moveInput.x > 0.1f) Facing = 1;
        else if (moveInput.x < -0.1f) Facing = -1;
        else return;

        if (sr != null)
            sr.flipX = spriteFacesRight ? (Facing == -1) : (Facing == 1);

        // Mirror the collider offset along with the sprite
        if (capsule != null)
        {
            int side = spriteFacesRight ? Facing : -Facing;
            capsule.offset = new Vector2(baseOffsetX * side, capsule.offset.y);
        }
    }

    // ---------- Jump ----------
    void HandleJump()
    {
        if (jumpPressed || (allowBunnyHop && jumpHeld))
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        if (isGrounded && rb.linearVelocity.y <= 0.01f)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        bool alreadyCut = false;

        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            isGrounded = false;

            if (!jumpHeld)
            {
                CutJump();
                alreadyCut = true;
            }
        }

        if (jumpReleased && !alreadyCut)
            CutJump();
    }

    void CutJump()
    {
        if (rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
    }

    // ---------- Dash ----------
    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        lungeTimer = 0f;
        RefreshColor();
    }

    void EndDash()
    {
        isDashing = false;
        RefreshColor();
    }

    // ---------- Lunge / Stun (called by PlayerCombat) ----------
    public void Lunge(float speed, float duration)
    {
        lungeSpeed = speed;
        lungeTimer = duration;
    }

    public void Stun(float duration)
    {
        stunTimer = duration;
        lungeTimer = 0f; // stun cancels any leftover lunge
        if (isDashing) EndDash();
    }

    public void AirBoost(float upSpeed)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, upSpeed);
    }

    // ---------- Health ----------
    public void TakeDamage(float amount)
    {
        if (IsDead || IsInvincible) return;

        currentHealth -= amount;
        Debug.Log($"Player took {amount} damage! HP remaining: {currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            StartCoroutine(HurtRoutine());
        }
    }

    IEnumerator HurtRoutine()
    {
        isHurtInvincible = true;
        isHitFlashing = true;
        RefreshColor();

        float flash = Mathf.Min(hitFlashDuration, invincibilityDuration);
        yield return new WaitForSeconds(flash);

        isHitFlashing = false;
        RefreshColor();

        yield return new WaitForSeconds(Mathf.Max(0f, invincibilityDuration - flash));

        isHurtInvincible = false;
    }

    void Die()
    {
        IsDead = true;
        StopAllCoroutines();
        isHurtInvincible = false;
        isHitFlashing = false;
        isDashing = false;
        lungeTimer = 0f;
        stunTimer = 0f;
        moveInput = Vector2.zero;

        Debug.Log("Player Died! (Game Over logic will go here later)");
        RefreshColor();
    }

    // ---------- Sprite color (single place so effects don't fight) ----------
    void RefreshColor()
    {
        if (sr == null) return;

        if (IsDead) sr.color = Color.gray;
        else if (isHitFlashing) sr.color = Color.red;
        else if (isDashing) sr.color = new Color(1f, 1f, 1f, 0.5f);
        else sr.color = Color.white;
    }
}