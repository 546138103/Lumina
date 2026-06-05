using UnityEngine;

// 自动添加依赖组件，确保挂载此脚本时自动加上 HighlightableObject
[RequireComponent(typeof(HighlightableObject))]
public class ObjectHighlightController : MonoBehaviour
{
    public enum HighlightMode { None, Constant, Flashing }

    [Header("Base Settings")]
    public HighlightMode mode = HighlightMode.None;

    [Header("Hover Settings")]
    [Tooltip("Enable highlight when mouse hovers over this object")]
    public bool enableHoverHighlight = true;
    public Color hoverColor = Color.red;

    // --- ✨ 新增：距离感应设置 ✨ ---
    [Header("Distance Settings")]
    [Tooltip("是否启用距离限制")]
    public bool useDistanceCheck = true;
    [Tooltip("触发高亮的最大距离（米）")]
    public float maxHighlightDistance = 5f;
    [Tooltip("距离测量的参考目标（通常拖入你的 ThirdPersonPlayer）。如果不填，默认使用主摄像机。")]
    public Transform playerTransform;
    // --------------------------------

    [Header("Constant Settings")]
    public Color constantColor = Color.yellow;
    [Tooltip("Enable fade in/out effect")]
    public bool useFade = true;

    [Header("Flashing Settings")]
    public Color flashColorMin = Color.blue;
    public Color flashColorMax = Color.cyan;
    public float flashFrequency = 2f;

    [Header("Occluder Settings")]
    public bool isOccluder = false;

    private HighlightableObject _highlight;
    private bool _isMouseOver = false;
    private bool _wasHighlightedThisFrame = false; // 用于记录上一帧是否因为距离合格而亮起

    void Awake()
    {
        _highlight = GetComponent<HighlightableObject>();
    }

    void Start()
    {
        // 如果没有手动赋予玩家的 Transform，自动寻找主摄像机作为备用测距点
        if (useDistanceCheck && playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        ApplyHighlightSettings();
    }

    void OnValidate()
    {
        if (_highlight != null && Application.isPlaying)
        {
            ApplyHighlightSettings();
        }
    }

    public void ApplyHighlightSettings()
    {
        if (isOccluder) { _highlight.OccluderOn(); } else { _highlight.OccluderOff(); }
        _highlight.Off();

        switch (mode)
        {
            case HighlightMode.Constant:
                if (useFade) _highlight.ConstantOn(constantColor);
                else _highlight.ConstantOnImmediate(constantColor);
                break;
            case HighlightMode.Flashing:
                _highlight.FlashingOn(flashColorMin, flashColorMax, flashFrequency);
                break;
            case HighlightMode.None:
                break;
        }
    }

    // --- ✨ 新增：距离检测辅助方法 ✨ ---
    private bool IsWithinDistance()
    {
        if (!useDistanceCheck) return true; // 如果不启用距离检查，默认允许
        if (playerTransform == null) return true; // 如果找不到测距目标，为防止报错默认允许

        // 计算此物体和玩家（或摄像机）之间的三维空间距离
        float currentDistance = Vector3.Distance(transform.position, playerTransform.position);
        return currentDistance <= maxHighlightDistance;
    }

    void OnMouseEnter()
    {
        if (enableHoverHighlight)
        {
            _isMouseOver = true;
            // 注意：这里不再直接调用 _highlight.On()，而是把权力完全交给 Update 来做距离的实时判断
        }
    }

    void OnMouseExit()
    {
        if (enableHoverHighlight)
        {
            _isMouseOver = false;
            ApplyHighlightSettings(); // 鼠标移出，立刻恢复默认状态
            _wasHighlightedThisFrame = false;
        }
    }

    void Update()
    {
        if (enableHoverHighlight && _isMouseOver && mode == HighlightMode.None)
        {
            // 实时判断：鼠标悬停中，且距离足够近
            if (IsWithinDistance())
            {
                _highlight.On(hoverColor);
                _wasHighlightedThisFrame = true;
            }
            else
            {
                // ✨ 关键边缘情况处理：
                // 如果鼠标一直指着物体，但玩家往后退，退出了最大距离范围
                // 需要强制熄灭高亮
                if (_wasHighlightedThisFrame)
                {
                    ApplyHighlightSettings();
                    _wasHighlightedThisFrame = false;
                }
            }
        }
    }
    private void AutoAttachScriptBToCamera()
    {
        // 1. 获取主相机
        Camera mainCam = Camera.main;

        if (mainCam == null)
        {
            Debug.LogWarning("未找到主相机（MainCamera），请检查标签设置。");
            return;
        }

        // 2. 检查相机上是否已经有了脚本 B
        // 假设脚本 B 的类名就是 HighlightingEffect
        if (mainCam.GetComponent<HighlightingEffect>() == null)
        {
            mainCam.gameObject.AddComponent<HighlightingEffect>();
            Debug.Log($"已成功为 {mainCam.name} 挂载 HighlightingEffect。");
        }
    }
    // 当脚本被挂载到物体上时触发
    private void Reset()
    {
        AutoAttachScriptBToCamera();
    }
}