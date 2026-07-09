using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PoseLevel2SceneInstaller
{
    private const string Level2ScenePath = "Assets/Scenes/Level2/Level2.unity";
    private const string HandRaisingClipPath = "Assets/Scenes/Level2/Hand Raising.anim";
    private const string WavingClipPath = "Assets/Scenes/Level2/Waving.anim";

    static PoseLevel2SceneInstaller()
    {
        EditorApplication.delayCall += InstallIntoOpenLevel2;
    }

    [MenuItem("Lumina/Level2/Install Pose Social Control")]
    public static void InstallIntoOpenLevel2()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != Level2ScenePath)
        {
            return;
        }

        PoseControlModeManager modeManager =
            Object.FindObjectOfType<PoseControlModeManager>();
        if (modeManager == null)
        {
            Debug.LogError("[PoseLevel2SceneInstaller] Level2 中找不到 PoseControlModeManager。");
            return;
        }

        GameObject playerRoot = modeManager.gameObject;
        PoseMovementInput movementInput =
            playerRoot.GetComponent<PoseMovementInput>();
        PoseStarterAssetsInputAdapter inputAdapter =
            playerRoot.GetComponent<PoseStarterAssetsInputAdapter>();
        PoseSocialActionRecognizer recognizer =
            playerRoot.GetComponent<PoseSocialActionRecognizer>();

        PoseMovementSourceManager movementSource =
            GetOrAdd<PoseMovementSourceManager>(playerRoot);
        PosePresetSocialAnimator presetAnimator =
            GetOrAdd<PosePresetSocialAnimator>(playerRoot);
        PoseMediaPipeArmDriver armDriver =
            GetOrAdd<PoseMediaPipeArmDriver>(playerRoot);
        PoseSocialPresentationController presentation =
            GetOrAdd<PoseSocialPresentationController>(playerRoot);
        PoseCalibrationCoordinator calibration =
            GetOrAdd<PoseCalibrationCoordinator>(playerRoot);

        SetObjectReference(movementSource, "modeManager", modeManager);
        SetObjectReference(movementSource, "poseMovementInput", movementInput);

        SetObjectReference(
            presetAnimator,
            "handRaisingClip",
            AssetDatabase.LoadAssetAtPath<AnimationClip>(HandRaisingClipPath));
        SetObjectReference(
            presetAnimator,
            "wavingClip",
            AssetDatabase.LoadAssetAtPath<AnimationClip>(WavingClipPath));

        PipeServer pipeServer = Object.FindObjectOfType<PipeServer>();
        SetObjectReference(armDriver, "pipeServer", pipeServer);

        SetObjectReference(presentation, "modeManager", modeManager);
        SetObjectReference(presentation, "actionRecognizer", recognizer);
        SetObjectReference(presentation, "presetAnimator", presetAnimator);
        SetObjectReference(presentation, "mediaPipeArmDriver", armDriver);

        SetObjectReference(calibration, "modeManager", modeManager);
        SetObjectReference(calibration, "movementSourceManager", movementSource);
        SetObjectReference(calibration, "poseMovementInput", movementInput);
        SetObjectReference(calibration, "presentationController", presentation);
        SetObjectReference(calibration, "mediaPipeArmDriver", armDriver);
        SetObjectReference(calibration, "presetAnimator", presetAnimator);
        SetObjectReference(calibration, "actionRecognizer", recognizer);

        if (inputAdapter != null)
        {
            SetObjectReference(
                inputAdapter,
                "movementSourceManager",
                movementSource);
        }

        Collider socialZone = GetSocialZone(recognizer);
        if (socialZone != null)
        {
            PoseSocialModeTrigger trigger =
                GetOrAdd<PoseSocialModeTrigger>(socialZone.gameObject);
            SetObjectReference(trigger, "modeManager", modeManager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            "[PoseLevel2SceneInstaller] 已在当前 Level2 场景补齐姿态/社交控制组件。请保存场景。",
            playerRoot);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void SetObjectReference(
        Object target,
        string propertyName,
        Object value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue == value)
        {
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static Collider GetSocialZone(PoseSocialActionRecognizer recognizer)
    {
        if (recognizer == null)
        {
            return null;
        }

        SerializedObject serializedObject = new SerializedObject(recognizer);
        SerializedProperty waitZone = serializedObject.FindProperty("waitZone");
        return waitZone?.objectReferenceValue as Collider;
    }
}
