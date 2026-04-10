using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct PlayerAction
{
    public float moveX;
    public bool runHeld;
    public bool jumpPressed;
    public bool jumpHeld;
    public bool jumpReleased;

    //Future mechanics 
    // Dash example (uncomment when implementing):
    // public bool    dashPressed;
    // public Vector2 dashDirection;


    public static PlayerAction Idle => new PlayerAction { moveX = 0f };


    public static PlayerAction Left => new PlayerAction { moveX = -1f };


    public static PlayerAction Right => new PlayerAction { moveX = 1f };


    public static PlayerAction RunLeft => new PlayerAction { moveX = -1f, runHeld = true };


    public static PlayerAction RunRight => new PlayerAction { moveX = 1f, runHeld = true };


    public static PlayerAction Jump(float moveX = 0f, bool runHeld = false, bool held = true)
        => new PlayerAction { moveX = moveX, runHeld = runHeld, jumpPressed = true, jumpHeld = held };


    public static PlayerAction ReleaseJump(float moveX = 0f)
        => new PlayerAction { moveX = moveX, jumpReleased = true };
}