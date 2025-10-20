using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{   
    //public variables (change these from inspector)
    public float speed = 10f;
    public float jumpForce = 7f;
    public float gravityForce = 9.81f;

    //movement variables
    private PlayerInputActions playerInput; //use to get input
    private Vector2 move; //move direction

    //jump variables
    private Rigidbody2D rbody; //use to apply physics
    private bool isGrounded; //should be false if player is falling/jumping


    //called when player is created
    private void Awake(){
        //connect to components
        playerInput = new PlayerInputActions(); //get input
        playerInput.Player.Enable();
        rbody = GetComponent<Rigidbody2D>(); //use physics

        //set gravity on player to provided value
        float gScale = gravityForce / (9.81f);
        rbody.gravityScale = gScale; 
    }


    //call (or don't call) the onJump function given keyboard input
    private void OnEnable(){
        playerInput.Player.Jump.performed += onJump;
    }
    
    private void OnDisable(){
        playerInput.Player.Jump.performed -= onJump;
    }


    //function to perform the jump
    void onJump(InputAction.CallbackContext context){
        //only let the player jump if they're on the ground
        if(isGrounded){
            rbody.AddForce(Vector3.up * jumpForce, ForceMode2D.Impulse);
        }
    }


    //called when player contacts an object
    //use stay instead of OnCollisionEnter so it properly checks contact points
    private void OnCollisionStay2D(Collision2D collision){

        //make array to hold contact points
        ///guessed for the size, so far actual number of points is between 0 and 6
        ContactPoint2D[] contactPoints = new ContactPoint2D[10];
        int contactCount = collision.GetContacts(contactPoints);

        //check everything we're colliding with
        foreach (ContactPoint2D contact in contactPoints){
            //only affect grounded if we're on top of something
            if(contact.normal.y > 0){
                isGrounded = true;
            }
        }
        
    }
    
    
    //called when player loses contact with an object
    private void OnCollisionExit2D(Collision2D collision){

        //make array to hold current contact points
        ContactPoint2D[] contactPoints = new ContactPoint2D[10];
        int contactCount = collision.GetContacts(contactPoints);

        //make sure player can't jump if they're already in the air
        if(collision.contactCount == 0){
            isGrounded = false;
        }
    }


    // Update is called once per frame
    void Update(){
        //get the move input and use it to change player location
        move = playerInput.Player.Move.ReadValue<Vector2>();
        transform.Translate(move * (Time.deltaTime * speed));
    }
}

