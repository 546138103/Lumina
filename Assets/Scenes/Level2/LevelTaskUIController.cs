using System;
using UnityEngine;
using UnityEngine.UI;

public enum TaskTeachingUiState
{
    Hidden,
    Default,
    Wait,
    Respond,
    QueueGuidance
}

[Serializable]
public class LevelTaskUiSet
{
    [Tooltip("玩家进入当前关大圈后显示。")]
    public GameObject taskUi;
    [Tooltip("第一、二关的普通教学图；也可作为其他关卡的默认教学图。")]
    public GameObject teachingUi;
    [Tooltip("第三关 NPC 说话阶段显示。")]
    public GameObject waitUi;
    [Tooltip("第三关 NPC 停顿、允许玩家回应时显示。")]
    public GameObject respondUi;
    [Tooltip("第四关选错位置或长时间未找到正确位置时显示。")]
    public GameObject queueGuidanceUi;
}

public class LevelTaskUIController : MonoBehaviour
{
    [Header("四关 UI（索引 0-3）")]
    [SerializeField] private LevelTaskUiSet[] stages = new LevelTaskUiSet[4];

    [Header("四关共用 2 秒环形进度条")]
    [Tooltip("Image Type 设为 Filled，Fill Method 设为 Radial 360。")]
    [SerializeField] private Image progressRing;

    public int StageCount => stages != null ? stages.Length : 0;

    private void Awake()
    {
        HideAll();
    }

    public void ShowTaskUi(int stageIndex, bool visible)
    {
        LevelTaskUiSet stage = GetStage(stageIndex);
        if (stage?.taskUi != null)
        {
            stage.taskUi.SetActive(visible);
        }
    }

    public void ShowTeachingUi(int stageIndex, TaskTeachingUiState state)
    {
        LevelTaskUiSet stage = GetStage(stageIndex);
        if (stage == null)
        {
            return;
        }

        SetActive(stage.teachingUi, state == TaskTeachingUiState.Default);
        SetActive(stage.waitUi, state == TaskTeachingUiState.Wait);
        SetActive(stage.respondUi, state == TaskTeachingUiState.Respond);
        SetActive(
            stage.queueGuidanceUi,
            state == TaskTeachingUiState.QueueGuidance);
    }

    public void SetProgress(float normalizedProgress)
    {
        if (progressRing == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(normalizedProgress);
        if (progress > 0f && !progressRing.gameObject.activeSelf)
        {
            progressRing.gameObject.SetActive(true);
        }

        progressRing.fillAmount = progress;
        progressRing.enabled = progress > 0f;
    }

    public void HideStage(int stageIndex)
    {
        ShowTaskUi(stageIndex, false);
        ShowTeachingUi(stageIndex, TaskTeachingUiState.Hidden);
    }

    public void HideAll()
    {
        if (stages != null)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                HideStage(i);
            }
        }

        SetProgress(0f);
    }

    private LevelTaskUiSet GetStage(int stageIndex)
    {
        if (stages == null || stageIndex < 0 || stageIndex >= stages.Length)
        {
            return null;
        }

        return stages[stageIndex];
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
