using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NPCAction : MonoBehaviour, IInteractable
{
    // Start is called before the first frame update
    private Animator animator;
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnInteract(Transform target) => DoInteract(target);

    public void DoInteract(Transform target) {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0; 
        Quaternion endRot = Quaternion.LookRotation(dir);//这是个迷惑函数，看起来是动词函数，其实这是生成了四元数
        transform.rotation = endRot;
        animator.Play("Waving", 0, 0);

    }
}
