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
    private ArrayList closedList = new ArrayList(); ///TODO: show final (fastest) path or total path taken by A*?


    // Start is called before the first frame update
    void Start(){
        //make sure user gave A* an object that has the movement functions
        if(playerObject == null){
            Debug.Log("Remember to give the A_star script a player object");
        }

        //add start node to final path list
        A_StarNode startNode = ScriptableObject.CreateInstance<A_StarNode>();
        startNode.nodeSetup(startPosition, 0f, Vector2.Distance(startPosition, goalPosition));
        closedList.Add(startNode);

        //A* starts at provided start node
        transform.position = startPosition;
        currentNode = startNode;

        //set up distances to check
        checkDistanceJump = halfHeight + jumpForce; ///
        checkDistanceFall = halfHeight + gravityForce; ///
        checkDistanceX = halfWidth + moveSpeed; ///
    }

    //helper function to print arraylists
    void printNodeList(){
        string list = "";
        for (int i = 0; i < nodeList.Count; ++i){
            var nextPos = ((A_StarNode)nodeList[i]).getPosition();
            var nextF = ((A_StarNode)nodeList[i]).getF();
            list += ("[" + nextPos + ", " + nextF + "], ");
        }
        Debug.Log("CURRENT NODE LIST: " + list);
    }

    void printClosedList(){
        string list = "";
        for (int i = 0; i < closedList.Count; ++i){
            var nextPos = ((A_StarNode)closedList[i]).getPosition();
            var nextF = ((A_StarNode)closedList[i]).getF();
            list += ("[" + nextPos + ", " + nextF + "], ");
        }
        Debug.Log("CURRENT CLOSED LIST: " + list);
    }

    /*///
    void printList(ArrayList printList){
        string list = "";
        for (int i = 0; i < printList.Count; ++i){
            var nextPos = ((A_StarNode)printList[i]).getPosition();
            var nextF = ((A_StarNode)printList[i]).getF();
            list += ("[" + nextPos + ", " + nextF + "], ");
        }
        Debug.Log(list);
    }
    *////

    // Update is called once per frame
    void Update(){
        //increment update number
        ++updateNumber; ///TODO: decide if we want this for g(n)

        //do nothing this frame if we reached the goal
        if( Vector2.Distance(currentNode.getPosition(), goalPosition) <= moveSpeed){
            Debug.Log("reached goal!");
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
        if(!hitLeft.collider){
            //calculate position and values
            Vector2 newPosLeft = (Vector2)transform.position + (Vector2.left * moveSpeed);
            A_StarNode newNode = createNewNode(newPosLeft, currentNode);
            //add node to open list and update current smallest node
            if(nodeNotClosed(newNode)){
                currentNode = addNodeAndGetSmallest(newNode);
            }
            ///printNodeList();
        }

        //RAYCAST RIGHT
        RaycastHit2D hitRight = Physics2D.Raycast(origin, Vector2.right, checkDistanceX, obstacleLayer);
        //valid direction when there's no obstacle
        if(!hitRight.collider){
            //calculate position and values
            Vector2 newPosRight = (Vector2)transform.position + (Vector2.right * moveSpeed);
            A_StarNode newNode = createNewNode(newPosRight, currentNode);
            //add node to open list and update current smallest node
            if(nodeNotClosed(newNode)){
                currentNode = addNodeAndGetSmallest(newNode);
            }
            ///printNodeList();
        }

        //RAYCAST UP AND DOWN 
        RaycastHit2D hitUp = Physics2D.Raycast(origin, Vector2.up, checkDistanceJump, obstacleLayer);
        RaycastHit2D hitDown = Physics2D.Raycast(origin, Vector2.down, checkDistanceFall, obstacleLayer);
        //valid direction when there's obstacle below and no obstacle above
        if(!hitUp.collider && hitDown.collider){
            //calculate position and values
            Vector2 newPosJump = (Vector2)transform.position + (Vector2.up * jumpForce);
            A_StarNode newNode = createNewNode(newPosJump, currentNode);
            //add node to open list and update current smallest node
            if(nodeNotClosed(newNode)){
                currentNode = addNodeAndGetSmallest(newNode);
            }
            ///printNodeList();
        }
        
        if(!hitDown.collider){
            //player can fall
            //calculate position and values
            Vector2 newPosFall = (Vector2)transform.position + (Vector2.down * gravityForce);
            A_StarNode newNode = createNewNode(newPosFall, currentNode);
            //add node to open list and update current smallest node
            if(nodeNotClosed(newNode)){
                currentNode = addNodeAndGetSmallest(newNode);
            }
        }

        ///Debug.Log("finished adding new nodes: ");
        ///Debug.Log("added nodes in directions: left = " + !hitLeft.collider + ", right = " + (hitRight.collider==null) + ", jump = " + (hitDown.collider && !hitUp.collider) + ", fall = " + !hitDown.collider);
        ///printNodeList();

        //current node is already set to smallest node
        transform.position = currentNode.getPosition();
        //update the arrays 
        closeNode(currentNode);

        ///Debug.Log("removed node with pos = " + currentNode.getPosition() + ", F = " + currentNode.getF());
        ///Debug.Log("current position is " + transform.position);
        ///Debug.Log("FINISHED UPDATE");
    }

    
    //helper function to set up node
    A_StarNode createNewNode(Vector2 newPos, A_StarNode currentNode){
        ///float newG = updateNumber;
        float newG = currentNode.getG() + Vector2.Distance(newPos, currentNode.getPosition());
        float newH = Vector2.Distance(newPos, goalPosition);
        //create a new node
        A_StarNode newNode = ScriptableObject.CreateInstance<A_StarNode>();
        newNode.nodeSetup(newPos, newG, newH);

        return newNode;
    }

    ///TODO: find another way to calculate h(n) and/or g(n) that has fewer ties??
    ///TODO: how much should we round f(n) and g(n)???


    //helper function to check if node was already eliminated
    bool nodeNotClosed(A_StarNode newNode){
        
        Debug.Log("CHECK NODE: pos = " + newNode.getPosition() + ", F = " + newNode.getF() + ", G = " + newNode.getG() + ", H = " + newNode.getH());
        printClosedList();

        //if node is already in closed list, don't add to open nodes list
        foreach ( A_StarNode closedNode in closedList){

            /*
            Debug.Log("checking closed node with pos = " + closedNode.getPosition());
            Debug.Log("new node has pos = " + newNode.getPosition());
            if(closedNode.getPosition() == newNode.getPosition()){
                Debug.Log("position already in list");
            }
            */

            if(newNode.isEqual(closedNode)){
                Debug.Log("node is already in closed list");
                return false;
            }
        }
        //otherwise node hasn't been closed already
        return true;
    }


    //helper function to close a node
    void closeNode(A_StarNode removeNode){
        
        Debug.Log("REMOVE A NODE: position = " + removeNode.getPosition() + ", f = " + removeNode.getF());
        ///printNodeList();
        
        int len = nodeList.Count;
        ///Debug.Log("length of nodeList = " + len);

        //find the node to remove in the open list
        for(int i = 0; i < len; ++i){
            A_StarNode openNode = (A_StarNode)nodeList[i];

            //remove it from the open nodeList and end
            if( removeNode.isEqual(openNode) ){
                ///Debug.Log("found node to remove at index " + i);
                nodeList.RemoveAt(i);
                break;
            }
        }

        //add it to the closed list
        closedList.Add(removeNode);
        printNodeList();
        ///TODO: figure out if we want to show the whole path including backtracking (then 
        /// we could just show closedList) or only the fastest version (then we'd have to 
        /// store node parents)
    }


    //helper function to find the next node with smallest F
    A_StarNode addNodeAndGetSmallest(A_StarNode newNode){
        
        ///Debug.Log("ADD NODE");
        ///printNodeList();
        float newNodeF = newNode.getF();
        float newNodeG = newNode.getG();
        Vector2 newNodePos = newNode.getPosition();
        ///Debug.Log("adding a node: pos = " + newNodePos + ", F = " + newNodeF + ", G = " + newNodeG + ", H = " + newNode.getH());
        

        //place new node and return the node with the current smallest F
        int len = nodeList.Count;
        //if there are zero nodes in the list, add the new one and return it
        if(len == 0){
            nodeList.Add(newNode);
            ///Debug.Log("default added node: pos = " + newNodePos + ", F = " + newNodeF);
            return newNode;
        }

        //otherwise start with newNode as smallest
        A_StarNode smallestNode = newNode;
        //track whether we replaced a node
        bool shouldAddNode = true;

        //scan through list from beginning and check each node
        /*
        float newNodeF = newNode.getF();
        float newNodeG = newNode.getG();
        Vector2 newNodePos = newNode.getPosition();
        */

        //start at beginning of list and go all the way through to the end
        for (int i = 0; i < len; ++i){
            //compare F values to find the new smallest F and/or replace
            A_StarNode openNode = (A_StarNode)nodeList[i];
            float openNodeF = openNode.getF();
            float openNodeG = openNode.getG();
            Vector2 openNodePos = openNode.getPosition();
            ///Debug.Log("compare new node to current node: pos = " + openNodePos + ", F = " + openNodeF + ", G = " + openNodeG + ", H = " + openNode.getH());

            //replace smallest node if current node has smaller F
            // or it's a tie and current node has bigger G (farther from start)
            if((openNodeF < smallestNode.getF()) || ((openNodeF == smallestNode.getF()) && (openNodeG > smallestNode.getG())) ){
                smallestNode = openNode;
                ///Debug.Log("found a new smallest node: new pos = " + smallestNode.getPosition() + ", new F = " + smallestNode.getF() + ", new G = " + smallestNode.getG()+ ", new H = " + smallestNode.getH());
            }

            //also check if this is the same node as the new one
            if(openNodePos == newNodePos){
                ///Debug.Log("open node has same pos as new node");
                //if this position is already here, don't add it to end of nodelist
                shouldAddNode = false;

                //if existing version has smaller F, don't place this node
                //if position is here with bigger F, replace it
                if (openNodeF > newNodeF){
                    ///Debug.Log("open node has bigger F than new node");
                    nodeList[i] = newNode;
                }

                ///TODO: what if it's the same F but a smaller G? current version keeps the older one
            }
        }
        ///Debug.Log("should we add the new node? " + shouldAddNode);
        
        //finished checking all nodes   
        //add node to end of nodeList if we didn't replace any spots
        if (shouldAddNode){
            nodeList.Add(newNode);
        }

        printNodeList();
        Debug.Log("final length = " + nodeList.Count + ", final smallest: pos = " + smallestNode.getPosition() + ", F = " + smallestNode.getF() + ", G = " + smallestNode.getG() + ", H = " + smallestNode.getH());
        
        //return the current smallest node
        return smallestNode;
    }
    

    //helper function to draw path
    void OnDrawGizmosSelected(){
        Gizmos.color = Color.red;
        int pathSize = closedList.Count;
        
        for (int g = 0; g < pathSize - 1; ++g){
            Vector2 origin = ( (A_StarNode)closedList[g] ).getPosition();
            Vector2 next = ( (A_StarNode)closedList[g + 1] ).getPosition();
            Gizmos.DrawLine(origin, next);
        }

        Gizmos.color = Color.yellow;
        Vector2 rayOrigin = (Vector2)transform.position + rayOffset;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.down * checkDistanceFall);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.up * checkDistanceJump);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.left * checkDistanceX);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector2.right * checkDistanceX);
    }

    ///TODO: does anything go in FixedUpdate?
    void FixedUpdate(){

    }

}
