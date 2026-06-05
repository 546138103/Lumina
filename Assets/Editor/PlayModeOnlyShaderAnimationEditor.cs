using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeOnlyShaderAnimationEditor
{
    private static readonly int LuminaEffectTimeId = Shader.PropertyToID("_LuminaEffectTime");
    private static readonly int TimeXId = Shader.PropertyToID("_TimeX");
    private static readonly List<Renderer> Renderers = new List<Renderer>();
    private static MaterialPropertyBlock Block;
    private static double _nextRefreshTime;

    static PlayModeOnlyShaderAnimationEditor()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += _ => RefreshRenderers();
    }

    private static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (EditorApplication.timeSinceStartup >= _nextRefreshTime)
        {
            RefreshRenderers();
            _nextRefreshTime = EditorApplication.timeSinceStartup + 1.0;
        }

        FreezeEditorTime();
    }

    private static void RefreshRenderers()
    {
        Renderers.Clear();
        Renderers.AddRange(Object.FindObjectsOfType<Renderer>(true));
    }

    private static void FreezeEditorTime()
    {
        foreach (Renderer targetRenderer in Renderers)
        {
            if (targetRenderer == null) continue;

            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material material = sharedMaterials[i];
                if (!ShouldControlMaterial(material)) continue;
                if (Block == null) Block = new MaterialPropertyBlock();

                Block.Clear();
                targetRenderer.GetPropertyBlock(Block, i);

                if (material.HasProperty(LuminaEffectTimeId))
                {
                    Block.SetFloat(LuminaEffectTimeId, 0f);
                }

                if (IsXradiationMaterial(material) && material.HasProperty(TimeXId))
                {
                    Block.SetFloat(TimeXId, 0f);
                }

                targetRenderer.SetPropertyBlock(Block, i);
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
