using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerMovement : MonoBehaviour, IMovement
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
        /*
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;
        */

        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded()){
            wantJump = true;
        }
    }

    void FixedUpdate()
    {
        //movement
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
        Physics2D.SyncTransforms();

        // here we put jump in fixedUpdate because rigid body + add force so we don't use the time.delta time thing...
        if (wantJump)
        {
            Jump();
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


    /////////////////////////////////
     public void Jump(){
        //Debug.Log("called jump: isGrounded? " + isGrounded);
        //Debug.Log("called jump");
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        //wantJump = false;
        //isGrounded = false;
        //Debug.Log("isGrounded?" + isGrounded);
    }

    public void MoveLeft(){
        //Debug.Log("called move left: wantLeft? " + wantLeft);
        //move
        rb.velocity = new Vector2((-1) * moveSpeed, rb.velocity.y);
        //wantLeft = false;
    }


    public void MoveRight(){
        //Debug.Log("called move right: wantRight? " + wantRight);
        //move
        rb.velocity = new Vector2((1) * moveSpeed, rb.velocity.y);
        //wantRight = false;
    }

    public bool IsGrounded(){
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = (hit.collider != null);
        return isGrounded;
    }
    ////////////////////////////////////////
}
