using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct GoalRegion
{

    public float minX;
    public float maxX;
    public float y;
    public float yTolerance;
    public bool requireGrounded;


    public static GoalRegion FromPlatform(float leftX, float rightX, float surfaceY,
                                           float yTolerance = 0.5f)
        => new GoalRegion
        {
            minX = leftX,
            maxX = rightX,
            y = surfaceY,
            yTolerance = yTolerance,
            requireGrounded = true
        };


    public static GoalRegion FromPoint(Vector2 point, float radius = 0.35f)
        => new GoalRegion
        {
            minX = point.x - radius,
            maxX = point.x + radius,
            y = point.y,
            yTolerance = radius,
            requireGrounded = false
        };
}