using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class DropZoneSetup
{
    public static void Execute()
    {
        // Réactiver les renderers pour qu'on puisse les voir en edit mode
        // Le script DropZone.Awake() les cachera au Play
        var board = GameObject.Find("DropZone_Board");
        var sell = GameObject.Find("DropZone_Sell");

        if (board != null)
        {
            var r = board.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = true;
            EditorUtility.SetDirty(board);
        }

        if (sell != null)
        {
            var r = sell.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = true;
            EditorUtility.SetDirty(sell);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[DropZoneSetup] Renderers réactivés — visibles en edit mode");
    }
}
