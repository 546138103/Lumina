using System.Collections;
using UnityEngine;

public class StarScoreManager : MonoBehaviour
{
    [Header("星星动画")]
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

    private void Awake()
    {
        ResolveStars();
        ResetStars();
    }

    public void ShowStars(int numberOfStars)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        ResolveStars();

        if (showStarsRoutine != null)
        {
            StopCoroutine(showStarsRoutine);
        }

        showStarsRoutine = StartCoroutine(
            ShowStarsRoutine(Mathf.Clamp(numberOfStars, 0, stars.Length)));
    }

    public void ResetStars()
    {
        ResolveStars();

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
            {
                stars[i].ResetVisual();
            }
        }
    }

    private IEnumerator ShowStarsRoutine(int numberOfStars)
    {
        ResetStars();

        for (int i = 0; i < numberOfStars; i++)
        {
            if (stars[i] != null)
            {
                yield return AnimateStar(stars[i]);
            }
        }

        showStarsRoutine = null;
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
