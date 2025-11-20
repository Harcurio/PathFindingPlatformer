using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class A_StarNode : ScriptableObject{   
    
    private Vector2 position;
    private float g_n;
    private float h_n;
    private float f_n;

    //set up values
    public void nodeSetup(Vector2 pos, float g, float h){
        position = pos;
        g_n = g;
        h_n = h;
        f_n = (float)Math.Round(g + h, 0);
    }

    //getters
    public Vector2 getPosition(){
        return position;
    }

    public float getF(){
        return f_n;
    }

}
