using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Combat;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Bouton world-space cliquable par raycast (pas de Canvas).
    /// Utilise le New Input System pour détecter hover et clic.
    ///
    /// 📋 CRÉER UN BOUTON :
    /// 1. Hierarchy → Create Empty → renommer (ex: "Button_Refresh")
    /// 2. Rotation (90, 0, 0) pour être à plat
    /// 3. Add Component → WorldButton
    /// 4. Add Component → BoxCollider → Size ajustée au background
    /// 5. Enfants : Background (SpriteRenderer), Label (TextMeshPro 3D)
    /// 6. Câbler les références dans l'Inspector
    /// </summary>
    public class WorldButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private TextMeshPro labelText;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.5f, 0.2f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.65f, 0.3f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Color pressedColor = new Color(0.2f, 0.35f, 0.15f, 1f);

        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float hoverDuration = 0.1f;

        [Header("Press")]
        [SerializeField] private float pressPunchScale = 0.1f;
        [SerializeField] private float pressPunchDuration = 0.15f;

        private bool _isInteractable = true;
        private bool _isHovered;
        private Vector3 _baseScale;
        private Camera _mainCamera;
        private Collider _collider;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Event déclenché au clic.</summary>
        public event System.Action OnClick;

        /// <summary>Active/désactive le bouton.</summary>
        public bool Interactable
        {
            get => _isInteractable;
            set
            {
                _isInteractable = value;
                UpdateVisual();
            }
        }

        /// <summary>Change le texte du label.</summary>
        public string Label
        {
            get => labelText != null ? labelText.text : "";
            set { if (labelText != null) labelText.text = value; }
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            _mainCamera = Camera.main;
            _collider = GetComponent<Collider>();
            _mpb = new MaterialPropertyBlock();
            UpdateVisual();
        }

        private void Update()
        {
            if (_mainCamera == null || Mouse.current == null || _collider == null) return;

            // Pas d'interaction quand un overlay bloque l'écran
            if (HeroSelectionScreen.IsShowing || CombatSequencer.IsPlayingCombat)
            {
                if (_isHovered) { _isHovered = false; OnHoverExit(); }
                return;
            }

            // Hover/click detection par RaycastAll (ignore les objets devant le bouton)
            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            bool hitThis = false;

            var hits = Physics.RaycastAll(ray, 100f);
            foreach (var h in hits)
            {
                if (h.collider == _collider)
                {
                    hitThis = true;
                    break;
                }
            }

            if (hitThis && !_isHovered)
            {
                _isHovered = true;
                if (_isInteractable) OnHoverEnter();
            }
            else if (!hitThis && _isHovered)
            {
                _isHovered = false;
                OnHoverExit();
            }

            // Click detection
            if (hitThis && _isInteractable && Mouse.current.leftButton.wasPressedThisFrame)
            {
                OnPress();
            }
        }

        // =====================================================
        // INTERACTIONS
        // =====================================================

        private void OnHoverEnter()
        {
            SetBackgroundColor(hoverColor);
            transform.DOKill();
            transform.DOScale(_baseScale * hoverScale, hoverDuration).SetEase(Ease.OutBack);
        }

        private void OnHoverExit()
        {
            UpdateVisual();
            transform.DOKill();
            transform.DOScale(_baseScale, hoverDuration).SetEase(Ease.OutQuad);
        }

        private void OnPress()
        {
            SetBackgroundColor(pressedColor);
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * pressPunchScale, pressPunchDuration)
                .SetEase(Ease.OutElastic)
                .OnComplete(UpdateVisual);

            Debug.Log($"[WorldButton] {gameObject.name} cliqué");
            OnClick?.Invoke();
        }

        // =====================================================
        // VISUEL
        // =====================================================

        private void UpdateVisual()
        {
            if (_isInteractable)
                SetBackgroundColor(_isHovered ? hoverColor : normalColor);
            else
                SetBackgroundColor(disabledColor);
        }

        private void SetBackgroundColor(Color color)
        {
            if (backgroundRenderer == null) return;
            backgroundRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            backgroundRenderer.SetPropertyBlock(_mpb);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
