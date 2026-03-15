using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace AutoBattler.Client.UI.HUD
{
    /// <summary>
    /// Construit le Canvas HUD par code au démarrage.
    /// Crée les textes (or, tier, timer, phase) et les boutons
    /// (reroll, freeze, level up, ready) puis les assigne au GameHUD.
    ///
    /// Ce builder évite de devoir configurer manuellement chaque élément UI
    /// dans l'éditeur Unity. À remplacer par un prefab quand le design sera finalisé.
    /// </summary>
    public class HUDBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameHUD gameHUD;

        private void Awake()
        {
            if (gameHUD == null)
                gameHUD = GetComponent<GameHUD>();

            BuildHUD();
        }

        private void BuildHUD()
        {
            // --- Canvas ---
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // EventSystem requis pour que les clics UI fonctionnent
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }

            // --- Barre du haut : Phase + Timer ---
            var topBar = CreatePanel("TopBar", canvas.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -10), new Vector2(600, 60),
                new Color(0, 0, 0, 0.6f));

            var phaseText = CreateText("PhaseText", topBar.transform,
                new Vector2(0, 0), new Vector2(0.6f, 1f),
                "Recrutement — Tour 1", 24, TextAlignmentOptions.MidlineLeft);

            var timerText = CreateText("TimerText", topBar.transform,
                new Vector2(0.6f, 0), new Vector2(1f, 1f),
                "45s", 32, TextAlignmentOptions.MidlineRight, Color.white);

            // --- Panneau gauche : Tier + Upgrade ---
            var tierPanel = CreatePanel("TierPanel", canvas.transform,
                new Vector2(0, 1f), new Vector2(0, 1f),
                new Vector2(10, -80), new Vector2(220, 80),
                new Color(0, 0, 0, 0.6f));

            var tierText = CreateText("TierText", tierPanel.transform,
                new Vector2(0, 0.5f), new Vector2(1f, 1f),
                "Tier 1", 28, TextAlignmentOptions.Center, new Color(0.3f, 0.7f, 1f));

            var upgradeCostText = CreateText("UpgradeCostText", tierPanel.transform,
                new Vector2(0, 0), new Vector2(1f, 0.5f),
                "Upgrade: 5g", 18, TextAlignmentOptions.Center, Color.yellow);

            // --- Panneau droit : Or ---
            var goldPanel = CreatePanel("GoldPanel", canvas.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10, -80), new Vector2(160, 60),
                new Color(0, 0, 0, 0.6f));

            var goldIcon = CreateText("GoldIcon", goldPanel.transform,
                new Vector2(0, 0), new Vector2(0.3f, 1f),
                "G", 28, TextAlignmentOptions.Center, Color.yellow);

            var goldText = CreateText("GoldText", goldPanel.transform,
                new Vector2(0.3f, 0), new Vector2(1f, 1f),
                "3", 36, TextAlignmentOptions.Center, Color.yellow);

            // --- Boutons en bas ---
            var buttonBar = CreatePanel("ButtonBar", canvas.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 10), new Vector2(800, 60),
                new Color(0, 0, 0, 0.4f));

            var rerollBtn = CreateButton("RerollButton", buttonBar.transform,
                new Vector2(0, 0), new Vector2(0.25f, 1f),
                "Reroll (1g)", new Color(0.2f, 0.5f, 0.8f));

            var freezeBtn = CreateButton("FreezeButton", buttonBar.transform,
                new Vector2(0.25f, 0), new Vector2(0.5f, 1f),
                "Freeze", new Color(0.3f, 0.7f, 0.9f));

            var upgradeBtn = CreateButton("UpgradeButton", buttonBar.transform,
                new Vector2(0.5f, 0), new Vector2(0.75f, 1f),
                "Level Up (5g)", new Color(0.7f, 0.5f, 0.2f));

            var readyBtn = CreateButton("ReadyButton", buttonBar.transform,
                new Vector2(0.75f, 0), new Vector2(1f, 1f),
                "READY!", new Color(0.2f, 0.7f, 0.3f));

            // --- Assigner au GameHUD via reflection ---
            AssignField(gameHUD, "goldText", goldText);
            AssignField(gameHUD, "tierText", tierText);
            AssignField(gameHUD, "timerText", timerText);
            AssignField(gameHUD, "phaseText", phaseText);
            AssignField(gameHUD, "upgradeCostText", upgradeCostText);
            AssignField(gameHUD, "rerollButton", rerollBtn.GetComponent<Button>());
            AssignField(gameHUD, "freezeButton", freezeBtn.GetComponent<Button>());
            AssignField(gameHUD, "upgradeButton", upgradeBtn.GetComponent<Button>());
            AssignField(gameHUD, "readyButton", readyBtn.GetComponent<Button>());
            AssignField(gameHUD, "rerollButtonText", rerollBtn.GetComponentInChildren<TextMeshProUGUI>());
            AssignField(gameHUD, "freezeButtonText", freezeBtn.GetComponentInChildren<TextMeshProUGUI>());
            AssignField(gameHUD, "upgradeButtonText", upgradeBtn.GetComponentInChildren<TextMeshProUGUI>());
        }

        // =====================================================
        // HELPERS UI
        // =====================================================

        private GameObject CreatePanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = obj.GetComponent<Image>();
            image.color = color;

            return obj;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            string defaultText, float fontSize, TextAlignmentOptions alignment,
            Color? color = null)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(10, 5);
            rect.offsetMax = new Vector2(-10, -5);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.color = color ?? Color.white;
            tmp.alignment = alignment;

            return tmp;
        }

        private GameObject CreateButton(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, string label, Color buttonColor)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4, 4);
            rect.offsetMax = new Vector2(-4, -4);

            var image = obj.GetComponent<Image>();
            image.color = buttonColor;

            // Configurer les couleurs du bouton
            var button = obj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonColor * 1.2f;
            colors.pressedColor = buttonColor * 0.8f;
            button.colors = colors;

            // Texte du bouton
            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(obj.transform, false);

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return obj;
        }

        private void AssignField<T>(GameHUD hud, string fieldName, T value)
        {
            var field = typeof(GameHUD).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(hud, value);
        }
    }
}
