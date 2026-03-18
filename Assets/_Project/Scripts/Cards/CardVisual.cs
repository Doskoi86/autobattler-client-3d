using UnityEngine;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Rendu d'une carte de minion via SpriteRenderers empilés + TextMeshPro 3D.
    /// Chaque couche visuelle est un SpriteRenderer enfant avec son Sorting Order.
    ///
    /// 📋 IMPORT DES SPRITES — à faire une seule fois :
    /// 1. Project → Assets/_Project/Sprites/Cards/ → sélectionner TOUS les sprites
    /// 2. Inspector → Pixels Per Unit : 225
    /// 3. Max Size : 2048, Compression : None
    /// 4. Cliquer Apply
    ///
    /// 📋 CRÉER LE PREFAB Card :
    ///
    /// ÉTAPE A — Root
    /// 1. Hierarchy → Create Empty → renommer "Card"
    /// 2. Position (0, 0.1, 0), Rotation (90, 0, 0)
    /// 3. Add Component → CardVisual
    /// 4. Add Component → Box Collider → Size (1.2, 1.8, 0.02)
    /// 5. Add Component → ShopCardClick → décocher (désactivé)
    ///
    /// ÉTAPE B — Couches de sprites (enfants de Card)
    /// Pour chaque couche : Clic droit sur Card → Create Empty → renommer → Add Component → Sprite Renderer
    /// Toutes les couches ont Position (0, 0, 0) sauf indication contraire.
    /// Le Z local négatif = plus proche de la caméra (devant).
    ///
    ///   "Background"     — Sprite: carteFondBlanc  — Sorting Order: 0
    ///   "Artwork"         — Sprite: (vide)          — Sorting Order: 1  — Pos (0, 0.17, 0) — Scale (0.55, 0.55, 1)
    ///   "ArtFrame"        — Sprite: wooden_frame    — Sorting Order: 2  — Pos (0, 0.17, 0)
    ///   "ScrollArea"      — Sprite: carteParcheminTexte — Sorting Order: 3 — Pos (0, -0.37, 0)
    ///   "NameBanner"      — Sprite: carteParcheminNom   — Sorting Order: 4 — Pos (0, 0.02, 0)
    ///   "Border"          — Sprite: carteTourMetal       — Sorting Order: 5
    ///   "TavernTierBadge" — Sprite: carteRondNiveau      — Sorting Order: 6 — Pos (-0.47, 0.67, 0)  — Scale (0.6, 0.6, 1)
    ///   "AttackBadge"     — Sprite: attack_badge         — Sorting Order: 7 — Pos (-0.50, -0.62, 0) — Scale (0.7, 0.7, 1)
    ///   "HealthBadge"     — Sprite: health_badge         — Sorting Order: 7 — Pos (0.50, -0.64, 0)  — Scale (0.7, 0.7, 1)
    ///   "RaceBadge"       — Sprite: cartePlaqueType      — Sorting Order: 6 — Pos (0, -0.72, 0)     — Scale (0.7, 0.7, 1)
    ///   "GlowQuad"        — Sprite: (carré blanc ou carteFondBlanc) — Sorting Order: -1 — Scale (1.1, 1.1, 1) — Color (1,1,1,0)
    ///
    /// ÉTAPE C — Textes (enfants de Card)
    /// Pour chaque texte : Clic droit sur Card → 3D Object → Text - TextMeshPro
    ///   "AttackText"  — Pos (-0.50, -0.62, -0.01) — FontSize 4  — Color blanc — Alignment center/middle
    ///   "HealthText"  — Pos (0.50, -0.64, -0.01)  — FontSize 4  — Color blanc
    ///   "TierText"    — Pos (-0.47, 0.67, -0.01)  — FontSize 4  — Color blanc
    ///   "NameText"    — Pos (0, 0.02, -0.01)       — FontSize 3  — Color blanc — Alignment center/middle
    ///   "DescriptionText" — Pos (0, -0.37, -0.01)  — FontSize 2  — Color noir — Enable Auto Size (min 1, max 2.5)
    ///   "RaceText"    — Pos (0, -0.72, -0.01)      — FontSize 2  — Color blanc
    ///   Pour tous : Rect Width 1, Height 0.3 (ajuster selon besoin)
    ///
    /// ÉTAPE D — Assigner les références dans CardVisual Inspector
    /// ÉTAPE E — Glisser Card → Assets/_Project/Prefabs/ pour créer le prefab
    /// </summary>
    public class CardVisual : MonoBehaviour, IDraggable
    {
        [Header("Sprite References — assignées dans le Prefab Inspector")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;
        [SerializeField] private SpriteRenderer artFrameRenderer;
        [SerializeField] private SpriteRenderer artworkRenderer;
        [SerializeField] private SpriteRenderer glowRenderer;

        [Header("Text References")]
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro tierText;
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro descriptionText;
        [SerializeField] private TextMeshPro raceText;

        [Header("Tribe Backgrounds — associe une race à un fond de carte")]
        [Tooltip("Chaque entrée associe un nom de race (ex: 'Beast') à un sprite de fond")]
        [SerializeField] private TribeBackground[] tribeBackgrounds;
        [SerializeField] private Sprite defaultBackground;

        [Header("Frame Sprites")]
        [SerializeField] private Sprite normalFrame;
        [SerializeField] private Sprite goldenFrame;

        [Header("Hover Settings")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float hoverLift = 0.5f;
        [SerializeField] private float hoverDuration = 0.15f;

        [Header("Glow Colors")]
        [SerializeField] private Color normalGlowColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color hoverGlowColor = new Color(0.8f, 0.9f, 1f, 0.4f);
        [SerializeField] private Color goldenGlowColor = new Color(1f, 0.85f, 0.2f, 0.6f);

        [Header("Animation Durations")]
        [SerializeField] private float buyMoveDuration = 0.4f;
        [SerializeField] private float buyScaleDuration = 0.3f;
        [SerializeField] private float placeDuration = 0.3f;
        [SerializeField] private float placePunchScale = 0.1f;
        [SerializeField] private float placePunchDuration = 0.2f;
        [SerializeField] private float damageDuration = 0.2f;
        [SerializeField] private float damageShakeIntensity = 0.1f;
        [SerializeField] private float deathDuration = 0.3f;
        [SerializeField] private float deathRotation = 15f;
        [SerializeField] private float attackForwardDuration = 0.15f;
        [SerializeField] private float attackReturnDuration = 0.2f;
        [SerializeField] private float buffPunchScale = 0.4f;
        [SerializeField] private float buffPunchDuration = 0.25f;

        // État
        private MinionState _data;
        private Vector3 _basePosition;
        public Vector3 BasePosition => _basePosition;
        private Vector3 _baseScale;
        private bool _isHovered;
        private bool _isDragging;
        private Camera _mainCamera;

        // MaterialPropertyBlock pour les variations per-instance
        private MaterialPropertyBlock _glowMPB;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // IDraggable
        public bool DragEnabled { get; set; } = true;
        public bool CanDrag => DragEnabled && !_isDragging;
        public string MinionInstanceId => _data?.InstanceId;
        public MinionState Data => _data;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _baseScale = transform.localScale;
            _glowMPB = new MaterialPropertyBlock();

            if (glowRenderer != null)
                SetSpriteColor(glowRenderer, _glowMPB, normalGlowColor);
        }

        // =====================================================
        // DONNÉES
        // =====================================================

        /// <summary>
        /// <summary>
        /// Assigne le sprite d'artwork du minion.
        /// </summary>
        public void SetArtwork(Sprite artwork)
        {
            if (artworkRenderer != null && artwork != null)
                artworkRenderer.sprite = artwork;
        }

        /// <summary>
        /// Met à jour l'affichage de la carte avec les données serveur.
        /// </summary>
        public void SetData(MinionState data)
        {
            _data = data;

            // Textes
            if (attackText != null) attackText.text = data.Attack.ToString();
            if (healthText != null) healthText.text = data.Health.ToString();
            if (tierText != null) tierText.text = data.Tier.ToString();
            if (nameText != null) nameText.text = data.Name;
            if (descriptionText != null) descriptionText.text = data.Description ?? "";
            if (raceText != null) raceText.text = data.Tribes ?? "";

            // Fond selon la race (swap de sprite)
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sprite = GetTribeBackground(data.Tribes);
            }

            // Cadre doré si golden
            if (artFrameRenderer != null)
            {
                if (data.IsGolden && goldenFrame != null)
                    artFrameRenderer.sprite = goldenFrame;
                else if (normalFrame != null)
                    artFrameRenderer.sprite = normalFrame;
            }

            // Glow doré permanent si golden
            if (data.IsGolden && glowRenderer != null)
                SetSpriteColor(glowRenderer, _glowMPB, goldenGlowColor);
        }

        /// <summary>
        /// Met à jour uniquement les stats (après un buff par exemple).
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
        /// Enregistre la position de base (utilisée pour le retour après hover).
        /// </summary>
        public void SetBasePosition(Vector3 position)
        {
            _basePosition = position;
        }

        // =====================================================
        // INTERACTIONS — HOVER
        // =====================================================

        private void OnMouseEnter()
        {
            if (_isDragging) return;
            _isHovered = true;
            AnimateHover(true);
        }

        private void OnMouseExit()
        {
            if (_isDragging) return;
            _isHovered = false;
            AnimateHover(false);
        }

        private Tween AnimateHover(bool hovering)
        {
            transform.DOKill();

            if (hovering)
            {
                var seq = DOTween.Sequence()
                    .Append(transform.DOScale(_baseScale * hoverScale, hoverDuration).SetEase(Ease.OutBack))
                    .Join(transform.DOLocalMoveY(_basePosition.y + hoverLift, hoverDuration).SetEase(Ease.OutQuad));

                if (glowRenderer != null && (_data == null || !_data.IsGolden))
                    AnimateSpriteColor(glowRenderer, _glowMPB, hoverGlowColor, hoverDuration);

                return seq;
            }
            else
            {
                var seq = DOTween.Sequence()
                    .Append(transform.DOScale(_baseScale, hoverDuration).SetEase(Ease.OutQuad))
                    .Join(transform.DOLocalMoveY(_basePosition.y, hoverDuration).SetEase(Ease.OutQuad));

                if (glowRenderer != null && (_data == null || !_data.IsGolden))
                    AnimateSpriteColor(glowRenderer, _glowMPB, normalGlowColor, hoverDuration);

                return seq;
            }
        }

        // =====================================================
        // ANIMATIONS DE JEU
        // =====================================================

        public Tween AnimateBuy(Vector3 fromPosition)
        {
            transform.position = fromPosition;
            transform.localScale = Vector3.zero;

            return DOTween.Sequence()
                .Append(transform.DOMove(_basePosition, buyMoveDuration).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(_baseScale, buyScaleDuration).SetEase(Ease.OutBack));
        }

        public Tween AnimatePlace(Vector3 targetPosition)
        {
            return DOTween.Sequence()
                .Append(transform.DOMove(targetPosition, placeDuration).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(_baseScale, placeDuration))
                .Append(transform.DOPunchScale(Vector3.one * placePunchScale, placePunchDuration));
        }

        public Tween AnimateDamage(int newHealth)
        {
            return DOTween.Sequence()
                .Append(transform.DOShakePosition(damageDuration, damageShakeIntensity, 20))
                .Join(healthText.DOColor(Color.red, damageDuration * 0.5f))
                .AppendCallback(() =>
                {
                    healthText.text = newHealth.ToString();
                    if (_data != null) _data.Health = newHealth;
                })
                .Append(healthText.transform.DOPunchScale(Vector3.one * 0.3f, damageDuration))
                .Append(healthText.DOColor(Color.white, damageDuration * 0.75f));
        }

        public Tween AnimateDeath()
        {
            return DOTween.Sequence()
                .Append(transform.DOScale(0f, deathDuration).SetEase(Ease.InBack))
                .Join(transform.DORotate(new Vector3(0f, 0f, deathRotation), deathDuration))
                .OnComplete(() => gameObject.SetActive(false));
        }

        public Tween AnimateAttack(Vector3 targetPosition)
        {
            var startPos = transform.position;

            return DOTween.Sequence()
                .Append(transform.DOMove(targetPosition, attackForwardDuration).SetEase(Ease.InQuad))
                .AppendInterval(0.05f)
                .Append(transform.DOMove(startPos, attackReturnDuration).SetEase(Ease.OutQuad));
        }

        public Tween AnimateBuff(int newAttack, int newHealth)
        {
            return DOTween.Sequence()
                .AppendCallback(() =>
                {
                    if (attackText != null) attackText.text = newAttack.ToString();
                    if (healthText != null) healthText.text = newHealth.ToString();
                    if (_data != null) { _data.Attack = newAttack; _data.Health = newHealth; }
                })
                .Append(attackText.transform.DOPunchScale(Vector3.one * buffPunchScale, buffPunchDuration).SetEase(Ease.OutElastic))
                .Join(healthText.transform.DOPunchScale(Vector3.one * buffPunchScale, buffPunchDuration).SetEase(Ease.OutElastic));
        }

        // =====================================================
        // MATERIALPROPERTYBLOCK (pour SpriteRenderer)
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
            if (currentColor.a == 0 && targetColor.a == 0) return;

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

        // =====================================================
        // TRIBE BACKGROUNDS
        // =====================================================

        private Sprite GetTribeBackground(string tribes)
        {
            if (string.IsNullOrEmpty(tribes) || tribeBackgrounds == null)
                return defaultBackground;

            foreach (var tb in tribeBackgrounds)
            {
                if (tribes.Contains(tb.tribeName))
                    return tb.backgroundSprite;
            }

            return defaultBackground;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }

    /// <summary>
    /// Associe un nom de race à un sprite de fond de carte.
    /// Configurable dans l'Inspector — pas besoin de recompiler pour ajouter une race.
    /// </summary>
    [System.Serializable]
    public class TribeBackground
    {
        [Tooltip("Nom de la race tel qu'envoyé par le serveur (ex: Beast, Dragon, Demon...)")]
        public string tribeName;
        [Tooltip("Sprite de fond correspondant (ex: carteFondVert pour Beast)")]
        public Sprite backgroundSprite;
    }
}
