using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HiderHeuristic : MonoBehaviour
{
    [SerializeField]
    public Rigidbody rb;

    [SerializeField]
    public int speed;

    [SerializeField]
    public int seeker_detect_range;

    [SerializeField]
    public int obstacledetectrange;

    Vector3 lastDirection;

    Vector3 Direction;

    public List<GameObject> target_list = new List<GameObject>();

    public List<GameObject> wall_list = new List<GameObject>();

    public bool is_obstacle_around;

    public int a;

    [SerializeField]
    private int update_freq;

    private int decision_count = 0;

    public bool special_case; // use to handle the case where the hider run into the seeker while in wall

    public bool _isActive;

    void Start()
    {
        is_obstacle_around = false;
    }

    // Update is called once per frame

    void FixedUpdate()
    {
        // Debug.Log("Update");
        //Debug.Log("decision_count: " + decision_count);
        if (!_isActive)
        {
            decision_count = 0;
            Direction = new Vector3(0,0,0);
            // Set rigid body velocity and angular velocity to zero
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            // transform.forward = new Vector3(0,0,0);
            return;
        }
        decision_count++;
        // Debug.Log(Time.deltaTime);
        transform.position = transform.position + (Direction* speed) *Time.deltaTime; 
  
        if (decision_count % update_freq != 0)
        {
            return;
        }
        
        is_obstacle_around = false;
        special_case = false;

        Direction = transform.forward;

        //detect seeker in range
        target_list.Clear();
        RaycastHit[] hits1 = Physics.SphereCastAll(new Ray(transform.position, Vector3.up),seeker_detect_range); //figure why 5 doesn't work
        foreach (var hit in hits1)
        {
            if (hit.collider.CompareTag("Player") && !target_list.Contains(hit.collider.gameObject))
            {
                target_list.Add(hit.collider.gameObject);
            }
        }

        //detect wall in range
        wall_list.Clear();

        RaycastHit[] hits = Physics.SphereCastAll(new Ray(transform.position, Vector3.up),obstacledetectrange); 
        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("wall") && !wall_list.Contains(hit.collider.gameObject))
            {
                wall_list.Add(hit.collider.gameObject);
            }
        }


        //calculate running Direction

        if (target_list.Count == 0) //no seeker
        {
            if (wall_list.Count == 0) //no wall
            {
                Direction = transform.forward;
            }
            else // there is wall
            {
                for (int j = 0;j <= 9;j++)
                {
                    
                    Vector3 ray_d11 = rotate(Direction,Mathf.PI/180*20f*j,true); // change this angle
                    Vector3 ray_d21 = rotate(Direction,Mathf.PI/180*20f*j,false);

                    // destination = transform.position + ray_d11;
                    // Direction = ray_d11;
                    if (NoWall(transform,ray_d11,obstacledetectrange))
                    {
 
                        Direction = ray_d11;
                        break;
                    }
                    
                    if (NoWall(transform,ray_d21,obstacledetectrange))
                    {
                        Direction = ray_d21;
                        break;
                    }

                }

                Direction = get_away_from_wall(wall_list,Direction);
               
            }
        }
        else //seeker chasing 
        {
            if (wall_list.Count == 0) //no wall directly run away
            {
                Direction = transform.position - target_list[0].transform.position;
            }
            else // there is wall then running while in wall
            {
                Direction = transform.position - target_list[0].transform.position;

                for (int j = 0;j <= 9;j++)
                {
                    
                    Vector3 ray_d11 = rotate(Direction,Mathf.PI/180*20f*j,true); // change this angle
                    Vector3 ray_d21 = rotate(Direction,Mathf.PI/180*20f*j,false);

                    // destination = transform.position + ray_d11;
                    // Direction = ray_d11;
                    if (NoWall(transform,ray_d11,obstacledetectrange))
                    {
 
                        Direction = ray_d11;
                        break;
                    }
                    
                    if (NoWall(transform,ray_d21,obstacledetectrange))
                    {
                        Direction = ray_d21;
                        break;
                    }

                }
                
            }

        }


        for (int j = 0;j <= 36;j++)
        {
            
            Vector3 ray_d11 = rotate(Direction,Mathf.PI/180*10f*j,true); // change this angle
            Vector3 ray_d12 = rotate(Direction,Mathf.PI/180*10f*(j+1),true); // change this angle
            Vector3 ray_d13 = rotate(Direction,Mathf.PI/180*10f*(j-1),true); // change this angle

            if (NoObstacle(transform,ray_d11,obstacledetectrange)  &&  NoObstacle(transform,ray_d12,obstacledetectrange) && NoObstacle(transform,ray_d13,obstacledetectrange)  )
            {
                // Debug.Log("turn cw"+j);
                Direction = ray_d11;
                break;
            }

        }

        Direction.y = 0f;
        Direction.Normalize();
        transform.forward = Direction;
        decision_count = 0;
    }

    Vector3 rotate(Vector3 oD,float theta, bool clockwise)
    {
        float sinAngle = Mathf.Sin(theta);
        float cosAngle = Mathf.Cos(theta);
        float newX;
        float newZ;
        if (clockwise == false)
        {
            newX = oD.x * cosAngle - oD.z * sinAngle;
            newZ = oD.x * sinAngle + oD.z * cosAngle;
        }
        else
        {
            newX = oD.x * cosAngle + oD.z * sinAngle;
            newZ = -oD.x * sinAngle + oD.z * cosAngle;
        }
        Vector3 Heading = new Vector3(newX, 0, newZ);

        return Heading;
    }

    Vector3 get_away_from_wall(List<GameObject> wall_list,Vector3 Direction)
    {
        if (wall_list.Count == 1)
        {
            // Debug.Log("Here");
            Vector3 wall_normal = wall_list[0].transform.forward.normalized;
            
            if (Vector3.Dot(wall_normal,Direction.normalized) >=0.95f)
            {
                // Debug.Log("HEre1");
                Direction = rotate(Direction,Mathf.PI/4f,false);
            }
            else if (Vector3.Dot(wall_normal,Direction.normalized) <= - 0.95f)
            {
                // Debug.Log("HEre2");
                Direction = rotate(Direction,Mathf.PI/4f,true);
            }
        }
        return Direction;
    }


    bool NoWall(Transform myself, Vector3 direction, float rayLength)
    {
        // Cast a ray from the 'myself' position in the specified direction
        RaycastHit hit;
        int wallLayer = 7;
        if (Physics.Raycast(myself.position, direction, out hit, rayLength,1 << wallLayer))
        {
            // If the ray hits something, check if the hit object has the tag "Obstacle"
            if (hit.collider.CompareTag("wall"))
            {
                // If it's tagged as an obstacle, return false
                return false;
            }
        }
        // If no obstacles are hit or if the hit object is not tagged as an obstacle, return true
        return true;
    }

    bool NoObstacle(Transform myself, Vector3 direction, float rayLength)
    {
        // Cast a ray from the 'myself' position in the specified direction
        RaycastHit hit;
        int Obstaclelayer = 8;
        if (Physics.Raycast(myself.position, direction, out hit, rayLength, 1 << Obstaclelayer))
        {
            // If the ray hits something, check if the hit object has the tag "Obstacle"
            if (hit.collider.CompareTag("Obstacle"))
            {
                // If it's tagged as an obstacle, return false
                return false;
            }
        }
        
        // If no obstacles are hit or if the hit object is not tagged as an obstacle, return true
        return true;
    }

}
