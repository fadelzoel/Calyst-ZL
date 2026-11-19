using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMovement : MonoBehaviour
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

    /// <summary>1 = facing right, -1 = facing left. Read by PlayerCombat.</summary>
    public int Facing { get; private set; } = 1;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private PlayerCombat combat;
    private bool isGrounded;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool jumpReleased;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private float lungeTimer;
    private float lungeSpeed;
    private float stunTimer;

    public bool IsStunned => stunTimer > 0f;

        private CapsuleCollider2D capsule;
    private float baseOffsetX;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        combat = GetComponent<PlayerCombat>();

        capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
            baseOffsetX = capsule.offset.x;
    }

    // ---------- Input callbacks ----------
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
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

    // ---------- Logic ----------
    void Update()
    {
        UpdateFacing();
        HandleJump();

        jumpPressed = false;
        jumpReleased = false;
    }

    void UpdateFacing()
    {
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

    void HandleJump()
    {
        if (IsStunned)
        {
            jumpBufferCounter = 0f;
            return;
        }
        
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

    void FixedUpdate()
{
    float xVel;

    if (lungeTimer > 0f)
    {
        xVel = lungeSpeed * Facing;
        lungeTimer -= Time.fixedDeltaTime;
    }
    else if (stunTimer > 0f)
    {
        xVel = 0f;
        stunTimer -= Time.fixedDeltaTime;
    }
    else
    {
        xVel = moveInput.x * moveSpeed;
    }

    rb.linearVelocity = new Vector2(xVel, rb.linearVelocity.y);
    isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
}

    public void Lunge(float speed, float duration)
    {
        lungeSpeed = speed;
        lungeTimer = duration;
    }

    public void Stun(float duration)
    {
        stunTimer = duration;
        lungeTimer = 0f; // stun cancels any leftover lunge
    }    
}