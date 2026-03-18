using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class OverlaySetup
{
    public static void Execute()
    {
        // Trouver ou créer le PhaseTransitionOverlay
        var go = GameObject.Find("PhaseTransitionOverlay");
        if (go == null)
        {
            go = new GameObject("PhaseTransitionOverlay");
        }

        // Nettoyer les anciens enfants SpriteRenderer (Background, PhaseText, TurnText)
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            var child = go.transform.GetChild(i);
            if (child.name != "BlurVolume") // Garder le BlurVolume
                Object.DestroyImmediate(child.gameObject);
        }

        // S'assurer que le composant existe
        var overlay = go.GetComponent<AutoBattler.Client.UI.PhaseTransitionOverlay>();
        if (overlay == null)
            overlay = go.AddComponent<AutoBattler.Client.UI.PhaseTransitionOverlay>();

        // Créer le Canvas (Screen Space Overlay)
        var canvasGo = new GameObject("OverlayCanvas");
        canvasGo.transform.SetParent(go.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>();
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Background Image (plein écran, noir transparent)
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Phase Text
        var phaseGo = new GameObject("PhaseText");
        phaseGo.transform.SetParent(canvasGo.transform, false);
        var phaseTmp = phaseGo.AddComponent<TextMeshProUGUI>();
        phaseTmp.text = "";
        phaseTmp.fontSize = 72;
        phaseTmp.alignment = TextAlignmentOptions.Center;
        phaseTmp.color = Color.white;
        phaseTmp.fontStyle = FontStyles.Bold;
        var phaseRect = phaseGo.GetComponent<RectTransform>();
        phaseRect.anchorMin = new Vector2(0.5f, 0.55f);
        phaseRect.anchorMax = new Vector2(0.5f, 0.55f);
        phaseRect.sizeDelta = new Vector2(800, 100);

        // Turn Text
        var turnGo = new GameObject("TurnText");
        turnGo.transform.SetParent(canvasGo.transform, false);
        var turnTmp = turnGo.AddComponent<TextMeshProUGUI>();
        turnTmp.text = "";
        turnTmp.fontSize = 36;
        turnTmp.alignment = TextAlignmentOptions.Center;
        turnTmp.color = Color.white;
        var turnRect = turnGo.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 0.42f);
        turnRect.anchorMax = new Vector2(0.5f, 0.42f);
        turnRect.sizeDelta = new Vector2(600, 60);

        // Câbler les références
        var so = new SerializedObject(overlay);
        so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        so.FindProperty("phaseText").objectReferenceValue = phaseTmp;
        so.FindProperty("turnText").objectReferenceValue = turnTmp;
        so.FindProperty("overlayCanvas").objectReferenceValue = canvas;

        // Chercher le BlurVolume existant
        var blurVol = go.transform.Find("BlurVolume");
        if (blurVol != null)
        {
            var vol = blurVol.GetComponent<UnityEngine.Rendering.Volume>();
            if (vol != null)
                so.FindProperty("blurVolume").objectReferenceValue = vol;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(go);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[OverlaySetup] Canvas overlay créé — blur net, texte net, plateau flou");
    }
}
