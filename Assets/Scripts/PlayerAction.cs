using Cinemachine;
using StarterAssets;
using UnityEngine;

public class PlayerAction : MonoBehaviour
{
    private StarterAssetsInputs assetsInputs;
    private Animator animator;
    public CinemachineVirtualCamera virtualCamera;
    float interactDistance = 7f;

    void Start()
    {
        assetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 瞄准逻辑保持不变
       // virtualCamera.gameObject.SetActive(assetsInputs.aim);

        // 手动挥手逻辑
        if (Input.GetKeyDown(KeyCode.E)) animator.Play("Waving", 1, 0);
        animator.SetLayerWeight(1, Input.GetKey(KeyCode.E) ? 1 : 0);

        // 射线检测交互逻辑
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                // ✨ 核心解耦：尝试获取 IInteractable 接口，而不是具体的 NPCAction
                IInteractable interactableObj = hit.transform.GetComponent<IInteractable>();

                if (interactableObj != null)
                {
                    // 玩家转向目标
                    FaceTarget(hit.transform);

                    // 触发目标自身的交互逻辑
                    interactableObj.OnInteract(transform);
                }
            }
        }
    }

    private void FaceTarget(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}