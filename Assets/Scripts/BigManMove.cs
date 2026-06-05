using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BigManMove : MonoBehaviour
{
    NavMeshAgent agent;
    Animator animator;
    float attackDistance = 2.0f;
    public Transform target;
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        float distance = Vector3.Distance(target.position, transform.position);
        if (distance < attackDistance)
        {
            agent.isStopped = true;
            animator.SetBool("attack", true);
           
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
            animator.SetBool("attack", false);
        }
    }
}
