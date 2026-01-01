
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;


public class PlayerMovement : MonoBehaviour, IMovement
{


    public enum MoveActionType { None, Left, Right, Jump } //here need to add also the new mechanic....
    private readonly List<MoveAction> candidateActions = new List<MoveAction>(8);

    public void SetUseUnityInput(bool enabled) => useUnityInput = enabled;


    public struct MoveAction
    {
        public MoveActionType type;
        public MoveAction(MoveActionType type) => this.type = type;

    }

    [System.Serializable]
    public struct MovementSnapshot
    {
        public Vector2 position;
        public Vector2 velocity;

        public float moveInput;
        public bool wantJump;
        //public bool wantDash; // example again here 

        public bool isGrounded;
        public int facing;
        //public float dashCooldownRemaining;

    }


    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    // [Header("Dash Settings")]                  //example of the dash 
    // public float dashSpeed = 12f;
    // public float dashCooldown = 0.5f;
    // public bool allowAirDash = true;

    [Header("Ground Check (Raycast)")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer;
    public Vector2 rayOffset = new Vector2(0f, 0f);

    [Header("Control")]
    [Tooltip("If true, reads input in Update(). If false external code must call setInput().")]
    [SerializeField] bool useUnityInput = true;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool wantJump;
    private float moveInput;
    //private bool wantDash;

    private int facing = 1; //facing right
    //private float dashCooldownRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("PlayerMovement requires a Rb2d");
    }


    void Update()
    {
        if (!useUnityInput) return;


        float horizontal = Input.GetAxisRaw("Horizontal");
        bool jumpDown = Input.GetKeyDown(KeyCode.Space);

        //bool dashDown = Input.GetKeyDown(KeyCode.LeftShift);

        SetInput(horizontal, jumpDown);//here the other mechanics need to be implemented too...

    }


    void FixedUpdate()
    {
        SimulateStep(Time.fixedDeltaTime);
    }

    ///<summary
    /// Single source of truth for movement.
    /// planer will call this on a simulation
    ///</summary>

    public void SimulateStep(float dt)
    {

        isGrounded = IsGrounded();

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);


        if (wantJump)
        {
            Jump();
            wantJump = false;
        }

        // if (wanDash)
        // {
        //     Dash();
        //     wantDash = false;
        // }

        // if (dashCooldownRemaining > 0f)
        //     dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - dt);

    }


    public void SetInput(float horizontal, bool jumpDown) //here dash will need to be add the dashDown bool...
    {

        moveInput = Mathf.Clamp(horizontal, -1f, 1f);

        if (moveInput < 0f) facing = -1;
        else if (moveInput > 0f) facing = 1;

        if (jumpDown && IsGrounded())
            wantJump = true;

        // if (dashDown)
        //     wantDash = true;

    }


    public void ApplyAction(MoveAction action)
    {
        switch (action.type)
        {
            case MoveActionType.None:
                SetInput(0f, false);  //need update this for each new mechanic...
                break;
            case MoveActionType.Left:
                MoveLeft();
                break;
            case MoveActionType.Right:
                MoveRight();
                break;
            case MoveActionType.Jump:
                SetInput(moveInput, true); // need to be updated for each new mechanic too 
                break;
                //here need to be added the new mechanics.

        }

    }


    public virtual IReadOnlyList<MoveAction> GetCandidateActions()
    {
        candidateActions.Clear();

        candidateActions.Add(new MoveAction(MoveActionType.None));
        candidateActions.Add(new MoveAction(MoveActionType.Left));
        candidateActions.Add(new MoveAction(MoveActionType.Right));

        bool groundedNow = IsGrounded();
        if (groundedNow)
            candidateActions.Add(new MoveAction(MoveActionType.Jump));

        // bool canDash = dashCooldownRemaining <= 0f && (groundedNow || allowAirDash);
        // if (canDash)
        //     candidateActions.Add(new MoveAction(MoveActionType.Dash));

        return candidateActions;

    }



    public MovementSnapshot CaptureSnapshot()
    {
        return new MovementSnapshot
        {
            position = transform.position,
            velocity = rb.velocity,
            moveInput = moveInput,
            wantJump = wantJump,
            //wantDash = wantDash,
            isGrounded = isGrounded,
            facing = facing,
            //dashCooldownRemaining = dashCooldownRemaining
        };
    }

    public void RestoreSnapshot(in MovementSnapshot s)
    {
        transform.position = s.position;
        rb.velocity = s.velocity;

        moveInput = s.moveInput;
        wantJump = s.wantJump;
        //wantDash = s.wantDash;

        isGrounded = s.isGrounded;
        facing = (s.facing == 0) ? 1 : s.facing;
        //dashCooldownRemaining = s.dashCooldownRemaining;
    }

    // ---------------- IMovement (runtime “capability” methods) ----------------
    public void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    public void MoveLeft()
    {
        moveInput = -1f;
        facing = -1;
    }

    public void MoveRight()
    {
        moveInput = 1f;
        facing = 1;
    }

    // public void Dash()
    // {
    //     if (dashCooldownRemaining > 0f) return;

    //     bool groundedNow = IsGrounded();
    //     if (!groundedNow && !allowAirDash) return;

    //     rb.velocity = new Vector2(facing * dashSpeed, rb.velocity.y);
    //     dashCooldownRemaining = dashCooldown;
    // }

    public bool IsGrounded()
    {
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = (hit.collider != null);
        return isGrounded;
    }

    public float GetJumpForce() => jumpForce;
    public float GetMoveSpeed() => moveSpeed;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 origin = (Vector2)transform.position + rayOffset;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }






}

