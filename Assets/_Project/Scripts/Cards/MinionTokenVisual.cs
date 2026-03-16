using UnityEngine;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Rendu compact d'un minion (token portrait) pour le shop, le board et le combat.
    /// Différent de CardVisual qui est une carte complète (main du joueur).
    ///
    /// Le token affiche : art, cadre, ATK/HP, tier (shop only), auras (board only).
    /// Pas de nom, pas de description, pas de race — ces infos sont sur le tooltip/hover.
    ///
    /// 📋 CRÉER LE PREFAB MinionToken :
    ///
    /// ÉTAPE A — Root
    /// 1. Hierarchy → Create Empty → renommer "MinionToken"
    /// 2. Position (0, 0.1, 0), Rotation (90, 0, 0)
    /// 3. Add Component → MinionTokenVisual
    /// 4. Add Component → Box Collider → Size (1.0, 1.0, 0.02)
    ///
    /// ÉTAPE B — Enfant "Visual" (conteneur pour animer sans affecter le collider)
    /// 1. Clic droit sur MinionToken → Create Empty → renommer "Visual"
    ///
    /// ÉTAPE C — Couches de sprites (enfants de Visual)
    ///   "HighlightGlow"  — SpriteRenderer — Sprite: carteFondBlanc — Sorting Order: -1 — Scale (1.15, 1.15, 1) — Color (1,1,1,0)
    ///   "Art"            — SpriteRenderer — Sprite: (vide, assigné au runtime) — Sorting Order: 1 — Scale (0.65, 0.65, 1)
    ///   "Frame"          — SpriteRenderer — Sprite: wooden_frame — Sorting Order: 2
    ///   "TierBadge"      — SpriteRenderer — Sprite: carteRondNiveau — Sorting Order: 3 — Pos (-0.35, 0.35, 0) — Scale (0.5, 0.5, 1)
    ///     └── "TierText" — TextMeshPro 3D — FontSize 3 — Color blanc — Pos (0, 0, -0.01)
    ///   "AttackBadge"    — SpriteRenderer — Sprite: attack_badge — Sorting Order: 3 — Pos (-0.38, -0.38, 0) — Scale (0.6, 0.6, 1)
    ///     └── "AttackText" — TextMeshPro 3D — FontSize 3.5 — Color blanc — Pos (0, 0, -0.01)
    ///   "HealthBadge"    — SpriteRenderer — Sprite: health_badge — Sorting Order: 3 — Pos (0.38, -0.38, 0) — Scale (0.6, 0.6, 1)
    ///     └── "HealthText" — TextMeshPro 3D — FontSize 3.5 — Color blanc — Pos (0, 0, -0.01)
    ///   "FrostOverlay"   — SpriteRenderer — Sprite: carteFondBlanc — Sorting Order: 4 — Color (0.6, 0.85, 1.0, 0.45) — Désactivé
    ///
    /// ÉTAPE D — Enfant "AuraContainer" (enfant de MinionToken, pas de Visual)
    ///   "TauntShield"       — SpriteRenderer — Sorting Order: -1 — Désactivé — Scale (1.3, 1.3, 1)
    ///   "DivineShieldBubble" — SpriteRenderer — Sorting Order: 5 — Désactivé — Scale (1.2, 1.2, 1)
    ///
    /// ÉTAPE E — Assigner les références dans MinionTokenVisual Inspector
    /// ÉTAPE F — Glisser MinionToken → Assets/_Project/Prefabs/ pour créer le prefab
    /// </summary>
    public class MinionTokenVisual : MonoBehaviour, IDraggable
    {
        [Header("Sprite References")]
        [SerializeField] private SpriteRenderer artRenderer;
        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer highlightGlowRenderer;
        [SerializeField] private SpriteRenderer frostOverlayRenderer;

        [Header("Badges")]
        [SerializeField] private SpriteRenderer attackBadgeRenderer;
        [SerializeField] private SpriteRenderer healthBadgeRenderer;
        [SerializeField] private SpriteRenderer tierBadgeRenderer;

        [Header("Texts")]
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro tierText;

        [Header("Auras")]
        [SerializeField] private GameObject tauntShield;
        [SerializeField] private GameObject divineShieldBubble;

        [Header("Frame Variants")]
        [SerializeField] private Sprite normalFrame;
        [SerializeField] private Sprite goldenFrame;

        [Header("Visual Container")]
        [Tooltip("Le conteneur Visual — pour animer le visuel sans affecter le collider")]
        [SerializeField] private Transform visualContainer;

        [Header("Hover Settings")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverDuration = 0.12f;

        [Header("Glow Colors")]
        [SerializeField] private Color normalGlowColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color hoverGlowColor = new Color(0.4f, 1f, 0.6f, 0.5f);
        [SerializeField] private Color goldenGlowColor = new Color(1f, 0.85f, 0.2f, 0.6f);

        // État
        private MinionState _data;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private bool _isHovered;
        private bool _showTier = true;
        private Camera _mainCamera;

        // MaterialPropertyBlock
        private MaterialPropertyBlock _glowMPB;
        private MaterialPropertyBlock _frostMPB;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // IDraggable
        public bool DragEnabled { get; set; } = true;
        public bool CanDrag => DragEnabled;
        public string MinionInstanceId => _data?.InstanceId;
        public MinionState Data => _data;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _baseScale = transform.localScale;
            _glowMPB = new MaterialPropertyBlock();
            _frostMPB = new MaterialPropertyBlock();

            if (highlightGlowRenderer != null)
                SetSpriteColor(highlightGlowRenderer, _glowMPB, normalGlowColor);

            // Auras off par défaut
            if (tauntShield != null) tauntShield.SetActive(false);
            if (divineShieldBubble != null) divineShieldBubble.SetActive(false);
            if (frostOverlayRenderer != null) frostOverlayRenderer.gameObject.SetActive(false);
        }

        // =====================================================
        // DONNÉES
        // =====================================================

        /// <summary>
        /// Initialise le token avec les données serveur.
        /// </summary>
        public void Init(MinionState data, bool showTier = true)
        {
            _data = data;
            _showTier = showTier;

            // Stats
            if (attackText != null) attackText.text = data.Attack.ToString();
            if (healthText != null) healthText.text = data.Health.ToString();

            // Tier (visible au shop, pas sur le board)
            if (tierBadgeRenderer != null)
                tierBadgeRenderer.gameObject.SetActive(showTier);
            if (tierText != null) tierText.text = data.Tier.ToString();

            // Cadre doré si golden
            if (frameRenderer != null)
            {
                if (data.IsGolden && goldenFrame != null)
                    frameRenderer.sprite = goldenFrame;
                else if (normalFrame != null)
                    frameRenderer.sprite = normalFrame;
            }

            // Glow doré permanent si golden
            if (data.IsGolden && highlightGlowRenderer != null)
                SetSpriteColor(highlightGlowRenderer, _glowMPB, goldenGlowColor);

            // Auras selon keywords
            UpdateKeywordAuras(data.Keywords);
        }

        /// <summary>
        /// Met à jour uniquement les stats (combat, buff).
        /// </summary>
        public void UpdateStats(int attack, int health)
        {
            if (_data != null)
            {
                _data.Attack = attack;
                _data.Health = health;
            }
            if (attackText != null) attackText.text = attack.ToString();
            if (healthText != null) healthText.text = health.ToString();
        }

        /// <summary>
        /// Met à jour les auras visuelles selon les keywords actifs.
        /// </summary>
        public void UpdateKeywordAuras(string keywords)
        {
            if (string.IsNullOrEmpty(keywords))
            {
                if (tauntShield != null) tauntShield.SetActive(false);
                if (divineShieldBubble != null) divineShieldBubble.SetActive(false);
                return;
            }

            if (tauntShield != null)
                tauntShield.SetActive(keywords.Contains("Taunt"));
            if (divineShieldBubble != null)
                divineShieldBubble.SetActive(keywords.Contains("DivineShield"));
        }

        /// <summary>
        /// Active/désactive l'overlay de givre (shop frozen).
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            if (frostOverlayRenderer != null)
                frostOverlayRenderer.gameObject.SetActive(frozen);
        }

        /// <summary>
        /// Active/désactive l'affichage du tier (shop = on, board = off).
        /// </summary>
        public void SetShowTier(bool show)
        {
            _showTier = show;
            if (tierBadgeRenderer != null)
                tierBadgeRenderer.gameObject.SetActive(show);
        }

        public void SetBasePosition(Vector3 position)
        {
            _basePosition = position;
        }

        // =====================================================
        // HOVER
        // =====================================================

        private void OnMouseEnter()
        {
            if (!DragEnabled) return;
            _isHovered = true;
            AnimateHover(true);
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            AnimateHover(false);
        }

        private Tween AnimateHover(bool hovering)
        {
            if (visualContainer == null) return null;
            visualContainer.DOKill();

            if (hovering)
            {
                if (highlightGlowRenderer != null && (_data == null || !_data.IsGolden))
                    AnimateSpriteColor(highlightGlowRenderer, _glowMPB, hoverGlowColor, hoverDuration);

                return visualContainer.DOScale(Vector3.one * hoverScale, hoverDuration).SetEase(Ease.OutBack);
            }
            else
            {
                if (highlightGlowRenderer != null && (_data == null || !_data.IsGolden))
                    AnimateSpriteColor(highlightGlowRenderer, _glowMPB, normalGlowColor, hoverDuration);

                return visualContainer.DOScale(Vector3.one, hoverDuration).SetEase(Ease.OutQuad);
            }
        }

        // =====================================================
        // ANIMATIONS
        // =====================================================

        public Tween AnimateSpawn(float delay = 0f)
        {
            transform.localScale = Vector3.zero;
            return DOTween.Sequence()
                .AppendInterval(delay)
                .Append(transform.DOScale(_baseScale, 0.3f).SetEase(Ease.OutBack));
        }

        public Tween AnimateDeath()
        {
            return DOTween.Sequence()
                .Append(transform.DOScale(0f, 0.3f).SetEase(Ease.InBack))
                .OnComplete(() => gameObject.SetActive(false));
        }

        public Tween AnimateDamage(int newHealth)
        {
            return DOTween.Sequence()
                .Append(transform.DOShakePosition(0.2f, 0.08f, 20))
                .AppendCallback(() => UpdateStats(_data?.Attack ?? 0, newHealth))
                .Append(healthText.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f).SetEase(Ease.OutElastic));
        }

        public Tween AnimateAttack(Vector3 targetPosition)
        {
            var startPos = transform.position;
            return DOTween.Sequence()
                .Append(transform.DOScale(_baseScale * 1.15f, 0.1f).SetEase(Ease.OutBack))
                .Append(transform.DOMove(targetPosition, 0.15f).SetEase(Ease.InQuad))
                .AppendInterval(0.05f)
                .Append(transform.DOMove(startPos, 0.2f).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(_baseScale, 0.2f));
        }

        public Tween AnimateBuff(int newAttack, int newHealth)
        {
            return DOTween.Sequence()
                .AppendCallback(() => UpdateStats(newAttack, newHealth))
                .Append(attackText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f).SetEase(Ease.OutElastic))
                .Join(healthText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f).SetEase(Ease.OutElastic));
        }

        // =====================================================
        // MATERIALPROPERTYBLOCK
        // =====================================================

        private void SetSpriteColor(SpriteRenderer sr, MaterialPropertyBlock mpb, Color color)
        {
            sr.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, color);
            sr.SetPropertyBlock(mpb);
        }

        private void AnimateSpriteColor(SpriteRenderer sr, MaterialPropertyBlock mpb, Color targetColor, float duration)
        {
            sr.GetPropertyBlock(mpb);
            Color currentColor = mpb.GetColor(ColorId);

            DOTween.To(
                () => currentColor,
                x =>
                {
                    currentColor = x;
                    sr.GetPropertyBlock(mpb);
                    mpb.SetColor(ColorId, x);
                    sr.SetPropertyBlock(mpb);
                },
                targetColor,
                duration
            );
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (visualContainer != null) visualContainer.DOKill();
        }
    }
}
