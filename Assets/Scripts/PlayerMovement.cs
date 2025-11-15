
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

    /////////////////////
    //private bool wantLeft;
    //private bool wantRight;
    //private int n;
    private int a;
    private int d;
    /////////////////////

    [Header("Ground Check (Raycast)")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;
    public Vector2 rayOffset = new Vector2(0f, 0f);

    [Header("Ground Check (Raycast2)")]
    public int x;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        //n = 0;
        a = 0;
        d = 0;
    }


    void Update()
    {   
        //print("called countInput: " + n + " times, " + "left count: " + a + ", right count: " + d);
        //print("left count: " + a + ", right count: " + d + ", moveInput = " + moveInput);
        
        //moveInput = Input.GetAxisRaw("Horizontal");
        //Debug.Log("rb velocity: " + rb.velocity + ", moveInput: " + moveInput);
        countInput();

        //if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            wantJump = true;
        }
    }


    void FixedUpdate()
    {
        //movement
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

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


    ///////////////////////////////
    void countInput(){
        moveInput = Input.GetAxisRaw("Horizontal");

        if(moveInput < 0){
            a++;
        }
        else if(moveInput > 0){
            d++;
        }
        /*
        if(Input.GetKeyDown(KeyCode.A)){
            moveInput = -1;
            a++;
        }
        if (Input.GetKeyDown(KeyCode.D)){
            moveInput = 1;
            d++;
        }
        */
        //rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }


    public void Jump(){
        //Debug.Log("called jump: isGrounded? " + isGrounded);
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }


    public bool IsGrounded(){
        //check if player can jump right now
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = (hit.collider != null);
        return isGrounded;
    }

    public float GetJumpForce(){
        return jumpForce;
    }

    public float GetMoveSpeed(){
        return moveSpeed;
    }

    public void MoveLeft(){
        //Debug.Log("called move left: wantLeft? " + wantLeft);
        //move in negative x direction
        rb.velocity = new Vector2(-1 * moveSpeed, rb.velocity.y);
        //wantLeft = false;
    }


    public void MoveRight(){
        //Debug.Log("called move right: wantRight? " + wantRight);
        //move in positive x direction
        rb.velocity = new Vector2(moveSpeed, rb.velocity.y);
        //wantRight = false;
    }

    //////////////////////////////////
}

