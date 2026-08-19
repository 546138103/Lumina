using UnityEngine;
using UnityEngine.UI;

public class Star : MonoBehaviour
{
    private const string YellowStarChildName = "yellowStart";

    private Image yellowStar;

    public Transform YellowStarTransform =>
        yellowStar != null ? yellowStar.transform : null;

    private void Awake()
    {
        ResolveYellowStar();
        ResetVisual();
    }

    public void ResetVisual()
    {
        if (ResolveYellowStar())
        {
            yellowStar.transform.localScale = Vector3.zero;
        }
    }

    public void SetScale(float scale)
    {
        if (ResolveYellowStar())
        {
            yellowStar.transform.localScale = Vector3.one * scale;
        }
    }

    private bool ResolveYellowStar()
    {
        if (yellowStar != null)
        {
            return true;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == transform ||
                children[i].name != YellowStarChildName)
            {
                continue;
            }

            yellowStar = children[i].GetComponent<Image>();
            if (yellowStar == null)
            {
                Debug.LogError(
                    $"[Star] 子物体 {YellowStarChildName} 上没有 Image 组件。",
                    children[i]);
                return false;
            }

            return true;
        }

        Debug.LogError(
            $"[Star] {name} 的子物体中找不到名为 {YellowStarChildName} 的对象。",
            this);
        return false;
    }
}
