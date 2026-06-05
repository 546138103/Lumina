using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BodyPartChoiceGame : MinigameBase
{
    [Header("UI 按钮引用")]
    public Button correctButton;
    public Button[] wrongButtons;
    public Button exitButton;

    [Header("反馈设置")]
    public string successChatText = "太棒了！你用嘴巴表达了想法！";
    public AudioClip successAudio;
    public AudioClip wrongAudio;
    private AudioSource audioSource;

    // 注意：这里已经删除了它自己的 isProcessing 变量，直接用父类的！

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();

        correctButton.onClick.AddListener(OnCorrectClicked);
        foreach (var btn in wrongButtons) btn.onClick.AddListener(() => OnWrongClicked(btn));
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
    }

    public override void OpenGame()
    {
        // 这一步会执行父类逻辑，其中已经包括了 isProcessing = false
        base.OpenGame();

        // 子类只负责恢复自己的UI表现
        correctButton.interactable = true;
        if (exitButton != null) exitButton.interactable = true;
        foreach (var btn in wrongButtons) btn.interactable = true;
    }

    private void OnCorrectClicked()
    {
        // ✨ 直接使用父类的防抖锁
        if (isProcessing) return;
        isProcessing = true;

        DisableAllButtons();
        if (successAudio != null) audioSource.PlayOneShot(successAudio);

        GameEventManager.OnUpdatePlayerChat?.Invoke(successChatText);
        StartCoroutine(CloseAfterDelay(1.5f));
    }

    private void OnWrongClicked(Button clickedButton)
    {
        // ✨ 直接使用父类的防抖锁
        if (isProcessing) return;

        if (wrongAudio != null) audioSource.PlayOneShot(wrongAudio);
        StartCoroutine(WrongFeedbackRoutine(clickedButton));
    }

    private void OnExitClicked()
    {
        // ✨ 直接使用父类的防抖锁
        if (isProcessing) return;
        isProcessing = true;

        DisableAllButtons();
        CloseGame();
    }

    private IEnumerator WrongFeedbackRoutine(Button btn)
    {
        Image btnImage = btn.GetComponent<Image>();
        Color originalColor = btnImage.color;

        btnImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.interactable = false;

        yield return new WaitForSeconds(1f);

        // 检查父类的锁
        if (!isProcessing)
        {
            btnImage.color = originalColor;
            btn.interactable = true;
        }
    }

    private void DisableAllButtons()
    {
        correctButton.interactable = false;
        if (exitButton != null) exitButton.interactable = false;
        foreach (var btn in wrongButtons) btn.interactable = false;
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CloseGame();
    }
}