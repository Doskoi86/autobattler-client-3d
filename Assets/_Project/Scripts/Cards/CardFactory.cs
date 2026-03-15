using UnityEngine;
using TMPro;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Fabrique de cartes 3D. Crée les GameObjects de carte par code
    /// avec tous les sous-objets (frame, art, textes, glow, collider).
    ///
    /// À terme, remplacer par des Prefabs pour personnaliser les visuels.
    /// Pour l'instant, génère des placeholders colorés.
    /// </summary>
    public class CardFactory : MonoBehaviour
    {
        public static CardFactory Instance { get; private set; }

        [Header("Card Dimensions")]
        [SerializeField] private float cardWidth = 1.2f;
        [SerializeField] private float cardHeight = 1.6f;
        [SerializeField] private float cardDepth = 0.02f;

        [Header("Text Settings")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float statFontSize = 6f;
        [SerializeField] private float nameFontSize = 3.5f;
        [SerializeField] private float tierFontSize = 2.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Crée un GameObject de carte 3D complet à partir des données serveur.
        /// </summary>
        public CardVisual CreateCard(MinionState data)
        {
            // --- Root ---
            var root = new GameObject($"Card_{data.Name}");

            // BoxCollider pour les raycasts (hover + drag)
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(cardWidth, cardDepth, cardHeight);
            collider.center = Vector3.zero;

            // --- Card Frame (fond de la carte) ---
            var frame = CreateQuad("CardFrame", root.transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(cardWidth, cardHeight, 1f),
                CreateCardMaterial(Color.gray));

            // --- Card Art (zone illustration, légèrement devant) ---
            var art = CreateQuad("CardArt", root.transform,
                new Vector3(0f, cardDepth * 0.5f, cardHeight * 0.1f),
                new Vector3(cardWidth * 0.8f, cardHeight * 0.45f, 1f),
                CreateCardMaterial(new Color(0.3f, 0.25f, 0.2f)));

            // --- Glow (derrière la carte, invisible par défaut) ---
            var glow = CreateQuad("GlowQuad", root.transform,
                new Vector3(0f, -cardDepth, 0f),
                new Vector3(cardWidth * 1.15f, cardHeight * 1.1f, 1f),
                CreateGlowMaterial());

            // --- Textes ---
            var attackTmp = CreateText("AttackText", root.transform,
                new Vector3(-cardWidth * 0.35f, cardDepth, -cardHeight * 0.38f),
                statFontSize, Color.yellow, "0", TextAlignmentOptions.Center);

            var healthTmp = CreateText("HealthText", root.transform,
                new Vector3(cardWidth * 0.35f, cardDepth, -cardHeight * 0.38f),
                statFontSize, Color.red, "0", TextAlignmentOptions.Center);

            var tierTmp = CreateText("TierText", root.transform,
                new Vector3(0f, cardDepth, cardHeight * 0.38f),
                tierFontSize, Color.white, "T1", TextAlignmentOptions.Center);

            var nameTmp = CreateText("NameText", root.transform,
                new Vector3(0f, cardDepth, -cardHeight * 0.15f),
                nameFontSize, Color.white, "Name", TextAlignmentOptions.Center);

            // --- CardVisual component ---
            var visual = root.AddComponent<CardVisual>();

            // Assigner les références via SerializeField (reflection pour le setup initial)
            AssignField(visual, "cardFrame", frame.GetComponent<MeshRenderer>());
            AssignField(visual, "glowQuad", glow.GetComponent<MeshRenderer>());
            AssignField(visual, "attackText", attackTmp);
            AssignField(visual, "healthText", healthTmp);
            AssignField(visual, "tierText", tierTmp);
            AssignField(visual, "nameText", nameTmp);

            // Orienter la carte face à la caméra (à plat sur le board, face vers le haut)
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Appliquer les données
            visual.SetData(data);

            return visual;
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private GameObject CreateQuad(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localScale = scale;
            quad.transform.localRotation = Quaternion.identity;

            // Retirer le collider du quad (le root a déjà un BoxCollider)
            var meshCollider = quad.GetComponent<MeshCollider>();
            if (meshCollider != null) Destroy(meshCollider);

            quad.GetComponent<MeshRenderer>().material = mat;
            return quad;
        }

        private TextMeshPro CreateText(string name, Transform parent,
            Vector3 localPos, float fontSize, Color color, string defaultText,
            TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.identity;

            var tmp = obj.AddComponent<TextMeshPro>();
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = false;

            // Taille du rect pour que le texte ne soit pas tronqué
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(cardWidth * 0.9f, fontSize * 0.15f);

            if (font != null) tmp.font = font;

            return tmp;
        }

        private Material CreateCardMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            return mat;
        }

        private Material CreateGlowMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            // Configurer pour la transparence
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.color = new Color(1f, 1f, 1f, 0f); // Invisible par défaut
            return mat;
        }

        /// <summary>
        /// Assigne un champ [SerializeField] private par reflection.
        /// Utilisé uniquement pour le setup initial par code.
        /// </summary>
        private void AssignField<T>(CardVisual visual, string fieldName, T value)
        {
            var field = typeof(CardVisual).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(visual, value);
            else
                Debug.LogWarning($"[CardFactory] Champ '{fieldName}' non trouvé sur CardVisual");
        }
    }
}
