using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class A_Star : MonoBehaviour{
    
    //public variables
    [Header ("Start Position")]
    public Vector2 startNode;

    [Header ("Goal Position")]
    public Vector2 goalNode;

    [Header("Obstacle Check (Raycast)")]
    public LayerMask obstacleLayer;

    [Header("Object to Move")]
    public GameObject playerObject;

    ///TODO: also get movement functions

    //private variables
    private Vector2 nextPos; //record when we're at goal
    int updateNumber = 0; //number of updates we've done (use for g)

    //raycasting variables
    private float checkDistance = 2f; ///TODO: should we make these public and let user set these??
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

        //A* starts at provided start node
        transform.position = startNode;
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
        ++updateNumber;

        ///Debug.Log("STARTING UPDATE " + updateNumber);
        ///printNodeList();
        
        //do nothing this frame if we reached the goal
        if(nextPos == goalNode){
            Debug.Log("reached goal!");
            return;
        }
    
        ///TODO: expand to diagonal directions while in the air?
        ///TODO: next position for the new node determined by provided jumpforce and move speed
        ///     or by jump and move functions??
        ///    (current rough-grain version: next pos determined by the raycast distance)
        
        //raycast in each neighbour direction to see if there's an obstacle
        //RAYCAST LEFT
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hitLeft = Physics2D.Raycast(origin, Vector2.left, checkDistance, obstacleLayer);
        //valid direction when there's no obstacle
        if(hitLeft.collider == null){
            //calculate position and values
            Vector2 newPos = (Vector2)transform.position + (Vector2.left * checkDistance);
            float newG = updateNumber;
            float newH = Vector2.Distance(newPos, goalNode);
            //create a new node
            A_StarNode newNode = ScriptableObject.CreateInstance<A_StarNode>();
            newNode.nodeSetup(newPos, newG, newH);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }

        //RAYCAST RIGHT
        RaycastHit2D hitRight = Physics2D.Raycast(origin, Vector2.right, checkDistance, obstacleLayer);
        //valid direction when there's no obstacle
        if(hitRight.collider == null){
            //calculate position and values
            Vector2 newPos = (Vector2)transform.position + (Vector2.right * checkDistance);
            float newG = updateNumber;
            float newH = Vector2.Distance(newPos, goalNode);
            //create a new node
            A_StarNode newNode = ScriptableObject.CreateInstance<A_StarNode>();
            newNode.nodeSetup(newPos, newG, newH);
            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }

        //RAYCAST UP AND DOWN 
        RaycastHit2D hitUp = Physics2D.Raycast(origin, Vector2.up, checkDistance, obstacleLayer);
        RaycastHit2D hitDown = Physics2D.Raycast(origin, Vector2.down, checkDistance, obstacleLayer);
        //valid direction when there's obstacle below and no obstacle above
        if(hitUp.collider == null && hitDown.collider != null){
            //calculate position and values
            Vector2 newPos = (Vector2)transform.position + (Vector2.up * checkDistance);
            float newG = updateNumber;
            float newH = Vector2.Distance(newPos, goalNode);
            //create a new node
            A_StarNode newNode = ScriptableObject.CreateInstance<A_StarNode>();
            newNode.nodeSetup(newPos, newG, newH);

            //add node to sorted nodes (if not already there), sorted by F
            addNodeToList(newNode);
            ///printNodeList();
        }
        
        //set position to node with smallest f(n) from the node list
        A_StarNode nextNode = (A_StarNode)nodeList[0];
        nextPos = nextNode.getPosition();
        transform.position = nextPos;

        //update the arrays 
        nodeList.RemoveAt(0);
        pathList.Add(nextNode);

        ///Debug.Log("removed node with pos = " + nextPos + ", F = " + nextNode.getF());
        ///Debug.Log("FINISHED UPDATE");
    }

    
    //helper function to draw path
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        int pathSize = pathList.Count;
        
        for (int g = 0; g < pathSize - 1; ++g){
            Vector2 origin = ( (A_StarNode)pathList[g] ).getPosition();
            Vector2 next = ( (A_StarNode)pathList[g + 1] ).getPosition();
            Gizmos.DrawLine(origin, next);
        }
    }

    
    //helper function to add node to list
    void addNodeToList(A_StarNode newNode){
        //set up helper variables
        int len = nodeList.Count;
        int sortedIndex = -1;
        A_StarNode storedNode1;
        A_StarNode storedNode2;

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
        Vector2 newNodePos = newNode.getPosition();

        //start at beginning of list
        for (int i = 0; i < len; ++i){
            //compare F values to find the sorted index
            A_StarNode currentNode = (A_StarNode)nodeList[i];
            float currentNodeF = currentNode.getF();
            Vector2 currentNodePos = currentNode.getPosition();

            //before the sorted point: F is smaller than new node's F
            if (currentNodeF <= newNodeF){
                ///Debug.Log("before sorting point: currentF = " + currentNodeF + ", newF = " + newNodeF);
                
                if(currentNodePos == newNodePos){
                    //if this position is here already, don't place this node
                    ///Debug.Log("node already in list so didn't place it: pos = " + newNodePos + ", F = " + newNodeF);
                    return;
                }
            }
            //found the sorted point: F is greater than new node's F
            else if (currentNodeF > newNodeF){
                ///Debug.Log("at/after sorting point: currentF = " + currentNodeF + ", newF = " + newNodeF);
                
                if(sortedIndex < 0){
                    //record index to place this node
                    sortedIndex = i;
                    ///Debug.Log("sorted index = " + sortedIndex);
                }

                //continue scanning forward to see if there's a position we must replace
                if (currentNodePos == newNodePos){
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
