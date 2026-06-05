using UnityEngine;
using UnityEngine.UI;
using TMPro; // 【新增】必须引入 TextMeshPro
using System.Collections.Generic;
using UnityEngine.Playables; // 引入 Timeline

public class SocialUIManager : MonoBehaviour
{
    [Header("极简 UI 面板")]
    public GameObject uiPanel;

    [Header("选项生成")]
    public Transform buttonContainer;
    public GameObject buttonPrefab;

    [Header("过场动画放映机")]
    public PlayableDirector director;   // 【新增】用来播放 Timeline 的组件

    private Animator _currentNPCAnimator;
    private List<GameObject> _activeButtons = new List<GameObject>();
    private AudioSource _audioSource;

    void OnEnable()
    {
        EventManager.OnOpenInteractionUI += DisplayNode;
        EventManager.OnCloseInteractionUI += HideUI;
    }

    void OnDisable()
    {
        EventManager.OnOpenInteractionUI -= DisplayNode;
        EventManager.OnCloseInteractionUI -= HideUI;
    }

    void Start()
    {
        HideUI();
        _audioSource = Camera.main != null ? Camera.main.GetComponent<AudioSource>() : null;
        // 如果面板上没挂 PlayableDirector，自动加一个
        if (director == null) director = gameObject.AddComponent<PlayableDirector>();
    }

    void DisplayNode(DialogueNode node)
    {

        GameEventManager.OnToggleUIMode?.Invoke(true);
        uiPanel.SetActive(true);
        ClearButtons();


        foreach (BranchOption option in node.options)
        {
            GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
            _activeButtons.Add(btnObj);
            btnObj.transform.Find("select_btn").GetComponent<Image>().sprite = option.result.sprite;
            Button select_btn = btnObj.transform.Find("select_btn").GetComponent<Button>();
            Button voice_btn = btnObj.transform.Find("voice_btn").GetComponent<Button>();
            select_btn.onClick.AddListener(() => OnOptionSelected(option));
            voice_btn.onClick.AddListener(() => OnWaveSelect(option));

            var optionLabel = btnObj.GetComponentInChildren<TMP_Text>();
            if (optionLabel != null && !string.IsNullOrEmpty(option.optionText))
                optionLabel.text = option.optionText;
        }
    }

    void OnOptionSelected(BranchOption selectedOption)
    {
        ClearButtons();
        InteractionResult res = selectedOption.result;

        switch (res.type)
        {

            case InteractionResultType.NextNode:
                DisplayNode(res.nextNode);
                break;

            case InteractionResultType.PlayTimeline:
                // 【新增逻辑】隐藏整个对话框，播放录制好的多人过场动画！
                uiPanel.SetActive(false);
                if (res.timelineAsset != null)
                {
                    director.Play(res.timelineAsset);
                }
                HideUI();
                break;

        }
    }
    void OnWaveSelect(BranchOption selectedOption)
    {
        if (selectedOption.result.clip != null && _audioSource != null)
            _audioSource.PlayOneShot(selectedOption.result.clip);
    }

    void ClearButtons()
    {
        foreach (GameObject btn in _activeButtons) Destroy(btn);
        _activeButtons.Clear();
    }

    void HideUI()
    {
        uiPanel.SetActive(false);
        GameEventManager.OnToggleUIMode?.Invoke(false);
    }
}