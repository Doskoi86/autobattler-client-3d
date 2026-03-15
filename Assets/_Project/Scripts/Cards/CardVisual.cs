using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Rendu 3D d'une carte de minion. Gère l'affichage des stats,
    /// les interactions (hover, sélection) et implémente IDraggable
    /// pour le drag & drop sur le board.
    ///
    /// Structure du GameObject :
    ///   Card (root) — CardVisual + BoxCollider
    ///     └ CardFrame — Quad (cadre de la carte)
    ///     └ CardArt — Quad (illustration, devant le frame)
    ///     └ AttackText — TextMeshPro 3D (bas gauche)
    ///     └ HealthText — TextMeshPro 3D (bas droite)
    ///     └ TierText — TextMeshPro 3D (haut centre)
    ///     └ NameText — TextMeshPro 3D (milieu)
    ///     └ GlowQuad — Quad derrière la carte (glow au hover)
    /// </summary>
    public class CardVisual : MonoBehaviour, IDraggable
    {
        [Header("References — auto-assignées par CardFactory")]
        [SerializeField] private MeshRenderer cardFrame;
        [SerializeField] private MeshRenderer glowQuad;
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro tierText;
        [SerializeField] private TextMeshPro nameText;

        [Header("Hover Settings")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float hoverLift = 0.5f;
        [SerializeField] private float hoverDuration = 0.15f;

        [Header("Colors")]
        [SerializeField] private Color normalGlowColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color hoverGlowColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color goldenGlowColor = new Color(1f, 0.85f, 0.2f, 0.6f);

        // État
        private MinionState _data;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private bool _isHovered;
        private bool _isDragging;
        private Material _glowMaterial;
        private Material _frameMaterial;
        private Camera _mainCamera;

        // IDraggable
        public bool CanDrag => !_isDragging;
        public string MinionInstanceId => _data?.InstanceId;

        /// <summary>Données du minion affichées par cette carte</summary>
        public MinionState Data => _data;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _baseScale = transform.localScale;

            if (glowQuad != null)
            {
                _glowMaterial = glowQuad.material;
                _glowMaterial.color = normalGlowColor;
            }

            if (cardFrame != null)
                _frameMaterial = cardFrame.material;
        }

        // =====================================================
        // DONNÉES
        // =====================================================

        /// <summary>
        /// Met à jour l'affichage de la carte avec les données serveur.
        /// </summary>
        public void SetData(MinionState data)
        {
            _data = data;

            if (attackText != null) attackText.text = data.Attack.ToString();
            if (healthText != null) healthText.text = data.Health.ToString();
            if (tierText != null) tierText.text = $"T{data.Tier}";
            if (nameText != null) nameText.text = data.Name;

            // Couleur du cadre selon le tier
            if (_frameMaterial != null)
                _frameMaterial.color = GetTierColor(data.Tier);

            // Glow doré permanent si golden
            if (data.IsGolden && _glowMaterial != null)
                _glowMaterial.color = goldenGlowColor;
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
        /// Appelé par BoardManager après positionnement.
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

                if (_glowMaterial != null && (_data == null || !_data.IsGolden))
                    _glowMaterial.DOColor(hoverGlowColor, hoverDuration);

                return seq;
            }
            else
            {
                var seq = DOTween.Sequence()
                    .Append(transform.DOScale(_baseScale, hoverDuration).SetEase(Ease.OutQuad))
                    .Join(transform.DOLocalMoveY(_basePosition.y, hoverDuration).SetEase(Ease.OutQuad));

                if (_glowMaterial != null && (_data == null || !_data.IsGolden))
                    _glowMaterial.DOColor(normalGlowColor, hoverDuration);

                return seq;
            }
        }

        // =====================================================
        // ANIMATIONS DE JEU
        // =====================================================

        /// <summary>Animation d'achat : la carte vole depuis une position source</summary>
        public Tween AnimateBuy(Vector3 fromPosition)
        {
            transform.position = fromPosition;
            transform.localScale = Vector3.zero;

            return DOTween.Sequence()
                .Append(transform.DOMove(_basePosition, 0.4f).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(_baseScale, 0.3f).SetEase(Ease.OutBack));
        }

        /// <summary>Animation de pose sur le board : petit rebond</summary>
        public Tween AnimatePlace(Vector3 targetPosition)
        {
            return DOTween.Sequence()
                .Append(transform.DOMove(targetPosition, 0.3f).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(_baseScale, 0.3f))
                .Append(transform.DOPunchScale(Vector3.one * 0.1f, 0.2f));
        }

        /// <summary>Animation de dégâts : tremblement + flash rouge</summary>
        public Tween AnimateDamage(int newHealth)
        {
            return DOTween.Sequence()
                .Append(transform.DOShakePosition(0.2f, 0.1f, 20))
                .Join(healthText.DOColor(Color.red, 0.1f))
                .AppendCallback(() =>
                {
                    healthText.text = newHealth.ToString();
                    if (_data != null) _data.Health = newHealth;
                })
                .Append(healthText.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f))
                .Append(healthText.DOColor(Color.white, 0.15f));
        }

        /// <summary>Animation de mort : rétrécissement + rotation + fade</summary>
        public Tween AnimateDeath()
        {
            return DOTween.Sequence()
                .Append(transform.DOScale(0f, 0.3f).SetEase(Ease.InBack))
                .Join(transform.DORotate(new Vector3(0f, 0f, 15f), 0.3f))
                .OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>Animation d'attaque : avancer vers la cible puis revenir</summary>
        public Tween AnimateAttack(Vector3 targetPosition)
        {
            var startPos = transform.position;

            return DOTween.Sequence()
                .Append(transform.DOMove(targetPosition, 0.15f).SetEase(Ease.InQuad))
                .AppendInterval(0.05f)
                .Append(transform.DOMove(startPos, 0.2f).SetEase(Ease.OutQuad));
        }

        /// <summary>Animation de buff : particules vertes + stats qui pulsent</summary>
        public Tween AnimateBuff(int newAttack, int newHealth)
        {
            return DOTween.Sequence()
                .AppendCallback(() =>
                {
                    if (attackText != null) attackText.text = newAttack.ToString();
                    if (healthText != null) healthText.text = newHealth.ToString();
                    if (_data != null) { _data.Attack = newAttack; _data.Health = newHealth; }
                })
                .Append(attackText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f).SetEase(Ease.OutElastic))
                .Join(healthText.transform.DOPunchScale(Vector3.one * 0.4f, 0.25f).SetEase(Ease.OutElastic));
        }

        // =====================================================
        // UTILITAIRES
        // =====================================================

        /// <summary>Couleur du cadre selon le tier</summary>
        private Color GetTierColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.6f, 0.6f, 0.6f);   // Gris
                case 2: return new Color(0.2f, 0.7f, 0.3f);   // Vert
                case 3: return new Color(0.2f, 0.4f, 0.9f);   // Bleu
                case 4: return new Color(0.6f, 0.2f, 0.8f);   // Violet
                case 5: return new Color(0.9f, 0.6f, 0.1f);   // Orange
                case 6: return new Color(0.9f, 0.2f, 0.2f);   // Rouge
                default: return Color.white;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
