using UnityEngine;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;
using System.Collections.Generic;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Bouton de pouvoir héroïque. S'affiche pendant la phase de recrutement.
    /// Montre le nom du pouvoir, le coût, et gère l'activation.
    ///
    /// Les pouvoirs passifs affichent "Passif" et ne sont pas cliquables.
    /// Les pouvoirs actifs affichent le coût et sont cliquables une fois par tour.
    ///
    /// Fonctionne comme un WorldButton : SpriteRenderers + TextMeshPro 3D + BoxCollider.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// Créé automatiquement par SetupHeroPower.cs (execute_script).
    /// Structure :
    ///   Button_HeroPower (HeroPowerButton, BoxCollider)
    ///     ├── Background (SpriteRenderer)
    ///     ├── CostLabel (TextMeshPro 3D) — coût en or ou "Passif"
    ///     └── PowerLabel (TextMeshPro 3D) — nom court du pouvoir
    /// </summary>
    public class HeroPowerButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private TextMeshPro costLabel;
        [SerializeField] private TextMeshPro powerLabel;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.4f, 0.2f, 0.5f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.55f, 0.3f, 0.65f, 1f);
        [SerializeField] private Color usedColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Color passiveColor = new Color(0.25f, 0.35f, 0.5f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.3f, 0.15f, 0.4f, 1f);

        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float hoverDuration = 0.1f;

        [Header("Press")]
        [SerializeField] private float pressPunchScale = 0.1f;
        [SerializeField] private float pressPunchDuration = 0.15f;

        // État
        private HeroOffer _heroData;
        private bool _isActive;       // true = pouvoir actif (cliquable)
        private bool _isUsedThisTurn;
        private bool _isHovered;
        private Vector3 _baseScale;
        private Camera _mainCamera;
        private MaterialPropertyBlock _mpb;
        private Plane _hitPlane;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _baseScale = transform.localScale;
            _mainCamera = Camera.main;
            _mpb = new MaterialPropertyBlock();
            _hitPlane = new Plane(Vector3.up, transform.position);
            SetVisualsVisible(false);
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroPowerUsed += HandleHeroPowerUsed;
            server.OnPhaseStarted += HandlePhaseStarted;
        }

        private void OnDestroy()
        {
            transform.DOKill();
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroPowerUsed -= HandleHeroPowerUsed;
            server.OnPhaseStarted -= HandlePhaseStarted;
        }

        private void Update()
        {
            if (_mainCamera == null || UnityEngine.InputSystem.Mouse.current == null) return;
            if (_heroData == null || backgroundRenderer == null || !backgroundRenderer.enabled) return;
            if (!_isActive || _isUsedThisTurn) return;
            if (HeroSelectionScreen.IsShowing || AutoBattler.Client.Combat.CombatSequencer.IsPlayingCombat) return;

            // Détection par bounds du SpriteRenderer (pas de BoxCollider nécessaire)
            var ray = _mainCamera.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            bool hitThis = false;

            if (_hitPlane.Raycast(ray, out float dist))
            {
                var worldPoint = ray.GetPoint(dist);
                hitThis = backgroundRenderer.bounds.Contains(worldPoint);
            }

            if (hitThis && !_isHovered)
            {
                _isHovered = true;
                OnHoverEnter();
            }
            else if (!hitThis && _isHovered)
            {
                _isHovered = false;
                OnHoverExit();
            }

            if (hitThis && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                OnPress();
            }
        }

        // =====================================================
        // HANDLERS SERVEUR
        // =====================================================

        private void HandleHeroOffered(List<HeroOffer> offers)
        {
            // Pas encore de héros choisi, on attend
        }

        private void HandleAllHeroesSelected()
        {
            // Le héros est sélectionné — on cherche ses données
            // HeroSelectionScreen a envoyé SelectHeroAsync, le MockServer a stocké _selectedHero
            // On attend le premier OnPhaseStarted("Recruiting") pour s'afficher
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            if (phase == "Recruiting")
            {
                _isUsedThisTurn = false;

                // Si on n'a pas encore de données héros, essayer de les récupérer
                if (_heroData == null)
                {
                    var hero = HeroSelectionScreen.SelectedHero;
                    if (hero != null) SetHero(hero);
                }

                if (_heroData != null)
                {
                    SetVisualsVisible(true);
                    if (costLabel != null && _isActive)
                    {
                        costLabel.text = _heroData.HeroPowerCost == 0
                            ? "0" : _heroData.HeroPowerCost.ToString();
                    }
                    UpdateVisual();
                }
            }
        }

        private void HandleHeroPowerUsed(int gold, bool canUseAgain)
        {
            _isUsedThisTurn = !canUseAgain;
            UpdateVisual();
        }

        // =====================================================
        // INITIALISATION
        // =====================================================

        /// <summary>Configure le bouton avec les données du héros choisi.</summary>
        public void SetHero(HeroOffer hero)
        {
            _heroData = hero;
            _isActive = hero.HeroPowerType == "ActiveCost" || hero.HeroPowerType == "ActiveFree";
            _isUsedThisTurn = false;

            if (powerLabel != null)
                powerLabel.text = "Pouvoir";

            if (costLabel != null)
            {
                if (!_isActive)
                    costLabel.text = "Passif";
                else if (hero.HeroPowerCost == 0)
                    costLabel.text = "0";
                else
                    costLabel.text = hero.HeroPowerCost.ToString();
            }

            UpdateVisual();
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
            if (_isUsedThisTurn || !_isActive) return;

            SetBackgroundColor(pressedColor);
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * pressPunchScale, pressPunchDuration)
                .SetEase(Ease.OutElastic)
                .OnComplete(UpdateVisual);

            Debug.Log($"[HeroPower] Activation du pouvoir !");
            GameManager.Instance?.Server?.UseHeroPowerAsync();
        }

        // =====================================================
        // VISUEL
        // =====================================================

        private void UpdateVisual()
        {
            if (!_isActive)
                SetBackgroundColor(passiveColor);
            else if (_isUsedThisTurn)
                SetBackgroundColor(usedColor);
            else
                SetBackgroundColor(_isHovered ? hoverColor : normalColor);

            if (costLabel != null && _isUsedThisTurn)
                costLabel.text = "Utilisé";
        }

        private void SetBackgroundColor(Color color)
        {
            if (backgroundRenderer == null) return;
            backgroundRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            backgroundRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Cache/montre les renderers et textes sans désactiver le GO.</summary>
        private void SetVisualsVisible(bool visible)
        {
            if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
            if (costLabel != null) costLabel.enabled = visible;
            if (powerLabel != null) powerLabel.enabled = visible;
        }
    }
}
