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

    public float getG(){
        return (float)Math.Round(g_n, 0);
    }

    public float getH(){
        //return h_n;
        return (float)Math.Round(h_n, 0);
    }

    public float getF(){
        return f_n;
    }

    //helper function to check if two nodes are equal
    public bool isEqual(A_StarNode n2){
        return (position == n2.position 
                && g_n == n2.g_n 
                && h_n == n2.h_n);
    }

}
