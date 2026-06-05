using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables; // 引入 Timeline 命名空间

public enum InteractionResultType { PlayTimeline, NextNode }

[System.Serializable]
public class InteractionResult
{
    public InteractionResultType type;
    public DialogueNode nextNode;
    public AudioClip clip;
    public PlayableAsset timelineAsset; // 【新增】录制好的多人互动 Timeline 文件
    public Sprite sprite;
}

[System.Serializable]
public class BranchOption
{
    public string optionText;
    public InteractionResult result;
}

[CreateAssetMenu(fileName = "NewDialogueNode", menuName = "社会互动/新建极简对话节点")]
public class DialogueNode : ScriptableObject
{
    public List<BranchOption> options;
}