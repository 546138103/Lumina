using UnityEngine;
using UnityEngine.AI;

public class ClickToController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;
    private Camera mainCam;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        mainCam = Camera.main;

        // 关键：禁用 Agent 的自动旋转，这样我们才能手动控制朝向
        agent.updateRotation = false;
    }

    void Update()
    {
        // 1. 处理鼠标点击移动
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                agent.SetDestination(hit.point);
            }
        }

        // 2. 强制角色正对着相机（看板效果）
        FaceCamera();

        // 3. 更新动画参数
        float currentSpeed = agent.velocity.magnitude;
        anim.SetFloat("Speed", currentSpeed);
    }

    void FaceCamera()
    {
        // 获取相机在水平面上的位置，防止角色上下倾斜
        Vector3 lookPos = mainCam.transform.position;
        lookPos.y = transform.position.y;

        // 计算朝向
        transform.LookAt(lookPos);

        // 注意：Mixamo 模型通常正面朝向 Z 轴。
        // 如果发现角色是背对着相机的，取消下面这一行的注释：
        // transform.Rotate(0, 180, 0); 
    }
}