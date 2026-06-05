using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChatBubble : MonoBehaviour
{
    private Text text;
    private Coroutine DelayCoroutine;
    private bool isInitialized = false;

    // ✨ 必须用 Awake 代替 Start
    void Awake()
    {
        InitComponents();
    }
    private void Start()
    {
        //初始化时先隐藏气泡，避免第一帧闪现
        gameObject.SetActive(false);
    }
    private void InitComponents()
    {
        if (isInitialized) return;
        text = GetComponentInChildren<Text>();
        isInitialized = true;
    }

    public void Show(string content)
    {
        InitComponents(); // 安全锁
        gameObject.SetActive(true);

        if (text != null)
        {
            text.text = content;
        }

        // ✨ 极其重要的一行：强制立刻重新计算 UI 尺寸，防止第一帧隐形 Bug
        Canvas.ForceUpdateCanvases();

        if (DelayCoroutine != null)
        {
            StopCoroutine(DelayCoroutine);
        }
        DelayCoroutine = StartCoroutine(HideCoroutine());
    }

    private IEnumerator HideCoroutine()
    {
        yield return new WaitForSeconds(2f);
        gameObject.SetActive(false);
    }
}