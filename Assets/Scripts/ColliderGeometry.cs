using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public struct ColliderGeometry
{

    public Vector2 feetSize;
    public Vector2 feetOffset;
    public Vector2 bodySize;
    public Vector2 bodyOffset;
    public float headWidthMultiplier;

    public static ColliderGeometry FromColliders(Collider2D feetColl, Collider2D bodyColl,
                                                  float headWidthMultiplier = 0.75f)
    {
        return new ColliderGeometry
        {
            feetSize = feetColl.bounds.size,
            feetOffset = feetColl.bounds.center - feetColl.transform.position,
            bodySize = bodyColl.bounds.size,
            bodyOffset = bodyColl.bounds.center - bodyColl.transform.position,
            headWidthMultiplier = headWidthMultiplier
        };
    }
}