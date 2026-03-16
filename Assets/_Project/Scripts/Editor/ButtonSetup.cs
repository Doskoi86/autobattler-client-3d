using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ButtonSetup
{
    public static void Execute()
    {
        FixButtonScale("Button_Refresh");
        FixButtonScale("Button_Freeze");
        FixButtonScale("Button_Upgrade");
        FixButtonScale("Button_Ready");

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ButtonSetup] All button scales fixed and scene saved");
    }

    private static void FixButtonScale(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogError($"[ButtonSetup] {name} not found"); return; }

        // Root scale must be (1,1,1) — the scale was incorrectly set on root instead of Background
        go.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(go);

        // Background child should have the visual scale
        var bg = go.transform.Find("Background");
        if (bg != null)
        {
            bg.localScale = new Vector3(0.6f, 0.25f, 1f);
            EditorUtility.SetDirty(bg.gameObject);
        }

        // Label should be at local (0, 0, -0.01) with no extra scale
        var label = go.transform.Find("Label");
        if (label != null)
        {
            label.localPosition = new Vector3(0f, 0f, -0.01f);
            label.localScale = Vector3.one;
            EditorUtility.SetDirty(label.gameObject);
        }

        Debug.Log($"[ButtonSetup] {name}: root scale=(1,1,1), bg scale=(0.6,0.25,1)");
    }
}
