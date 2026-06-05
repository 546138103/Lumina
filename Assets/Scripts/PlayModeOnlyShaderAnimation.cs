using System.Collections.Generic;
using UnityEngine;

public class PlayModeOnlyShaderAnimation : MonoBehaviour
{
    private static readonly int LuminaEffectTimeId = Shader.PropertyToID("_LuminaEffectTime");
    private static readonly int TimeXId = Shader.PropertyToID("_TimeX");
    private static readonly List<Renderer> Renderers = new List<Renderer>();

    private MaterialPropertyBlock _block;
    private float _nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeUpdater()
    {
        if (FindObjectOfType<PlayModeOnlyShaderAnimation>() != null) return;

        var updater = new GameObject(nameof(PlayModeOnlyShaderAnimation));
        updater.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(updater);
        updater.AddComponent<PlayModeOnlyShaderAnimation>();
    }

    private void OnEnable()
    {
        RefreshRenderers();
    }

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefreshTime)
        {
            RefreshRenderers();
            _nextRefreshTime = Time.unscaledTime + 1f;
        }

        ApplyRuntimeTime();
    }

    private static void RefreshRenderers()
    {
        Renderers.Clear();
        Renderers.AddRange(FindObjectsOfType<Renderer>(true));
    }

    private void ApplyRuntimeTime()
    {
        float effectTime = Time.timeSinceLevelLoad;

        foreach (Renderer targetRenderer in Renderers)
        {
            if (targetRenderer == null) continue;

            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material material = sharedMaterials[i];
                if (!ShouldControlMaterial(material)) continue;
                if (_block == null) _block = new MaterialPropertyBlock();

                _block.Clear();
                targetRenderer.GetPropertyBlock(_block, i);

                if (material.HasProperty(LuminaEffectTimeId))
                {
                    _block.SetFloat(LuminaEffectTimeId, effectTime);
                }

                if (IsXradiationMaterial(material) && material.HasProperty(TimeXId))
                {
                    _block.SetFloat(TimeXId, material.GetFloat(TimeXId));
                }

                targetRenderer.SetPropertyBlock(_block, i);
            }
        }
    }

    private static bool ShouldControlMaterial(Material material)
    {
        return material != null &&
               (material.HasProperty(LuminaEffectTimeId) || IsXradiationMaterial(material));
    }

    private static bool IsXradiationMaterial(Material material)
    {
        return material != null &&
               material.shader != null &&
               material.shader.name.Contains("Xradiation");
    }
}
