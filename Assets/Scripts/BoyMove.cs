using UnityEngine;
using UnityEngine.AI;

    /// <summary>
    /// Use physics raycast hit from mouse click to set agent destination
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class BoyMove : MonoBehaviour
    {
        NavMeshAgent m_Agent;
        RaycastHit m_HitInfo = new RaycastHit();
        Animator m_Animator;

        void Start()
        {
            m_Animator = GetComponent<Animator>();
            m_Agent = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftShift))
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray.origin, ray.direction, out m_HitInfo))
                {
                    m_Agent.destination = m_HitInfo.point;
                    
                }

            }
            if (m_Agent.velocity.magnitude > 0f)
            {
                m_Animator.SetBool("isRunning", true);
            }
            else
            {
                m_Animator.SetBool("isRunning", false);
            }
        }
    }
