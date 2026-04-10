using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// to add a new mechanic
/// add input fields to PlayerAction
/// add state fields to PlayerState
/// add the commands here
/// need to implement the physics in PlayerMovement
/// expose it in GetCandidateActions
/// 
/// 
/// </summary>

public interface IMovement
{


    void MoveLeft();
    void MoveRight();
    void RunLeft();
    void RunRight();
    void Stop();


    void Jump();
    void HoldJump();
    void ReleaseJump();

    // Future mechanics??



    bool IsGrounded();
    bool IsFalling();
    bool IsJumping();
    float GetHorizontalVelocity();
    float GetVerticalVelocity();
    bool IsFacingRight();


    float GetMaxWalkSpeed();
    float GetMaxRunSpeed();
    float GetJumpHeight();
    PlayerMovementStats GetStats();


    PlayerState SimulateStep(PlayerState current, PlayerAction action, float dt);


    List<PlayerAction> GetCandidateActions(PlayerState state);


    PlayerState CaptureSnapshot();


    void RestoreSnapshot(PlayerState state);


    bool IsGoal(PlayerState state, GoalRegion goal);
}