using UnityEngine;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Une entrée du panel héros gauche.
    /// Affiche : nom raccourci, PV, tier, barre de vie, indicateur éliminé.
    ///
    /// 📋 CRÉER LE PREFAB PlayerListEntry :
    /// 1. Hierarchy → Create Empty → renommer "PlayerListEntry"
    /// 2. Rotation (90, 0, 0) pour être à plat
    /// 3. Add Component → PlayerListEntry
    ///
    /// 4. Enfants :
    ///    "Background" → SpriteRenderer (carteFondBlanc, sortingOrder 10, scale 0.4/0.15/1)
    ///    "HealthBar"  → SpriteRenderer (carteFondBlanc, sortingOrder 11, color vert, scale variable)
    ///    "NameText"   → TextMeshPro 3D (fontSize 2, align left, pos local)
    ///    "HealthText" → TextMeshPro 3D (fontSize 2, align right, pos local)
    ///    "TierText"   → TextMeshPro 3D (fontSize 1.5, align center, pos local)
    ///
    /// 5. Câbler les références dans l'Inspector
    /// 6. Glisser → Assets/_Project/Prefabs/PlayerListEntry.prefab
    /// </summary>
    public class PlayerListEntry : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer healthBarRenderer;
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro tierText;

        [Header("Colors")]
        [SerializeField] private Color normalBgColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        [SerializeField] private Color localPlayerBgColor = new Color(0.15f, 0.25f, 0.4f, 0.9f);
        [SerializeField] private Color eliminatedBgColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        [SerializeField] private Color healthBarColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color healthBarLowColor = new Color(0.8f, 0.2f, 0.2f, 1f);

        [Header("Health Bar")]
        [SerializeField] private float maxHealthBarWidth = 0.35f;
        [SerializeField] private int maxHealth = 30;
        [SerializeField] private int lowHealthThreshold = 10;

        [Header("Animation")]
        [SerializeField] private float updateDuration = 0.3f;

        private MaterialPropertyBlock _bgMPB;
        private MaterialPropertyBlock _barMPB;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _bgMPB = new MaterialPropertyBlock();
            _barMPB = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Met à jour l'entrée avec les données d'un joueur.
        /// </summary>
        public void UpdateData(PlayerPublicState data, bool isLocalPlayer)
        {
            // Nom (raccourci)
            if (nameText != null)
            {
                string displayName = data.PlayerId;
                if (displayName.Length > 10)
                    displayName = displayName.Substring(0, 8) + "..";
                nameText.text = displayName;
            }

            // PV
            if (healthText != null)
                healthText.text = $"{data.Health}";

            // Tier
            if (tierText != null)
                tierText.text = $"T{data.TavernTier}";

            // Couleur de fond
            Color bgColor = data.IsEliminated ? eliminatedBgColor
                          : isLocalPlayer ? localPlayerBgColor
                          : normalBgColor;
            SetSpriteColor(backgroundRenderer, _bgMPB, bgColor);

            // Barre de vie
            if (healthBarRenderer != null)
            {
                float healthRatio = Mathf.Clamp01((float)data.Health / maxHealth);
                Color barColor = data.Health <= lowHealthThreshold ? healthBarLowColor : healthBarColor;
                SetSpriteColor(healthBarRenderer, _barMPB, barColor);

                // Animer la largeur de la barre
                var targetScale = new Vector3(maxHealthBarWidth * healthRatio, healthBarRenderer.transform.localScale.y, 1f);
                healthBarRenderer.transform.DOKill();
                healthBarRenderer.transform.DOScale(targetScale, updateDuration).SetEase(Ease.OutQuad);
            }

            // Opacité si éliminé
            if (data.IsEliminated)
            {
                if (nameText != null) nameText.alpha = 0.5f;
                if (healthText != null) healthText.alpha = 0.5f;
                if (tierText != null) tierText.alpha = 0.5f;
            }
            else
            {
                if (nameText != null) nameText.alpha = 1f;
                if (healthText != null) healthText.alpha = 1f;
                if (tierText != null) tierText.alpha = 1f;
            }
        }

        private void SetSpriteColor(SpriteRenderer sr, MaterialPropertyBlock mpb, Color color)
        {
            if (sr == null) return;
            sr.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, color);
            sr.SetPropertyBlock(mpb);
        }

        private void OnDestroy()
        {
            if (healthBarRenderer != null)
                healthBarRenderer.transform.DOKill();
        }
    }
}
