using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class SetupCombatResultOverlay
{
    public static void Execute()
    {
        // Trouver le GO parent
        var overlay = GameObject.Find("CombatResultOverlay");
        if (overlay == null)
        {
            Debug.LogError("CombatResultOverlay introuvable !");
            return;
        }

        // Créer le Canvas enfant
        var canvasGO = new GameObject("ResultCanvas");
        canvasGO.transform.SetParent(overlay.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Background — Image noire transparente, stretch
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f); // transparent par défaut

        // ResultText — TMP centré
        var resultGO = new GameObject("ResultText");
        resultGO.transform.SetParent(canvasGO.transform, false);
        var resultRect = resultGO.AddComponent<RectTransform>();
        resultRect.anchorMin = new Vector2(0.5f, 0.5f);
        resultRect.anchorMax = new Vector2(0.5f, 0.5f);
        resultRect.anchoredPosition = new Vector2(0f, 40f);
        resultRect.sizeDelta = new Vector2(800f, 150f);
        var resultTmp = resultGO.AddComponent<TextMeshProUGUI>();
        resultTmp.text = "Victoire !";
        resultTmp.fontSize = 90;
        resultTmp.fontStyle = FontStyles.Bold;
        resultTmp.alignment = TextAlignmentOptions.Center;
        resultTmp.color = new Color(0.2f, 1f, 0.3f, 0f); // transparent par défaut

        // DamageText — TMP sous le résultat
        var dmgGO = new GameObject("DamageText");
        dmgGO.transform.SetParent(canvasGO.transform, false);
        var dmgRect = dmgGO.AddComponent<RectTransform>();
        dmgRect.anchorMin = new Vector2(0.5f, 0.5f);
        dmgRect.anchorMax = new Vector2(0.5f, 0.5f);
        dmgRect.anchoredPosition = new Vector2(0f, -40f);
        dmgRect.sizeDelta = new Vector2(600f, 80f);
        var dmgTmp = dmgGO.AddComponent<TextMeshProUGUI>();
        dmgTmp.text = "";
        dmgTmp.fontSize = 42;
        dmgTmp.alignment = TextAlignmentOptions.Center;
        dmgTmp.color = new Color(1f, 1f, 1f, 0f); // transparent par défaut

        // Désactiver le Canvas par défaut (le script l'active)
        canvasGO.SetActive(false);

        // Câbler les références sur le composant CombatResultOverlay
        var component = overlay.GetComponent<AutoBattler.Client.Combat.CombatResultOverlay>();
        if (component != null)
        {
            var so = new SerializedObject(component);
            so.FindProperty("resultCanvas").objectReferenceValue = canvas;
            so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
            so.FindProperty("resultText").objectReferenceValue = resultTmp;
            so.FindProperty("damageText").objectReferenceValue = dmgTmp;
            so.ApplyModifiedProperties();
            Debug.Log("[Setup] CombatResultOverlay câblé !");
        }

        EditorUtility.SetDirty(overlay);
        Debug.Log("[Setup] CombatResultOverlay créé avec succès !");
    }
}
