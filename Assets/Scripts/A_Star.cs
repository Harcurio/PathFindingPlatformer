using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class A_Star : MonoBehaviour{
    
    //public variables
    [Header ("Start and Goal Positions")]
    public Vector2 startPosition;
    public Vector2 goalPosition;

    [Header("Obstacle Check (Raycast)")]
    public LayerMask obstacleLayer;

    [Header("Object to Move")]
    public GameObject playerObject;

    ///TODO: also get movement functions

    //private variables
    A_StarNode currentNode; //record node that A* is currently at
    int updateNumber = 0; //number of updates we've done (use for g)?? ///

    private float moveSpeed = 1f;
    private float jumpForce = 1f;
    private float gravityForce = 0.5f;
    private float halfWidth = 0.5f;
    private float halfHeight = 0.5f; ///TODO: get all these from player object??

    //raycasting variables
    private float checkDistanceJump ; ///TODO: should we make these public and let user set these??
    private float checkDistanceFall;
    private float checkDistanceX;

    private Vector2 rayOffset = new Vector2(0f, 0f);
    
    //arraylist for unexplored nodes and nodes added to the path
    private ArrayList nodeList = new ArrayList(); ///TODO: more efficient data structure?
    private ArrayList pathList = new ArrayList();


    // Start is called before the first frame update
    void Start(){
        //make sure user gave A* an object that has the movement functions
        if(playerObject == null){
            Debug.Log("Remember to give the A_star script a player object");
        }

        //add start node to final path list
        A_StarNode startNode = ScriptableObject.CreateInstance<A_StarNode>();
        startNode.nodeSetup(startPosition, 0f, Vector2.Distance(startPosition, goalPosition));
        pathList.Add(startNode);

        //A* starts at provided start node
        transform.position = startPosition;
        currentNode = startNode;

        //set up distances to check
        checkDistanceJump = halfHeight + jumpForce; ///
        checkDistanceFall = halfHeight + gravityForce; ///
        checkDistanceX = halfWidth + moveSpeed; ///
    }

    //helper function to print the current node list
    void printNodeList(){
        string list = "";
        for (int i = 0; i < nodeList.Count; ++i){
            var nextPos = ((A_StarNode)nodeList[i]).getPosition();
            var nextF = ((A_StarNode)nodeList[i]).getF();
            list += ("[" + nextPos + ", " + nextF + "], ");
        }
        Debug.Log("CURRENT NODE LIST: " + list);
    }


    // Update is called once per frame
    void Update(){
        //increment update number
        ++updateNumber; ///TODO: decide if we want this for g(n)

        //do nothing this frame if we reached the goal
        if( Vector2.Distance(currentNode.getPosition(), goalPosition) <= moveSpeed){
            Debug.Log("reached goal!");

            int pathSize = pathList.Count;
            
            /*///
            for (int g = 0; g < pathSize - 1; ++g){
                Vector2 current = ( (A_StarNode)pathList[g] ).getPosition();
                Vector2 next = ( (A_StarNode)pathList[g + 1] ).getPosition();
                Debug.DrawLine(current, next);
            }
            *////
            return;
        }
        
        ///TODO: decide on failure condition and choose a response

        ///Debug.Log("STARTING UPDATE " + updateNumber);
        ///printNodeList();
        

        ///TODO: expand to diagonal directions while in the air?
        ///TODO: next position for the new node determined by provided jumpforce and move speed
        ///     or by jump and move functions??
        
        //raycast in each neighbour direction to see if there's an obstacle
        //RAYCAST LEFT
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hitLeft = Physics2D.Raycast(origin, Vector2.left, checkDistanceX, obstacleLayer);
        //valid direction when there's no obstacle
        //if(hitLeft.collider == null){
        if(!hitLeft.collider){
            //calculate position and values
            Vector2 newPosLeft = (Vector2)transform.position + (Vector2.left * moveSpeed);
            A_StarNode newNode = createNewNode(newPosLeft, currentNode);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }

        //RAYCAST RIGHT
        RaycastHit2D hitRight = Physics2D.Raycast(origin, Vector2.right, checkDistanceX, obstacleLayer);
        //valid direction when there's no obstacle
        if(hitRight.collider == null){
        ///if(!hitRight.collider){
            //calculate position and values
            Vector2 newPosRight = (Vector2)transform.position + (Vector2.right * moveSpeed);
            A_StarNode newNode = createNewNode(newPosRight, currentNode);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }

        //RAYCAST UP AND DOWN 
        RaycastHit2D hitUp = Physics2D.Raycast(origin, Vector2.up, checkDistanceJump, obstacleLayer);
        RaycastHit2D hitDown = Physics2D.Raycast(origin, Vector2.down, checkDistanceFall, obstacleLayer);
        //valid direction when there's obstacle below and no obstacle above
        ///if(hitUp.collider == null && hitDown.collider != null){
        if(!hitUp.collider && hitDown.collider){
            //calculate position and values
            Vector2 newPosJump = (Vector2)transform.position + (Vector2.up * jumpForce);
            A_StarNode newNode = createNewNode(newPosJump, currentNode);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }
        
        ///if(hitDown.collider == null){
        if(!hitDown.collider){
            //player can fall
            //calculate position and values
            Vector2 newPosFall = (Vector2)transform.position + (Vector2.down * gravityForce);
            A_StarNode newNode = createNewNode(newPosFall, currentNode);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
        }
        ///Debug.Log("finished adding new nodes: ");
        ///Debug.Log("added nodes in directions: left = " + !hitLeft.collider + ", right = " + (hitRight.collider==null) + ", jump = " + (hitDown.collider && !hitUp.collider) + ", fall = " + !hitDown.collider);
        ///printNodeList();
        
        //set position to node with smallest f(n) from the node list
        currentNode = (A_StarNode)nodeList[0];
        //nextPos = currentNode.getPosition();
        transform.position = currentNode.getPosition();

        //update the arrays 
        nodeList.RemoveAt(0);
        pathList.Add(currentNode);

        ///Debug.Log("removed node with pos = " + currentNode.getPosition() + ", F = " + currentNode.getF());
        ///Debug.Log("current position is " + transform.position);
        ///Debug.Log("FINISHED UPDATE");
    }

    
    //helper function to set up node
    A_StarNode createNewNode(Vector2 newPos, A_StarNode currentNode){
        //float newG = updateNumber;
        float newG = currentNode.getG() + Vector2.Distance(newPos, currentNode.getPosition());
        float newH = Vector2.Distance(newPos, goalPosition);
        //create a new node
        A_StarNode newNode = ScriptableObject.CreateInstance<A_StarNode>();
        newNode.nodeSetup(newPos, newG, newH);

        return newNode;
    }

    ///TODO: find another way to calculate h(n) and/or g(n) that has fewer ties??
    
    //helper function to draw path
    void OnDrawGizmosSelected(){
        Gizmos.color = Color.red;
        int pathSize = pathList.Count;
        
        for (int g = 0; g < pathSize - 1; ++g){
            Vector2 origin = ( (A_StarNode)pathList[g] ).getPosition();
            Vector2 next = ( (A_StarNode)pathList[g + 1] ).getPosition();
            Gizmos.DrawLine(origin, next);
        }

        Gizmos.color = Color.yellow;
        Vector2 rayOrigin = (Vector2)transform.position + rayOffset;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * checkDistanceFall);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.up * checkDistanceJump);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.left * checkDistanceX);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.right * checkDistanceX);
    }

    
    //helper function to add node to list
    void addNodeToList(A_StarNode newNode){
        //set up helper variables
        int len = nodeList.Count;
        int sortedIndex = -1;
        A_StarNode storedNode1;
        A_StarNode storedNode2;

        //if node is already in path, don't add to open nodes list
        foreach ( A_StarNode pathNode in pathList){
            if (pathNode.getPosition() == newNode.getPosition()){
                return;
            }
        }

        /*///
        float newNodeF = newNode.getF();
        Vector2 newNodePos = newNode.getPosition();
        Debug.Log("STARTING ADD NODE: new node has pos = " + newNodePos + ", F = " + newNodeF);
        *////

        //if there's zero nodes in the list, add this one and return
        if(len == 0){
            nodeList.Add(newNode);
            ///Debug.Log("default added node: pos = " + newNodePos + ", F = " + newNodeF);
            return;
        }

        //otherwise move through the list starting at the beginning
        float newNodeF = newNode.getF();
        float newNodeG = newNode.getG();
        Vector2 newNodePos = newNode.getPosition();

        //start at beginning of list
        for (int i = 0; i < len; ++i){
            //compare F values to find the sorted index
            A_StarNode openNode = (A_StarNode)nodeList[i];
            float openNodeF = openNode.getF();
            float openNodeG = openNode.getG();
            Vector2 openNodePos = openNode.getPosition();

            //before the sorted point: F is smaller than new node's F
            if (openNodeF < newNodeF){
                ///Debug.Log("before sorting point: currentF = " + currentNodeF + ", newF = " + newNodeF);
                
                if(openNodePos == newNodePos){
                    //if this position is here already, don't place this node
                    ///Debug.Log("node already in list so didn't place it: pos = " + newNodePos + ", F = " + newNodeF);
                    return;
                }
            }
            //at the sorting point: tie breaking
            else if (openNodeF == newNodeF){

                //if this position is here already, don't place this node
                if(openNodePos == newNodePos){
                    ///Debug.Log("node already in list so didn't place it: pos = " + newNodePos + ", F = " + newNodeF);
                    return;
                }
                //the node with larger g(n) value goes first
                if(newNodeG >= openNodeG){
                    //if new node should go first, this is the sorted index
                    if(sortedIndex < 0){
                        //record index to place this node
                        sortedIndex = i;
                        ///Debug.Log("sorted index = " + sortedIndex);
                    }
                }
                //if new node should go next, keep checking (could have other ties)
            }
            //found the sorted point: F is greater than new node's F
            else if (openNodeF > newNodeF){
                ///Debug.Log("at/after sorting point: currentF = " + currentNodeF + ", newF = " + newNodeF);
                
                if(sortedIndex < 0){
                    //record index to place this node
                    sortedIndex = i;
                    ///Debug.Log("sorted index = " + sortedIndex);
                }

                //continue scanning forward to see if there's a position we must replace
                if (openNodePos == newNodePos){
                    nodeList[i] = newNode;
                    ///Debug.Log("replaced bigger F: placed node with pos = " + newNodePos + ", F = " + newNodeF);
                    return;
                }
            }
        }//end for: finished scan through all nodes
        
        //if we didn't update sortedIndex, new node should go at the end
        if(sortedIndex < 0){
            sortedIndex = len - 1;
        }
        ///Debug.Log("didn't replace anything");

        //if we get here, this pos wasn't already in the list
        //need to place it at sorted index and copy everything else down
        //increase array size by 1 and update array length
        nodeList.Add(newNode);
        len = nodeList.Count;
        ///Debug.Log("nodeList has length: " + len + ", sortedIndex = " + sortedIndex + ", sortedIndex + 1 = " + (sortedIndex + 1));
        
        //record the nodes to be copied up
        storedNode1 = (A_StarNode)nodeList[sortedIndex];
        storedNode2 = (A_StarNode)nodeList[sortedIndex + 1];

        //insert the new node
        nodeList[sortedIndex] = newNode;

        ///Debug.Log("Starting Copy Loop:");
        //copy back the elements past it
        for (int j = sortedIndex + 1; j < len; ++j){
            ///Debug.Log("storedNode1: pos = " + storedNode1.getPosition() + ", F = " + storedNode1.getF() + "; storedNode2: pos = " + storedNode2.getPosition() + ", F = " + storedNode2.getF());
            nodeList[j] = storedNode1;
            if(j < len - 1){
                storedNode1 = storedNode2;
                storedNode2 = (A_StarNode)nodeList[j + 1];
            }
        }
        ///Debug.Log("inserted node with pos = " + newNodePos + ", F = " + newNodeF);
    }


    ///TODO: does anything go in FixedUpdate?
    void FixedUpdate(){

    }

}
