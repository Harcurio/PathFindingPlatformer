using UnityEngine;
using UnityEngine.InputSystem;

public class AdditiveGroundedJump2D : MonoBehaviour
{
    [Header("Jump")]
    public float jumpVelocity = 12f;         // vertical speed to set when jumping

    [Header("Ground Check")]
    public Transform groundCheck;            // empty child at the player's feet
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;             // set to your Ground layer(s)

    [Header("Input (new Input System)")]
    public InputActionReference jumpAction;  // drag the Jump action here

    [Header("Optional: Physics horizontal (off by default)")]
    public bool overrideHorizontal = false;  // leave false to keep your old movement
    public float moveSpeed = 10f;
    public InputActionReference moveAction;  // drag the Move action if you enable override

    private Rigidbody2D rb;
    private bool isGrounded;
    private Vector2 move;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (jumpAction != null) jumpAction.action.Enable();
        if (moveAction != null) moveAction.action.Enable();
    }

    private void OnEnable()
    {
        if (jumpAction != null)
            jumpAction.action.performed += OnJump;
    }

    private void OnDisable()
    {
        if (jumpAction != null)
            jumpAction.action.performed -= OnJump;
    }

    private void Update()
    {
        if (overrideHorizontal && moveAction != null)
            move = moveAction.action.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // robust grounded check
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        // optional physics-based horizontal
        if (overrideHorizontal)
            rb.velocity = new Vector2(move.x * moveSpeed, rb.velocity.y);
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (isGrounded)
        {
            // set vertical velocity directly for a crisp jump
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
