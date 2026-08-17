using System;
using System.Collections;
using UnityEngine;

public class StarScoreManager : MonoBehaviour
{
    [Header("四关共用星星动画")]
    [Min(1f)]
    [SerializeField] private float enlargeScale = 1.5f;
    [Min(0f)]
    [SerializeField] private float finalScale = 1f;
    [Min(0.01f)]
    [SerializeField] private float enlargeDuration = 0.25f;
    [Min(0.01f)]
    [SerializeField] private float shrinkDuration = 0.25f;

    private Star[] stars;
    private Coroutine showStarsRoutine;
    private Action showCompletedCallback;
    private int visibleStarCount;

    public int StarCount
    {
        get
        {
            ResolveStars();
            return stars.Length;
        }
    }

    public int VisibleStarCount => visibleStarCount;

    private void Awake()
    {
        ResolveStars();
        ResetStars();
    }

    public void ShowStars(int numberOfStars)
    {
        ShowStars(numberOfStars, null);
    }

    public void ShowStars(int numberOfStars, Action onCompleted)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        ResolveStars();
        int targetCount = Mathf.Clamp(numberOfStars, 0, stars.Length);

        if (showStarsRoutine != null)
        {
            StopCoroutine(showStarsRoutine);
            showStarsRoutine = null;
            showCompletedCallback = null;
        }

        if (targetCount < visibleStarCount)
        {
            SetStarsImmediate(targetCount);
            onCompleted?.Invoke();
            return;
        }

        if (targetCount == visibleStarCount)
        {
            onCompleted?.Invoke();
            return;
        }

        showCompletedCallback = onCompleted;
        showStarsRoutine = StartCoroutine(ShowStarsRoutine(targetCount));
    }

    public void SetStarsImmediate(int numberOfStars)
    {
        ResolveStars();

        if (showStarsRoutine != null)
        {
            StopCoroutine(showStarsRoutine);
            showStarsRoutine = null;
        }

        showCompletedCallback = null;
        visibleStarCount = Mathf.Clamp(numberOfStars, 0, stars.Length);

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                continue;
            }

            if (i < visibleStarCount)
            {
                stars[i].SetScale(finalScale);
            }
            else
            {
                stars[i].ResetVisual();
            }
        }
    }

    public void ResetStars()
    {
        SetStarsImmediate(0);
    }

    private IEnumerator ShowStarsRoutine(int targetCount)
    {
        // 保证 StartCoroutine 返回前不会同步跑完整个回调链。
        yield return null;

        for (int i = visibleStarCount; i < targetCount; i++)
        {
            if (stars[i] != null)
            {
                yield return AnimateStar(stars[i]);
            }

            visibleStarCount = i + 1;
        }

        showStarsRoutine = null;
        Action callback = showCompletedCallback;
        showCompletedCallback = null;
        callback?.Invoke();
    }

    private IEnumerator AnimateStar(Star star)
    {
        yield return ChangeStarScale(
            star,
            Vector3.zero,
            Vector3.one * enlargeScale,
            enlargeDuration);

        yield return ChangeStarScale(
            star,
            Vector3.one * enlargeScale,
            Vector3.one * finalScale,
            shrinkDuration);
    }

    private static IEnumerator ChangeStarScale(
        Star star,
        Vector3 startScale,
        Vector3 targetScale,
        float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float progress = Mathf.Clamp01(elapsedTime / duration);
            star.SetScale(Vector3.Lerp(startScale, targetScale, progress).x);
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        star.SetScale(targetScale.x);
    }

    private void ResolveStars()
    {
        if (stars == null || stars.Length == 0)
        {
            stars = GetComponentsInChildren<Star>(true);
        }
    }
}
