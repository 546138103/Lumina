using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WaveGreetingTaskSceneInstaller
{
    private const string ControllerObjectName = "WaveGreetingTaskController";

    [MenuItem("Lumina/Level2/Configure Selected Wave Greeting Zones")]
    public static void ConfigureSelectedZones()
    {
        GameObject actionArea = Selection.activeGameObject;
        GameObject taskArea = Selection.gameObjects.FirstOrDefault(item => item != actionArea);
        if (taskArea == null || actionArea == null || Selection.gameObjects.Length != 2)
        {
            EditorUtility.DisplayDialog(
                "Wave Greeting Task",
                "请选择两个对象：先选大圈，再按住 Ctrl 选小圈，最后选中的小圈必须是活动对象。",
                "确定");
            return;
        }

        if (!TryGetTrigger(taskArea, out _) || !TryGetTrigger(actionArea, out _))
        {
            EditorUtility.DisplayDialog(
                "Wave Greeting Task",
                "大圈和小圈都需要 Collider，并且必须勾选 Is Trigger。",
                "确定");
            return;
        }

        WaveGreetingTaskController controller =
            Object.FindObjectOfType<WaveGreetingTaskController>();
        if (controller == null)
        {
            GameObject controllerObject = new GameObject(ControllerObjectName);
            Undo.RegisterCreatedObjectUndo(controllerObject, "Create Wave Greeting Task");
            controller = controllerObject.AddComponent<WaveGreetingTaskController>();
        }

        ConfigureZone(taskArea, TaskZoneRole.TaskArea, controller);
        ConfigureZone(actionArea, TaskZoneRole.ActionArea, controller);
        ConfigureController(controller);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeGameObject = controller.gameObject;

        Debug.Log(
            $"[WaveGreetingTaskInstaller] 已接线：大圈={taskArea.name}，小圈={actionArea.name}。" +
            "UI 事件仍需按项目资源在 Inspector 中配置。",
            controller);
    }

    private static void ConfigureZone(
        GameObject target,
        TaskZoneRole role,
        WaveGreetingTaskController controller)
    {
        TaskZone zone = target.GetComponent<TaskZone>();
        if (zone == null)
        {
            zone = Undo.AddComponent<TaskZone>(target);
        }

        SerializedObject serializedZone = new SerializedObject(zone);
        serializedZone.FindProperty("role").enumValueIndex = (int)role;
        serializedZone.FindProperty("taskController").objectReferenceValue = controller;
        serializedZone.ApplyModifiedProperties();
        EditorUtility.SetDirty(zone);
    }

    private static void ConfigureController(WaveGreetingTaskController controller)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("modeManager").objectReferenceValue =
            Object.FindObjectOfType<PoseControlModeManager>();
        serializedController.FindProperty("actionRecognizer").objectReferenceValue =
            Object.FindObjectOfType<PoseSocialActionRecognizer>();
        serializedController.FindProperty("presetAnimator").objectReferenceValue =
            Object.FindObjectOfType<PosePresetSocialAnimator>();
        serializedController.ApplyModifiedProperties();
    }

    private static bool TryGetTrigger(GameObject target, out Collider trigger)
    {
        trigger = target.GetComponent<Collider>();
        return trigger != null && trigger.isTrigger;
    }
}
