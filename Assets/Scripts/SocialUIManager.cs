using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Playables;

public class SocialUIManager : MonoBehaviour
{
    [Header("极简 UI 面板")]
    public GameObject uiPanel;

    [Header("选项生成")]
    public Transform buttonContainer;
    public GameObject buttonPrefab;

    [Header("过场动画放映机")]
    public PlayableDirector director;

    private Animator _currentNPCAnimator;
    private List<GameObject> _activeButtons = new List<GameObject>();
    private AudioSource _audioSource;
    private bool _isInteractionUIOpen;
    private Transform _currentNPCTransform;
    private Transform _currentPlayerTransform;
    private Vector3 _currentNPCOriginalPosition;
    private Quaternion _currentNPCOriginalRotation;
    private Coroutine _npcAnimationRoutine;

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
        if (director == null) director = gameObject.AddComponent<PlayableDirector>();
    }

    void LateUpdate()
    {
        if (_isInteractionUIOpen)
        {
            ForceCursorForUI();
        }
    }

    void DisplayNode(DialogueNode node)
    {
        if (node == null) return;
        CacheInteractionContext();
        _isInteractionUIOpen = true;
        ForceCursorForUI();
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
                uiPanel.SetActive(false);
                if (res.timelineAsset != null)
                {
                    director.Play(res.timelineAsset);
                }
                HideUI();
                break;

            case InteractionResultType.End:
                HideUI();
                break;

            case InteractionResultType.PlayNPCAnimation:
                HideUI();
                PlayNPCAnimationResult(res);
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
        _isInteractionUIOpen = false;
        uiPanel.SetActive(false);
        GameEventManager.OnToggleUIMode?.Invoke(false);
        ForceCursorForGameplay();
    }

    private void ForceCursorForUI()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ForceCursorForGameplay()
    {
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void CacheInteractionContext()
    {
        _currentNPCTransform = EventManager.CurrentNPCTransform;
        _currentPlayerTransform = EventManager.CurrentPlayerTransform;

        if (_currentNPCTransform != null)
        {
            _currentNPCOriginalPosition = _currentNPCTransform.position;
            _currentNPCOriginalRotation = _currentNPCTransform.rotation;
            _currentNPCAnimator = _currentNPCTransform.GetComponent<Animator>();
        }
        else
        {
            _currentNPCAnimator = null;
        }
    }

    private void PlayNPCAnimationResult(InteractionResult result)
    {
        if (_npcAnimationRoutine != null)
        {
            StopCoroutine(_npcAnimationRoutine);
        }

        _npcAnimationRoutine = StartCoroutine(NPCAnimationRoutine(result));
    }

    private IEnumerator NPCAnimationRoutine(InteractionResult result)
    {
        if (_currentNPCTransform == null)
        {
            yield break;
        }

        FaceNPCToPlayer();

        if (_currentNPCAnimator != null && !string.IsNullOrEmpty(result.npcAnimationState))
        {
            _currentNPCAnimator.Play(result.npcAnimationState, 0, 0f);
        }

        if (result.clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(result.clip);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, result.npcAnimationDuration));

        if (_currentNPCAnimator != null && !string.IsNullOrEmpty(result.npcResetAnimationState))
        {
            _currentNPCAnimator.Play(result.npcResetAnimationState, 0, 0f);
        }

        if (result.restoreNpcPosition)
        {
            _currentNPCTransform.position = _currentNPCOriginalPosition;
        }

        if (result.restoreNpcRotation)
        {
            _currentNPCTransform.rotation = _currentNPCOriginalRotation;
        }

        _npcAnimationRoutine = null;
    }

    private void FaceNPCToPlayer()
    {
        if (_currentNPCTransform == null || _currentPlayerTransform == null) return;

        Vector3 dir = _currentPlayerTransform.position - _currentNPCTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f) return;

        _currentNPCTransform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
