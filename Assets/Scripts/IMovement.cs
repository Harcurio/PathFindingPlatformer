using System.Collections;
using System.Collections.Generic;
using UnityEngine;

interface IMovement 
{   
    //player must implement these functions
    void Jump();
    void MoveLeft();
    void MoveRight();
    bool IsGrounded();

    float GetJumpForce();
    float GetMoveSpeed();
}
