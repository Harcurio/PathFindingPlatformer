using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]   // makes sure this player always has a Rigidbody2D
[RequireComponent(typeof(BoxCollider2D))] // and also has a BoxCollider2D (so we can check for ground)
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;   // how fast I move left and right
    public float jumpForce = 10f;  // how strong my jump is

    private Rigidbody2D rb;        // reference to my Rigidbody so I can move using physics
    private BoxCollider2D box;     // reference to my collider so I can tell where my feet are

    private bool isGrounded;       // true if I'm standing on the ground
    private bool wantJump;         // true for one frame when I hit space to jump
    private float moveInput;       // stores left/right movement (-1, 0, or 1)

    [Header("Ground Check (Raycast)")]
    public float groundCheckDistance = 0.15f; // how far below my feet the ray looks for ground
    public LayerMask groundLayer;             // only objects on this layer count as "ground"

    public Vector2 rayOffset = Vector2.zero;  // not used anymore but leaving it here just in case

    void Start()
    {
        rb = GetComponent<Rigidbody2D>(); // grab my Rigidbody2D
        box = GetComponent<BoxCollider2D>(); // grab my BoxCollider2D
    }

    void Update()
    {
        // --- HORIZONTAL MOVEMENT INPUT ---
        // Get my A/D or arrow key input.
        moveInput = Input.GetAxisRaw("Horizontal");

        // if it's smaller than 0.2, just ignore it (this stops tiny drifts when I’m not touching anything)
        if (Mathf.Abs(moveInput) < 0.2f) moveInput = 0f;

        // --- GROUND CHECK ---
        // call my custom function that checks if the ray under my feet hits the ground
        isGrounded = Grounded();

        // --- JUMP INPUT ---
        // when I press space, mark that I want to jump (the actual jump happens in FixedUpdate)
        if (Input.GetKeyDown(KeyCode.Space))
            wantJump = true;
    }

    void FixedUpdate()
    {
        // --- APPLY MOVEMENT ---
        // this makes me move left/right by directly setting my Rigidbody’s velocity
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        // re-check if I'm grounded right before jumping (just in case I walked off a ledge)
        isGrounded = Grounded();

        // --- APPLY JUMP ---
        // if I pressed space and I'm grounded, time to jump!
        if (wantJump && isGrounded)
        {
            // reset my vertical velocity so I always jump the same height (no stacking jumps)
            rb.velocity = new Vector2(rb.velocity.x, 0f);

            // add an instant upward force to make me jump
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        // clear the jump request so it only happens once per press
        wantJump = false;
    }

    // --- CHECK IF I'M ON THE GROUND ---
    bool Grounded()
    {
        // find the center-bottom of my collider (my feet)
        Vector2 origin = (Vector2)box.bounds.center + Vector2.down * (box.bounds.extents.y + 0.02f);

        // send a small invisible line (raycast) down to see if it hits the ground layer
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);

        // draw a green line in the Scene view so I can actually see where it checks (only shows in editor)
        Debug.DrawLine(origin, origin + Vector2.down * groundCheckDistance, Color.green);

        // if the ray hits something, that means I'm on the ground
        return hit.collider != null;
    }

    // --- VISUALIZE THE RAY IN THE EDITOR ---
    void OnDrawGizmosSelected()
    {
        // draws a blue line under my feet when I select the Player (just helps me debug visually)
        if (GetComponent<BoxCollider2D>() != null)
        {
            var b = GetComponent<BoxCollider2D>().bounds;
            Vector2 origin = (Vector2)b.center + Vector2.down * (b.extents.y + 0.02f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
        }
    }
}

