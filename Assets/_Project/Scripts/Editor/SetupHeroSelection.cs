using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;

public class SetupHeroSelection
{
    public static void Execute()
    {
        // =====================================================
        // 1. CRÉER LE PREFAB HERO CARD
        // =====================================================
        var cardPrefab = CreateHeroCardPrefab();

        // =====================================================
        // 2. CRÉER LE HERO SELECTION SCREEN DANS LA SCÈNE
        // =====================================================
        var screenGO = GameObject.Find("HeroSelectionScreen");
        if (screenGO == null)
        {
            screenGO = new GameObject("HeroSelectionScreen");
        }

        // Ajouter le composant
        var screen = screenGO.GetComponent<AutoBattler.Client.UI.HeroSelectionScreen>();
        if (screen == null)
            screen = screenGO.AddComponent<AutoBattler.Client.UI.HeroSelectionScreen>();

        // Créer le Canvas enfant
        var existingCanvas = screenGO.transform.Find("SelectionCanvas");
        if (existingCanvas != null)
            Object.DestroyImmediate(existingCanvas.gameObject);

        var canvasGO = new GameObject("SelectionCanvas");
        canvasGO.transform.SetParent(screenGO.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // --- Background ---
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0f);

        // --- Title ---
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -100f);
        titleRect.sizeDelta = new Vector2(800f, 80f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Choisissez votre héros";
        titleTmp.fontSize = 56;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.85f, 0.3f, 0f);

        // --- Timer ---
        var timerGO = new GameObject("TimerText");
        timerGO.transform.SetParent(canvasGO.transform, false);
        var timerRect = timerGO.AddComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.5f, 1f);
        timerRect.anchorMax = new Vector2(0.5f, 1f);
        timerRect.anchoredPosition = new Vector2(0f, -170f);
        timerRect.sizeDelta = new Vector2(200f, 60f);
        var timerTmp = timerGO.AddComponent<TextMeshProUGUI>();
        timerTmp.text = "30";
        timerTmp.fontSize = 40;
        timerTmp.alignment = TextAlignmentOptions.Center;
        timerTmp.color = new Color(1f, 1f, 1f, 0f);

        // --- Card Container ---
        var containerGO = new GameObject("CardContainer");
        containerGO.transform.SetParent(canvasGO.transform, false);
        var containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, -20f);
        containerRect.sizeDelta = new Vector2(1600f, 500f);

        var hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Désactiver le Canvas par défaut
        canvasGO.SetActive(false);

        // =====================================================
        // 3. CHERCHER OU CRÉER LE BLUR VOLUME
        // =====================================================
        // On réutilise le BlurVolume de PhaseTransitionOverlay
        var phaseOverlay = GameObject.Find("PhaseTransitionOverlay");
        Volume blurVol = null;
        if (phaseOverlay != null)
        {
            var blurGO = phaseOverlay.transform.Find("BlurVolume");
            if (blurGO != null) blurVol = blurGO.GetComponent<Volume>();
        }

        // =====================================================
        // 4. CÂBLER LES RÉFÉRENCES
        // =====================================================
        var so = new SerializedObject(screen);
        so.FindProperty("selectionCanvas").objectReferenceValue = canvas;
        so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        so.FindProperty("titleText").objectReferenceValue = titleTmp;
        so.FindProperty("timerText").objectReferenceValue = timerTmp;
        so.FindProperty("cardContainer").objectReferenceValue = containerRect;
        if (blurVol != null)
            so.FindProperty("blurVolume").objectReferenceValue = blurVol;

        // Sauvegarder le prefab et l'assigner
        string prefabPath = "Assets/_Project/Prefabs/HeroCard.prefab";
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(cardPrefab, prefabPath);
        Object.DestroyImmediate(cardPrefab);
        so.FindProperty("heroCardPrefab").objectReferenceValue = savedPrefab;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(screenGO);

        Debug.Log("[Setup] HeroSelectionScreen créé et câblé !");
        Debug.Log($"[Setup] HeroCard prefab sauvé : {prefabPath}");
    }

    private static GameObject CreateHeroCardPrefab()
    {
        // Root — carte de héros
        var cardGO = new GameObject("HeroCard");
        var cardRect = cardGO.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(340f, 440f);

        // Background / frame
        var cardImage = cardGO.AddComponent<Image>();
        cardImage.color = new Color(0.15f, 0.12f, 0.1f, 1f);

        // Rendre cliquable
        var button = cardGO.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
        button.colors = colors;
        button.transition = Selectable.Transition.None; // On gère le hover nous-mêmes

        // Outline pour le cadre
        var outline = cardGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.55f, 0.2f, 0.8f);
        outline.effectDistance = new Vector2(3f, 3f);

        // --- Portrait placeholder (couleur unie, sera remplacé par l'art) ---
        var portraitGO = new GameObject("HeroPortrait");
        portraitGO.transform.SetParent(cardGO.transform, false);
        var portraitRect = portraitGO.AddComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.1f, 0.5f);
        portraitRect.anchorMax = new Vector2(0.9f, 0.95f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.color = new Color(0.25f, 0.2f, 0.3f, 1f);

        // --- Hero Name ---
        var nameGO = new GameObject("HeroName");
        nameGO.transform.SetParent(cardGO.transform, false);
        var nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.35f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.offsetMin = new Vector2(10f, 0f);
        nameRect.offsetMax = new Vector2(-10f, 0f);
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text = "Nom du Héros";
        nameTmp.fontSize = 26;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.color = new Color(1f, 0.9f, 0.6f, 1f);
        nameTmp.enableWordWrapping = true;

        // --- Hero Description ---
        var descGO = new GameObject("HeroDescription");
        descGO.transform.SetParent(cardGO.transform, false);
        var descRect = descGO.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0.05f);
        descRect.anchorMax = new Vector2(1f, 0.35f);
        descRect.offsetMin = new Vector2(15f, 0f);
        descRect.offsetMax = new Vector2(-15f, 0f);
        var descTmp = descGO.AddComponent<TextMeshProUGUI>();
        descTmp.text = "Description du pouvoir héroïque";
        descTmp.fontSize = 18;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        descTmp.enableWordWrapping = true;

        // --- Tribe Label (petit texte en bas) ---
        var tribeGO = new GameObject("TribeLabel");
        tribeGO.transform.SetParent(cardGO.transform, false);
        var tribeRect = tribeGO.AddComponent<RectTransform>();
        tribeRect.anchorMin = new Vector2(0f, 0f);
        tribeRect.anchorMax = new Vector2(1f, 0.08f);
        tribeRect.offsetMin = Vector2.zero;
        tribeRect.offsetMax = Vector2.zero;
        var tribeTmp = tribeGO.AddComponent<TextMeshProUGUI>();
        tribeTmp.text = "";
        tribeTmp.fontSize = 14;
        tribeTmp.alignment = TextAlignmentOptions.Center;
        tribeTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        return cardGO;
    }
}
