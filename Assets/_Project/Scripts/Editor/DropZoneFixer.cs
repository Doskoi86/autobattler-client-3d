using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class DropZoneFixer
{
    public static void Execute()
    {
        // === BoardManager ===
        var bm = GameObject.Find("BoardManager");
        if (bm != null)
        {
            var so = new SerializedObject(bm.GetComponent<AutoBattler.Client.Board.BoardManager>());
            SetFloat(so, "slotSpacing", 2.8f);
            SetFloat(so, "arcAmount", 0f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bm);
            Debug.Log("[Audit Fix] BoardManager: slotSpacing=2.8, arcAmount=0");
        }

        // === DragDropController ===
        var ddc = GameObject.Find("DragDropController");
        if (ddc != null)
        {
            var so = new SerializedObject(ddc.GetComponent<AutoBattler.Client.Board.DragDropController>());
            SetFloat(so, "sellThresholdZFallback", 3.9f);
            SetFloat(so, "shopBuyThresholdZFallback", 4f);
            SetFloat(so, "dragSortingOffset", 100f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ddc);
            Debug.Log("[Audit Fix] DragDropController: thresholds set");
        }

        // === CardFactory ===
        var cf = GameObject.Find("CardFactory");
        if (cf != null)
        {
            var so = new SerializedObject(cf.GetComponent<AutoBattler.Client.Cards.CardFactory>());
            SetFloat(so, "tokenScale", 1f);
            SetFloat(so, "cardScale", 1f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cf);
            Debug.Log("[Audit Fix] CardFactory: tokenScale=1, cardScale=1");
        }

        // === ShopManager ===
        var sm = GameObject.Find("ShopManager");
        if (sm != null)
        {
            var so = new SerializedObject(sm.GetComponent<AutoBattler.Client.Shop.ShopManager>());
            SetFloat(so, "shopSpacing", 2.5f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sm);
            Debug.Log("[Audit Fix] ShopManager: shopSpacing=2.5");
        }

        // === HandManager ===
        var hm = GameObject.Find("HandManager");
        if (hm != null)
        {
            var so = new SerializedObject(hm.GetComponent<AutoBattler.Client.Hand.HandManager>());
            SetFloat(so, "handSpacing", 1.1f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hm);
            Debug.Log("[Audit Fix] HandManager: handSpacing=1.1");
        }

        // === Drop Zones ===
        var boardSurface = GameObject.Find("BoardSurface");
        if (boardSurface != null)
        {
            var boardZone = boardSurface.transform.Find("DropZoneIndicator_Board");
            var sellZone = boardSurface.transform.Find("DropZoneIndicator_Sell");

            if (boardZone != null)
            {
                boardZone.localPosition = new Vector3(0f, 0.02f, 2.26f);
                boardZone.localScale = new Vector3(1.5f, 1f, 0.4f);
                boardZone.gameObject.SetActive(false);
                EditorUtility.SetDirty(boardZone.gameObject);
            }

            if (sellZone != null)
            {
                sellZone.localPosition = new Vector3(0f, 0.02f, 5.41f);
                sellZone.localScale = new Vector3(1.5f, 1f, 0.3f);
                sellZone.gameObject.SetActive(false);
                EditorUtility.SetDirty(sellZone.gameObject);
            }
        }

        // === Save ===
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Audit Fix] ALL DONE — Scene saved");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.floatValue = value;
        else Debug.LogWarning($"[Audit Fix] Property '{name}' not found");
    }
}
