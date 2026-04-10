using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour, IMovement
{

    [Header("References")]
    [SerializeField] private PlayerMovementStats _stats;
    [SerializeField] private Collider2D _feetColl;
    [SerializeField] private Collider2D _bodyColl;

    private Rigidbody2D _rb;

    private float _horizontalVelocity;
    private bool _isFacingRight;

    private RaycastHit2D _groundHit;
    private RaycastHit2D _headHit;
    private bool _isGrounded;
    private bool _bumpedHead;

    private float _verticalVelocity;
    private bool _isJumping;
    private bool _isFastFalling;
    private bool _isFalling;
    private float _fastFallTime;
    private float _fastFallReleaseSpeed;
    private int _numberOfJumpsUsed;


    private float _apexPoint;
    private float _timePastApexThreshold;
    private bool _isPastApexThreshold;

    private float _jumpBufferTimer;
    private bool _jumpReleaseDuringBuffer;

    private float _coyoteTimer;

    private readonly List<PlayerAction> _candidates = new List<PlayerAction>(16);


    //here to track the platform..
    private int _currentPlatformId = -1;
    private Vector2 _currentContactPoint;



    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; //gravity is custom
        _isFacingRight = true;

    }


    private void Update()
    {
        CountTimers();
        JumpChecks();
        LandCheck();
    }

    private void FixedUpdate()
    {
        CollisionChecks();
        ApplyJumpPhysics();
        Fall();

        if (_isGrounded)
            Move(_stats.GroundAcceleration, _stats.GroundDeceleration, InputManager.Movement);
        else
            Move(_stats.AirAcceleration, _stats.AirDeceleration, InputManager.Movement);

        ApplyVelocity();

    }


    public void MoveLeft()
    {
        _horizontalVelocity = -_stats.MaxWalkSpeed;
        _isFacingRight = false;
    }

    public void MoveRight()
    {
        _horizontalVelocity = _stats.MaxWalkSpeed;
        _isFacingRight = true;

    }

    public void RunLeft()
    {
        _horizontalVelocity = -_stats.MaxRunSpeed;
        _isFacingRight = false;
    }

    public void RunRight()
    {
        _horizontalVelocity = _stats.MaxRunSpeed;
        _isFacingRight = true;
    }

    public void Stop()
    {
        _horizontalVelocity = 0;
    }

    public void Jump()
    {
        _jumpBufferTimer = _stats.JumpBufferTime;
        _jumpReleaseDuringBuffer = false;
    }

    public void ReleaseJump()
    {
        if (_jumpBufferTimer > 0f)
            _jumpReleaseDuringBuffer = true;

        if (_isJumping && _verticalVelocity > 0f)
        {
            if (_isPastApexThreshold)
            {
                _isPastApexThreshold = false;
                _isFastFalling = true;
                _fastFallTime = _stats.TimeForUpwardsCancel;
                _verticalVelocity = 0f;
            }
            else
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = _verticalVelocity;
            }
        }
    }


    public bool IsGrounded() => _isGrounded;
    public bool IsFalling() => _isFalling;
    public bool IsJumping() => _isJumping;
    public float GetHorizontalVelocity() => _horizontalVelocity;
    public float GetVerticalVelocity() => _verticalVelocity;
    public bool IsFacingRight() => _isFacingRight;

    public void HoldJump() { /* apex hang is handled automatically inside Jump() */ }

    public float GetMaxWalkSpeed() => _stats.MaxWalkSpeed;
    public float GetMaxRunSpeed() => _stats.MaxRunSpeed;
    public float GetJumpHeight() => _stats.AdjustedJumpHeight;
    public PlayerMovementStats GetStats() => _stats;

    // public GroundInfo CheckGround(Vector2 position)
    // {
    //     Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, _stats.GroundDetectionRayLength);
    //     Vector2 boxCastOrigin = new Vector2(
    //         position.x,
    //         position.y + (_feetColl.bounds.center.y - _feetColl.transform.position.y)
    //                     - _feetColl.bounds.extents.y
    //                     + boxCastSize.y * 0.5f);

    //     RaycastHit2D hit = Physics2D.BoxCast(
    //         boxCastOrigin, boxCastSize, 0f, Vector2.down,
    //         _stats.GroundDetectionRayLength, _stats.GroundLayer);

    //     return hit.collider != null ? GroundInfo.FromHit(hit) : GroundInfo.NotGrounded;
    // }


    public PlayerState CaptureSnapshot() => new PlayerState
    {
        position = _rb.position,
        horizontalVelocity = _horizontalVelocity,
        verticalVelocity = _verticalVelocity,
        isFacingRight = _isFacingRight,
        isGrounded = _isGrounded,
        bumpedHead = _bumpedHead,
        isJumping = _isJumping,
        isFalling = _isFalling,
        isFastFalling = _isFastFalling,
        fastFallTime = _fastFallTime,
        fastFallReleaseSpeed = _fastFallReleaseSpeed,
        numberOfJumpsUsed = _numberOfJumpsUsed,
        isPastApexThreshold = _isPastApexThreshold,
        timePastApexThreshold = _timePastApexThreshold,
        jumpBufferTimer = _jumpBufferTimer,
        jumpReleaseDuringBuffer = _jumpReleaseDuringBuffer,
        coyoteTimer = _coyoteTimer,
    };

    public void RestoreSnapshot(PlayerState s)
    {
        _rb.position = s.position;
        _rb.velocity = new Vector2(s.horizontalVelocity, s.verticalVelocity);
        _horizontalVelocity = s.horizontalVelocity;
        _verticalVelocity = s.verticalVelocity;
        _isFacingRight = s.isFacingRight;
        _isGrounded = s.isGrounded;
        _bumpedHead = s.bumpedHead;
        _isJumping = s.isJumping;
        _isFalling = s.isFalling;
        _isFastFalling = s.isFastFalling;
        _fastFallTime = s.fastFallTime;
        _fastFallReleaseSpeed = s.fastFallReleaseSpeed;
        _numberOfJumpsUsed = s.numberOfJumpsUsed;
        _isPastApexThreshold = s.isPastApexThreshold;
        _timePastApexThreshold = s.timePastApexThreshold;
        _jumpBufferTimer = s.jumpBufferTimer;
        _jumpReleaseDuringBuffer = s.jumpReleaseDuringBuffer;
        _coyoteTimer = s.coyoteTimer;

        Physics2D.SyncTransforms();
    }

    public bool IsGoal(PlayerState state, GoalRegion goal)
    {
        if (goal.requireGrounded && !state.isGrounded) return false;

        bool inX = state.position.x >= goal.minX && state.position.x <= goal.maxX;
        bool inY = Mathf.Abs(state.position.y - goal.y) <= goal.yTolerance;

        return inX && inY;
    }

    public bool IsGrounded(Vector2 position)
    {
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, _stats.GroundDetectionRayLength);
        Vector2 boxCastOrigin = new Vector2(
            position.x,
            position.y + (_feetColl.bounds.center.y - _feetColl.transform.position.y)
                       - _feetColl.bounds.extents.y
                       + boxCastSize.y * 0.5f);

        return Physics2D.BoxCast(
            boxCastOrigin, boxCastSize, 0f, Vector2.down,
            _stats.GroundDetectionRayLength, _stats.GroundLayer).collider != null;
    }

    public bool IsBumpingHead(Vector2 position)
    {
        Vector2 bodyOffset = _bodyColl.bounds.center - _bodyColl.transform.position;
        Vector2 origin = new Vector2(position.x, position.y + bodyOffset.y + _bodyColl.bounds.extents.y);
        Vector2 size = new Vector2(_bodyColl.bounds.size.x * _stats.headWidth,
                                          _stats.HeadDetectionRayLength);

        return Physics2D.BoxCast(
            origin, size, 0f, Vector2.up,
            _stats.HeadDetectionRayLength, _stats.GroundLayer).collider != null;
    }

    public List<PlayerAction> GetCandidateActions(PlayerState state)
    {
        _candidates.Clear();

        _candidates.Add(PlayerAction.Idle);
        _candidates.Add(PlayerAction.Left);
        _candidates.Add(PlayerAction.Right);
        _candidates.Add(PlayerAction.RunLeft);
        _candidates.Add(PlayerAction.RunRight);

        bool canJump = state.isGrounded
                    || state.coyoteTimer > 0f
                    || (!state.isJumping && state.isFalling
                        && state.numberOfJumpsUsed < _stats.NumberOfJumpsAllowed - 1);

        bool canDoubleJump = state.isJumping
                          && state.numberOfJumpsUsed < _stats.NumberOfJumpsAllowed;

        if (canJump || canDoubleJump)
        {
            // Full jump variants (hold)
            _candidates.Add(PlayerAction.Jump(0f, false, true));
            _candidates.Add(PlayerAction.Jump(-1f, false, true));
            _candidates.Add(PlayerAction.Jump(1f, false, true));
            _candidates.Add(PlayerAction.Jump(-1f, true, true));
            _candidates.Add(PlayerAction.Jump(1f, true, true));

            // Short hop variants (immediate release)
            _candidates.Add(PlayerAction.Jump(0f, false, false));
            _candidates.Add(PlayerAction.Jump(-1f, false, false));
            _candidates.Add(PlayerAction.Jump(1f, false, false));
        }

        if (state.isJumping && state.verticalVelocity > 0f && !state.isFastFalling)
        {
            _candidates.Add(PlayerAction.ReleaseJump(-1f));
            _candidates.Add(PlayerAction.ReleaseJump(0f));
            _candidates.Add(PlayerAction.ReleaseJump(1f));
        }

        // Future mechanics:
        // if (canDash) _candidates.Add(new PlayerAction { dashPressed = true });

        return _candidates;
    }


    public PlayerState SimulateStep(PlayerState s, PlayerAction action, float dt)
    {
        PlayerState n = s;

        // Collision
        n.isGrounded = IsGrounded(n.position);
        n.bumpedHead = IsBumpingHead(n.position);

        // Timers
        n.jumpBufferTimer -= dt;
        if (!n.isGrounded)
            n.coyoteTimer -= dt;
        else
            n.coyoteTimer = _stats.JumpCoyoteTime;

        // Jump checks
        if (action.jumpPressed)
        {
            n.jumpBufferTimer = _stats.JumpBufferTime;
            n.jumpReleaseDuringBuffer = false;
        }

        if (action.jumpReleased)
        {
            if (n.jumpBufferTimer > 0f)
                n.jumpReleaseDuringBuffer = true;

            if (n.isJumping && n.verticalVelocity > 0f)
            {
                if (n.isPastApexThreshold)
                {
                    n.isPastApexThreshold = false;
                    n.isFastFalling = true;
                    n.fastFallTime = _stats.TimeForUpwardsCancel;
                    n.verticalVelocity = 0f;
                }
                else
                {
                    n.isFastFalling = true;
                    n.fastFallReleaseSpeed = n.verticalVelocity;
                }
            }
        }

        // Initiate jump — grounded or coyote
        if (n.jumpBufferTimer > 0f && !n.isJumping && (n.isGrounded || n.coyoteTimer > 0f))
        {
            n = InitiateJump(n, 1);
            if (n.jumpReleaseDuringBuffer)
            {
                n.isFastFalling = true;
                n.fastFallReleaseSpeed = n.verticalVelocity;
            }
        }
        // Double jump
        else if (n.jumpBufferTimer > 0f && n.isJumping
                 && n.numberOfJumpsUsed < _stats.NumberOfJumpsAllowed)
        {
            n.isFastFalling = false;
            n = InitiateJump(n, 1);
        }
        // Air jump after coyote lapsed
        else if (n.jumpBufferTimer > 0f && n.isFalling
                 && n.numberOfJumpsUsed < _stats.NumberOfJumpsAllowed - 1)
        {
            n = InitiateJump(n, 2);
            n.isFalling = false;
        }

        // Land check
        if ((n.isJumping || n.isFalling) && n.isGrounded && n.verticalVelocity <= 0f)
        {
            n.isJumping = false;
            n.isFalling = false;
            n.isFastFalling = false;
            n.fastFallTime = 0f;
            n.isPastApexThreshold = false;
            n.numberOfJumpsUsed = 0;
            n.verticalVelocity = 0f;
        }

        // Fall
        if (!n.isGrounded && !n.isJumping)
        {
            if (!n.isFalling) n.isFalling = true;
            n.verticalVelocity += _stats.Gravity * dt;
        }

        // Jump physics
        if (n.isJumping)
        {
            if (n.bumpedHead) n.isFastFalling = true;

            if (n.verticalVelocity >= 0f)
            {
                float apexPoint = Mathf.InverseLerp(_stats.InitialJumpVelocity, 0f, n.verticalVelocity);

                if (apexPoint > _stats.ApexTheshold)
                {
                    if (!n.isPastApexThreshold)
                    {
                        n.isPastApexThreshold = true;
                        n.timePastApexThreshold = 0f;
                    }

                    n.timePastApexThreshold += dt;

                    if (n.timePastApexThreshold < _stats.ApexHangTime)
                        n.verticalVelocity = 0f;
                    else
                        n.verticalVelocity = -0.01f;
                }
                else if (!n.isFastFalling)
                {
                    n.verticalVelocity += _stats.Gravity * dt;
                    if (n.isPastApexThreshold)
                        n.isPastApexThreshold = false;
                }
            }
            else if (!n.isFastFalling)
            {
                n.verticalVelocity += _stats.Gravity * _stats.GravityOnReleaseMultiplier * dt;
            }
            else if (n.verticalVelocity < 0f)
            {
                if (!n.isFalling) n.isFalling = true;
            }
        }

        // Fast fall / jump cut
        if (n.isFastFalling)
        {
            if (n.fastFallTime >= _stats.TimeForUpwardsCancel)
                n.verticalVelocity += _stats.Gravity * _stats.GravityOnReleaseMultiplier * dt;
            else
                n.verticalVelocity = Mathf.Lerp(n.fastFallReleaseSpeed, 0f,
                                                  n.fastFallTime / _stats.TimeForUpwardsCancel);

            n.fastFallTime += dt;
        }

        // Clamp vertical
        n.verticalVelocity = Mathf.Clamp(n.verticalVelocity, -_stats.MaxFallSpeed, 50f);

        // Horizontal
        float accel = n.isGrounded ? _stats.GroundAcceleration : _stats.AirAcceleration;
        float decel = n.isGrounded ? _stats.GroundDeceleration : _stats.AirDeceleration;

        if (Mathf.Abs(action.moveX) >= _stats.MoveTrheshold)
        {
            if (action.moveX < 0f) n.isFacingRight = false;
            else if (action.moveX > 0f) n.isFacingRight = true;

            float target = action.moveX *
                (action.runHeld ? _stats.MaxRunSpeed : _stats.MaxWalkSpeed);

            n.horizontalVelocity = Mathf.Lerp(n.horizontalVelocity, target, accel * dt);
        }
        else
        {
            n.horizontalVelocity = Mathf.Lerp(n.horizontalVelocity, 0f, decel * dt);
        }

        // Integrate position
        n.position += new Vector2(n.horizontalVelocity, n.verticalVelocity) * dt;

        return n;
    }

    private void ApplyVelocity()
    {
        _verticalVelocity = Mathf.Clamp(_verticalVelocity, -_stats.MaxFallSpeed, 50f);
        _rb.velocity = new Vector2(_horizontalVelocity, _verticalVelocity);
    }

    private void Move(float acceleration, float deceleration, Vector2 moveInput)
    {
        if (Mathf.Abs(moveInput.x) >= _stats.MoveTrheshold)
        {
            TurnCheck(moveInput);

            float target = moveInput.x *
                (InputManager.RunIsHeld ? _stats.MaxRunSpeed : _stats.MaxWalkSpeed);

            _horizontalVelocity = Mathf.Lerp(_horizontalVelocity, target, acceleration * Time.fixedDeltaTime);
        }
        else
        {
            _horizontalVelocity = Mathf.Lerp(_horizontalVelocity, 0f, deceleration * Time.fixedDeltaTime);
        }
    }

    private void TurnCheck(Vector2 moveInput)
    {
        if (_isFacingRight && moveInput.x < 0) Turn(false);
        else if (!_isFacingRight && moveInput.x > 0) Turn(true);
    }

    private void Turn(bool turnRight)
    {
        _isFacingRight = turnRight;
        transform.Rotate(0f, turnRight ? 180f : -180f, 0f);
    }

    private void LandCheck()
    {
        if ((_isJumping || _isFalling) && _isGrounded && _verticalVelocity <= 0f)
        {
            _isJumping = false;
            _isFalling = false;
            _isFastFalling = false;
            _fastFallTime = 0f;
            _isPastApexThreshold = false;
            _numberOfJumpsUsed = 0;
            _verticalVelocity = 0f;
        }
    }

    private void Fall()
    {
        if (!_isGrounded && !_isJumping)
        {
            if (!_isFalling) _isFalling = true;
            _verticalVelocity += _stats.Gravity * Time.fixedDeltaTime;
        }
    }

    private void JumpChecks()
    {
        if (InputManager.JumpWasPressed)
        {
            _jumpBufferTimer = _stats.JumpBufferTime;
            _jumpReleaseDuringBuffer = false;
        }

        if (InputManager.JumpWasRelesed)
        {
            if (_jumpBufferTimer > 0f)
                _jumpReleaseDuringBuffer = true;

            if (_isJumping && _verticalVelocity > 0f)
            {
                if (_isPastApexThreshold)
                {
                    _isPastApexThreshold = false;
                    _isFastFalling = true;
                    _fastFallTime = _stats.TimeForUpwardsCancel;
                    _verticalVelocity = 0f;
                }
                else
                {
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = _verticalVelocity;
                }
            }
        }

        // Grounded or coyote jump
        if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f))
        {
            RuntimeInitiateJump(1);
            if (_jumpReleaseDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = _verticalVelocity;
            }
        }
        // Double jump
        else if (_jumpBufferTimer > 0f && _isJumping
                 && _numberOfJumpsUsed < _stats.NumberOfJumpsAllowed)
        {
            _isFastFalling = false;
            RuntimeInitiateJump(1);
        }
        // Air jump after coyote lapsed
        else if (_jumpBufferTimer > 0f && _isFalling
                 && _numberOfJumpsUsed < _stats.NumberOfJumpsAllowed - 1)
        {
            RuntimeInitiateJump(2);
            _isFalling = false;
        }
    }

    private void RuntimeInitiateJump(int jumpsToAdd)
    {
        if (!_isJumping) _isJumping = true;
        _jumpBufferTimer = 0f;
        _numberOfJumpsUsed += jumpsToAdd;
        _verticalVelocity = _stats.InitialJumpVelocity;
    }

    private void ApplyJumpPhysics()
    {
        if (_isJumping)
        {
            if (_bumpedHead) _isFastFalling = true;

            if (_verticalVelocity >= 0f)
            {
                _apexPoint = Mathf.InverseLerp(_stats.InitialJumpVelocity, 0f, _verticalVelocity);

                if (_apexPoint > _stats.ApexTheshold)
                {
                    if (!_isPastApexThreshold)
                    {
                        _isPastApexThreshold = true;
                        _timePastApexThreshold = 0f;
                    }

                    _timePastApexThreshold += Time.fixedDeltaTime;

                    if (_timePastApexThreshold < _stats.ApexHangTime)
                        _verticalVelocity = 0f;
                    else
                        _verticalVelocity = -0.01f;
                }
                else if (!_isFastFalling)
                {
                    _verticalVelocity += _stats.Gravity * Time.fixedDeltaTime;
                    if (_isPastApexThreshold) _isPastApexThreshold = false;
                }
            }
            else if (!_isFastFalling)
            {
                _verticalVelocity += _stats.Gravity * _stats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_verticalVelocity < 0f)
            {
                if (!_isFalling) _isFalling = true;
            }
        }

        if (_isFastFalling)
        {
            if (_fastFallTime >= _stats.TimeForUpwardsCancel)
                _verticalVelocity += _stats.Gravity * _stats.GravityOnReleaseMultiplier * Time.fixedDeltaTime;
            else
                _verticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f,
                                                 _fastFallTime / _stats.TimeForUpwardsCancel);

            _fastFallTime += Time.fixedDeltaTime;
        }

        _verticalVelocity = Mathf.Clamp(_verticalVelocity, -_stats.MaxFallSpeed, 50f);
    }

    private void CountTimers()
    {
        _jumpBufferTimer -= Time.deltaTime;

        if (!_isGrounded)
            _coyoteTimer -= Time.deltaTime;
        else
            _coyoteTimer = _stats.JumpCoyoteTime;
    }

    private void CollisionChecks()
    {
        CheckIsGrounded();
        CheckBumpedHead();
    }

    private void CheckIsGrounded()
    {
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, _stats.GroundDetectionRayLength);
        Vector2 boxCastOrigin = new Vector2(
            _feetColl.bounds.center.x,
            _feetColl.bounds.min.y + boxCastSize.y * 0.5f);

        _groundHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.down,
                                         _stats.GroundDetectionRayLength, _stats.GroundLayer);
        _isGrounded = _groundHit.collider != null;

        if (_stats.DebugShowIsGroundedBox)
        {
            Color c = _isGrounded ? Color.green : Color.red;
            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2, boxCastOrigin.y),
                          Vector2.down * _stats.GroundDetectionRayLength, c);
            Debug.DrawRay(new Vector2(boxCastOrigin.x + boxCastSize.x / 2, boxCastOrigin.y),
                          Vector2.down * _stats.GroundDetectionRayLength, c);
            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2,
                                       boxCastOrigin.y - _stats.GroundDetectionRayLength),
                          Vector2.right * boxCastSize.x, c);
        }
    }

    private void CheckBumpedHead()
    {
        Vector2 origin = new Vector2(_bodyColl.bounds.center.x, _bodyColl.bounds.max.y);
        Vector2 size = new Vector2(_bodyColl.bounds.size.x * _stats.headWidth,
                                      _stats.HeadDetectionRayLength);

        _headHit = Physics2D.BoxCast(origin, size, 0f, Vector2.up,
                                        _stats.HeadDetectionRayLength, _stats.GroundLayer);
        _bumpedHead = _headHit.collider != null;
    }


    private PlayerState InitiateJump(PlayerState s, int jumpsToAdd)
    {
        if (!s.isJumping) s.isJumping = true;
        s.jumpBufferTimer = 0f;
        s.numberOfJumpsUsed += jumpsToAdd;
        s.verticalVelocity = _stats.InitialJumpVelocity;
        return s;
    }


    private void OnDrawGizmosSelected()
    {
        if (_feetColl == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_feetColl.bounds.center, _feetColl.bounds.size);
    }






}
