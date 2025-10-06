using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool wantJump;
    private float moveInput;

    [Header("Ground Check (Raycast)")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;
    public Vector2 rayOffset = new Vector2(0f, 0f);
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        // let's check if raycast is touching the ground 
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;


        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            wantJump = true;
    }

    void FixedUpdate()
    {
        //movement
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        // here we put jump in fixedUpdate because rigid body + add force so we don't use the time.delta time thing...
        if (wantJump)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            wantJump = false;
            isGrounded = false;
        }
    }

    /// <summary>
    /// this function allow us to visualize the ray in scene :D 
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 origin = (Vector2)transform.position + rayOffset;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }
}
