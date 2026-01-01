using System.Collections;
using System.Collections.Generic;
using UnityEngine;

interface IMovement
{
    //player must implement these functions
    void Jump();
    void MoveLeft();
    void MoveRight();

    //her need to explain how to add new mechanics to developers... maybe with an example commented will be a good idea...
    //void Dash();


    bool IsGrounded();

    float GetJumpForce();
    float GetMoveSpeed();
}
