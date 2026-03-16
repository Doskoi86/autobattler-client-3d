using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Shop;
using AutoBattler.Client.Core;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Contrôleur de drag & drop 3D par raycast.
    /// Gère 3 types de drag selon la source :
    ///   - Shop → lâcher sous la zone shop = achat
    ///   - Main → lâcher sur un slot board = jouer (main → board)
    ///   - Board → lâcher sur un autre slot = déplacer
    ///
    /// Fonctionne en 3D (Physics.Raycast), pas en UI (Canvas).
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BoardManager boardManager;

        [Header("Drag Settings")]
        [Tooltip("Hauteur du plan invisible de drag (Y world)")]
        [SerializeField] private float dragPlaneHeight = 0.5f;

        [Tooltip("Hauteur à laquelle la carte est soulevée pendant le drag")]
        [SerializeField] private float dragLiftHeight = 1f;

        [Tooltip("Échelle de la carte pendant le drag")]
        [SerializeField] private float dragScale = 1.2f;

        [Header("Zones")]
        [Tooltip("Seuil Z en dessous duquel un drop depuis le shop déclenche un achat")]
        [SerializeField] private float shopBuyThresholdZ = 4f;

        [Tooltip("Seuil Z au-dessus duquel un drop depuis la main/board est considéré comme vente")]
        [SerializeField] private float sellThresholdZ = 6f;

        [Header("Animation")]
        [SerializeField] private float pickupDuration = 0.15f;
        [SerializeField] private float dropDuration = 0.2f;
        [SerializeField] private float returnDuration = 0.3f;

        private enum DragSource { None, Shop, Hand, Board }

        // État du drag
        private bool _isDragging;
        private Transform _draggedObject;
        private CardVisual _draggedCard;
        private Vector3 _dragStartPosition;
        private Vector3 _dragStartScale;
        private DragSource _dragSource;
        private int _dragShopIndex;
        private Plane _dragPlane;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            _dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));
        }

        private void Update()
        {
            if (_isDragging)
                UpdateDrag();
            else
                CheckForPickup();
        }

        // =====================================================
        // PICKUP
        // =====================================================

        private void CheckForPickup()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var card = hit.transform.GetComponent<CardVisual>();
                if (card == null) return;

                // Déterminer la source du drag
                var shopClick = hit.transform.GetComponent<ShopCardClick>();
                if (shopClick != null)
                {
                    StartDrag(card, DragSource.Shop, shopClick.ShopIndex);
                }
                else if (card.DragEnabled)
                {
                    // TODO: distinguer hand vs board quand la main sera implémentée
                    StartDrag(card, DragSource.Board, -1);
                }
            }
        }

        private void StartDrag(CardVisual card, DragSource source, int shopIndex)
        {
            _isDragging = true;
            _draggedObject = card.transform;
            _draggedCard = card;
            _dragStartPosition = card.transform.position;
            _dragStartScale = card.transform.localScale;
            _dragSource = source;
            _dragShopIndex = shopIndex;

            // Légère augmentation de taille, pas de déplacement
            // (le UpdateDrag prend le relais immédiatement pour suivre le curseur)
            _draggedObject.DOKill();
            _draggedObject.DOScale(_dragStartScale * dragScale, pickupDuration)
                .SetEase(Ease.OutBack);

            if (boardManager != null && _dragSource != DragSource.Shop)
                boardManager.ShowAllHighlights(true);
        }

        // =====================================================
        // DRAG
        // =====================================================

        private void UpdateDrag()
        {
            if (_draggedObject == null)
            {
                CancelDrag();
                return;
            }

            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (_dragPlane.Raycast(ray, out float distance))
            {
                var worldPos = ray.GetPoint(distance);
                // La carte suit le curseur directement sur le plan, avec un léger lift
                // pour qu'elle ne traverse pas le plateau
                _draggedObject.position = new Vector3(
                    worldPos.x,
                    dragPlaneHeight + 0.15f,
                    worldPos.z
                );
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        // =====================================================
        // DROP
        // =====================================================

        private void EndDrag()
        {
            _isDragging = false;

            if (boardManager != null)
                boardManager.HideAllHighlights();

            if (_draggedObject == null) return;

            var dropPos = _draggedObject.position;

            switch (_dragSource)
            {
                case DragSource.Shop:
                    HandleShopDrop(dropPos);
                    break;
                case DragSource.Hand:
                    HandleHandDrop(dropPos);
                    break;
                case DragSource.Board:
                    HandleBoardDrop(dropPos);
                    break;
                default:
                    ReturnToStart();
                    break;
            }
        }

        /// <summary>
        /// Drop depuis le shop : si en dessous du seuil Z → acheter
        /// </summary>
        private void HandleShopDrop(Vector3 dropPos)
        {
            if (dropPos.z < shopBuyThresholdZ && ShopManager.Instance != null && ShopManager.Instance.BuyCard(_dragShopIndex))
            {
                // Achat réussi — animer la carte vers la main
                Debug.Log($"[DragDrop] Achat depuis le shop index {_dragShopIndex}");

                var target = _draggedObject;
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(new Vector3(0f, 0.5f, -3.5f), 0.3f).SetEase(Ease.InQuad))
                    .Join(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (target != null) target.gameObject.SetActive(false);
                    });
            }
            else
            {
                // Pas assez d'or ou retour au shop
                ReturnToStart();
            }

            _draggedObject = null;
            _draggedCard = null;
        }

        /// <summary>
        /// Drop depuis la main : si sur un slot du board → jouer
        /// </summary>
        private void HandleHandDrop(Vector3 dropPos)
        {
            var slot = boardManager?.GetNearestPlayerSlot(dropPos);
            if (slot != null && _draggedCard != null)
            {
                int slotIndex = boardManager.GetInsertionIndex(dropPos);
                GameManager.Instance.Server?.PlayMinionAsync(_draggedCard.MinionInstanceId, slotIndex);

                var targetPos = slot.transform.position + Vector3.up * 0.01f;
                var target = _draggedObject;
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(targetPos, dropDuration).SetEase(Ease.OutQuad))
                    .Join(target.DOScale(_dragStartScale, dropDuration))
                    .Append(target.DOPunchScale(Vector3.one * 0.05f, 0.15f));
            }
            else
            {
                ReturnToStart();
            }

            _draggedObject = null;
            _draggedCard = null;
        }

        /// <summary>
        /// Drop depuis le board : si sur un autre slot → déplacer
        /// </summary>
        private void HandleBoardDrop(Vector3 dropPos)
        {
            var slot = boardManager?.GetNearestPlayerSlot(dropPos);
            if (slot != null && _draggedCard != null)
            {
                int slotIndex = boardManager.GetInsertionIndex(dropPos);
                GameManager.Instance.Server?.MoveMinionAsync(_draggedCard.MinionInstanceId, slotIndex);

                var targetPos = slot.transform.position + Vector3.up * 0.01f;
                var target = _draggedObject;
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(targetPos, dropDuration).SetEase(Ease.OutQuad))
                    .Join(target.DOScale(_dragStartScale, dropDuration));
            }
            else
            {
                ReturnToStart();
            }

            _draggedObject = null;
            _draggedCard = null;
        }

        private void ReturnToStart()
        {
            if (_draggedObject == null) return;
            var target = _draggedObject;

            target.DOKill();
            DOTween.Sequence()
                .Append(target.DOMove(_dragStartPosition, returnDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(_dragStartScale, returnDuration));

            _draggedObject = null;
            _draggedCard = null;
        }

        private void CancelDrag()
        {
            _isDragging = false;
            _draggedObject = null;
            _draggedCard = null;
            _dragSource = DragSource.None;

            if (boardManager != null)
                boardManager.HideAllHighlights();
        }
    }

    /// <summary>
    /// Interface pour les objets draggables (minions sur le board ou en main).
    /// </summary>
    public interface IDraggable
    {
        bool CanDrag { get; }
        string MinionInstanceId { get; }
    }
}
