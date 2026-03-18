using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Board;
using AutoBattler.Client.Core;
using AutoBattler.Client.Utils;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Affiche une preview de carte complète au hover d'un MinionToken.
    /// Maintient une seule instance de CardVisual réutilisée.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "TooltipController" dans la Hierarchy
    /// 2. Add Component → TooltipController
    /// 3. Aucune référence à câbler (crée la preview à la volée depuis CardFactory)
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Décalage X de la preview par rapport au token survolé")]
        [SerializeField] private float tooltipOffsetX = 3f;

        [Tooltip("Décalage Z de la preview (positif = vers le haut de l'écran)")]
        [SerializeField] private float tooltipOffsetZ = 0f;

        [Tooltip("Scale de la preview")]
        [SerializeField] private float tooltipScale = 0.6f;

        [Tooltip("Y de la preview (au-dessus du plateau)")]
        [SerializeField] private float tooltipY = 0.3f;

        [Tooltip("Délai avant l'apparition du tooltip (en secondes)")]
        [SerializeField] private float showDelay = 0.5f;

        [Tooltip("Durée du fade in")]
        [SerializeField] private float fadeInDuration = 0.15f;

        [Tooltip("Durée du fade out")]
        [SerializeField] private float fadeOutDuration = 0.1f;

        [Tooltip("Cooldown après un drag avant de réactiver le tooltip")]
        [SerializeField] private float postDragCooldown = 0.5f;

        private CardVisual _preview;
        private MinionTokenVisual _currentTarget;
        private MinionTokenVisual _pendingTarget;
        private float _hoverTimer;
        private float _dragEndCooldown;
        private bool _wasDragging;
        private Camera _mainCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_mainCamera == null || Mouse.current == null) return;

            // Pas de tooltip pendant le drag
            if (DragDropController.IsDragging)
            {
                _wasDragging = true;
                if (_currentTarget != null) HideTooltip();
                _pendingTarget = null;
                _hoverTimer = 0f;
                return;
            }

            // Cooldown après un drag
            if (_wasDragging)
            {
                _wasDragging = false;
                _dragEndCooldown = postDragCooldown;
            }
            if (_dragEndCooldown > 0f)
            {
                _dragEndCooldown -= Time.deltaTime;
                return;
            }

            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var token = hit.transform.GetComponent<MinionTokenVisual>();
                if (token != null && token.Data != null)
                {
                    if (token == _pendingTarget)
                    {
                        // Même token qu'avant — accumuler le timer
                        _hoverTimer += Time.deltaTime;
                        if (_hoverTimer >= showDelay && token != _currentTarget)
                            ShowTooltip(token);
                    }
                    else
                    {
                        // Nouveau token — reset le timer
                        _pendingTarget = token;
                        _hoverTimer = 0f;
                        if (_currentTarget != null)
                            HideTooltip();
                    }
                    return;
                }
            }

            // Pas de token sous la souris
            _pendingTarget = null;
            _hoverTimer = 0f;
            if (_currentTarget != null)
                HideTooltip();
        }

        private void ShowTooltip(MinionTokenVisual token)
        {
            _currentTarget = token;

            // Créer la preview si elle n'existe pas
            if (_preview == null)
            {
                if (CardFactory.Instance == null) return;
                _preview = CardFactory.Instance.CreateCard(token.Data, sortingLayer: "Tooltip");
                if (_preview == null) return;
                _preview.DragEnabled = false;
            }
            else
            {
                _preview.SetData(token.Data);
                _preview.gameObject.SetActive(true);
                SortingLayerHelper.SetSortingLayer(_preview.gameObject, "Tooltip");
            }

            // Positionner à côté du token (à droite par défaut, à gauche si ça sort de l'écran)
            var tokenPos = token.transform.position;
            float xOffset = tooltipOffsetX;

            // Vérifier si le tooltip sortirait de l'écran à droite
            var screenPos = _mainCamera.WorldToViewportPoint(
                new Vector3(tokenPos.x + tooltipOffsetX, tooltipY, tokenPos.z));
            if (screenPos.x > 0.85f)
                xOffset = -tooltipOffsetX;

            _preview.transform.position = new Vector3(
                tokenPos.x + xOffset,
                tooltipY,
                tokenPos.z + tooltipOffsetZ
            );

            // Fade in : scale de 0 → tooltipScale
            _preview.transform.DOKill();
            _preview.transform.localScale = Vector3.zero;
            _preview.transform.DOScale(Vector3.one * tooltipScale, fadeInDuration)
                .SetEase(Ease.OutBack);
        }

        private void HideTooltip()
        {
            _currentTarget = null;
            if (_preview != null)
            {
                var preview = _preview;
                preview.transform.DOKill();
                preview.transform.DOScale(Vector3.zero, fadeOutDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (preview != null) preview.gameObject.SetActive(false);
                    });
            }
        }
    }
}
