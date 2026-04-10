using System.Collections;
using System.Collections.Generic;
using UnityEngine;




[System.Serializable]
public struct PlayerState
{

    public Vector2 position;
    public float horizontalVelocity;
    public float verticalVelocity;
    public bool isFacingRight;


    public bool isGrounded;
    public bool bumpedHead;


    public bool isJumping;
    public bool isFalling;
    public bool isFastFalling;
    public float fastFallTime;
    public float fastFallReleaseSpeed;
    public int numberOfJumpsUsed;

    public bool isPastApexThreshold;
    public float timePastApexThreshold;

    public float jumpBufferTimer;
    public bool jumpReleaseDuringBuffer;

    public float coyoteTimer;


}