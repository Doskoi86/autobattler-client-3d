using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class DropZoneFixer
{
    public static void Execute()
    {
        // Trouver le parent BoardSurface (actif)
        var boardSurface = GameObject.Find("BoardSurface");
        if (boardSurface == null)
        {
            Debug.LogError("[DropZoneFixer] BoardSurface introuvable !");
            return;
        }

        // Chercher les enfants par nom (fonctionne même si inactifs)
        var boardZone = boardSurface.transform.Find("DropZoneIndicator_Board");
        var sellZone = boardSurface.transform.Find("DropZoneIndicator_Sell");

        if (boardZone != null)
        {
            boardZone.localPosition = new Vector3(0f, 0.02f, 2.26f);
            boardZone.localScale = new Vector3(1.5f, 1f, 0.4f);
            boardZone.gameObject.SetActive(false);
            EditorUtility.SetDirty(boardZone.gameObject);
            Debug.Log($"[DropZoneFixer] Board zone → pos {boardZone.localPosition}, scale {boardZone.localScale}");
        }
        else
        {
            Debug.LogError("[DropZoneFixer] DropZoneIndicator_Board introuvable !");
        }

        if (sellZone != null)
        {
            sellZone.localPosition = new Vector3(0f, 0.02f, 5.41f);
            sellZone.localScale = new Vector3(1.5f, 1f, 0.3f);
            sellZone.gameObject.SetActive(false);
            EditorUtility.SetDirty(sellZone.gameObject);
            Debug.Log($"[DropZoneFixer] Sell zone → pos {sellZone.localPosition}, scale {sellZone.localScale}");
        }
        else
        {
            Debug.LogError("[DropZoneFixer] DropZoneIndicator_Sell introuvable !");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[DropZoneFixer] Done — scène sauvegardée");
    }
}
