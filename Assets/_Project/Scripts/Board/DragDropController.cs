using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Shop;
using AutoBattler.Client.Hand;
using AutoBattler.Client.Core;
using AutoBattler.Client.Utils;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Contrôleur de drag & drop 3D par raycast.
    /// Fonctionne avec IDraggable (MinionTokenVisual et CardVisual).
    ///
    /// Flux supportés :
    ///   - Shop token → drag hors du shop → achat → token disparaît, carte apparaît en main
    ///   - Carte en main → drag sur le board → jouer → carte disparaît, token apparaît sur le board
    ///   - Token sur le board → drag sur un autre slot → réorganiser
    ///   - Token sur le board → drag vers le haut → vendre
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BoardManager boardManager;

        [Header("Drag Settings")]
        [Tooltip("Hauteur du plan invisible de drag (Y world)")]
        [SerializeField] private float dragPlaneHeight = 0.5f;

        [Tooltip("Échelle multiplicateur pendant le drag")]
        [SerializeField] private float dragScale = 1.2f;

        [Header("Drop Zones (Quads transparents, positionnables dans l'éditeur)")]
        [Tooltip("Zone de drop pour poser un minion sur le board")]
        [SerializeField] private AutoBattler.Client.UI.DropZone dropZoneBoard;
        [Tooltip("Zone de drop pour vendre un minion (zone shop)")]
        [SerializeField] private AutoBattler.Client.UI.DropZone dropZoneSell;

        [Header("Animation")]
        [SerializeField] private float pickupDuration = 0.15f;
        [SerializeField] private float dropDuration = 0.2f;
        [SerializeField] private float returnDuration = 0.3f;
        [SerializeField] private float buyAnimDuration = 0.3f;
        [SerializeField] private float buyAnimLift = 0.5f;
        [SerializeField] private float sellAnimDuration = 0.25f;
        [SerializeField] private float dragLiftOffset = 0.15f;

        private enum DragSource { None, Shop, Hand, Board }

        [Header("Limits")]
        [SerializeField] private int maxHandSize = 10;
        [SerializeField] private int maxBoardSize = 7;

        [Header("Sorting")]
        [Tooltip("Offset de sorting order appliqué pendant le drag pour passer au-dessus de tout")]
        [SerializeField] private int dragSortingOffset = 100;

        // État du drag
        public static bool IsDragging { get; private set; }
        private bool _isDragging;
        private Transform _draggedObject;
        private IDraggable _draggedDraggable;
        private Vector3 _dragStartPosition;
        private Vector3 _dragStartScale;
        private DragSource _dragSource;
        private int _dragShopIndex;
        private Plane _dragPlane;
        private Dictionary<SpriteRenderer, int> _savedSortingOrders = new Dictionary<SpriteRenderer, int>();

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            _dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));

            // Cacher les indicateurs au démarrage
            SetDropZonesVisible(false);
        }

        private void Update()
        {
            if (_isDragging)
                UpdateDrag();
            else
                CheckForPickup();
        }

        // =====================================================
        // PICKUP — détecte un clic sur un IDraggable
        // =====================================================

        private void CheckForPickup()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0) return;

            // Trouver le IDraggable avec le sorting order le plus élevé (visuellement au-dessus)
            Transform bestTarget = null;
            IDraggable bestDraggable = null;
            int bestOrder = int.MinValue;

            foreach (var hit in hits)
            {
                var draggable = hit.transform.GetComponent<IDraggable>();
                if (draggable == null || !draggable.CanDrag) continue;

                // Prendre le sorting order max parmi les renderers de cet objet
                int maxOrder = GetMaxSortingOrder(hit.transform);
                if (maxOrder > bestOrder)
                {
                    bestOrder = maxOrder;
                    bestTarget = hit.transform;
                    bestDraggable = draggable;
                }
            }

            if (bestTarget == null || bestDraggable == null) return;

            // Déterminer la source
            var shopClick = bestTarget.GetComponent<ShopCardClick>();
            if (shopClick != null && shopClick.enabled)
            {
                StartDrag(bestTarget, bestDraggable, DragSource.Shop, shopClick.ShopIndex);
                return;
            }

            var cardVisual = bestTarget.GetComponent<CardVisual>();
            if (cardVisual != null && HandManager.Instance != null &&
                HandManager.Instance.GetCard(cardVisual.MinionInstanceId) != null)
            {
                // Remettre la carte à son état normal avant de sauvegarder le drag state
                HandManager.Instance.ForceUnhover();
                StartDrag(bestTarget, bestDraggable, DragSource.Hand, -1);
                return;
            }

            StartDrag(bestTarget, bestDraggable, DragSource.Board, -1);
        }

        /// <summary>
        /// Retourne le sorting order le plus élevé parmi les renderers d'un objet.
        /// Utilisé pour déterminer quel objet est visuellement "au-dessus".
        /// </summary>
        private int GetMaxSortingOrder(Transform target)
        {
            int max = int.MinValue;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // Convertir le sorting layer en poids : chaque layer vaut 10000 ordres
                int layerWeight = SortingLayer.GetLayerValueFromID(r.sortingLayerID) * 10000;
                int total = layerWeight + r.sortingOrder;
                if (total > max) max = total;
            }
            return max;
        }

        private void StartDrag(Transform target, IDraggable draggable, DragSource source, int shopIndex)
        {
            _isDragging = true;
            IsDragging = true;
            _draggedObject = target;
            _draggedDraggable = draggable;
            _dragStartPosition = target.position;
            _dragStartScale = target.localScale;
            _dragSource = source;
            _dragShopIndex = shopIndex;

            _draggedObject.DOKill();
            _draggedObject.DOScale(_dragStartScale * dragScale, pickupDuration)
                .SetEase(Ease.OutBack);

            RaiseSortingOrder(_draggedObject);
            SetDropZonesVisible(true);
        }

        // =====================================================
        // DRAG — suit le curseur
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
                _draggedObject.position = new Vector3(
                    worldPos.x,
                    dragPlaneHeight + dragLiftOffset,
                    worldPos.z
                );
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        // =====================================================
        // DROP — détermine l'action selon la source et la position
        // =====================================================

        private void EndDrag()
        {
            _isDragging = false;
            IsDragging = false;
            SetDropZonesVisible(false);

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
        /// Drop depuis le shop : si en dessous du seuil Z → acheter.
        /// Le serveur va envoyer OnMinionBought + OnHandUpdated qui créeront la carte en main.
        /// </summary>
        private void HandleShopDrop(Vector3 dropPos)
        {
            // Vérifier la limite de main
            int handCount = HandManager.Instance != null ? HandManager.Instance.HandCards.Count : 0;
            if (handCount >= maxHandSize)
            {
                Debug.Log($"[DragDrop] Main pleine ({handCount}/{maxHandSize}) !");
                ReturnToStart();
                ClearDragState();
                return;
            }

            bool inBoardZone = dropZoneBoard != null && dropZoneBoard.Contains(dropPos);
            if (inBoardZone &&
                ShopManager.Instance != null &&
                ShopManager.Instance.BuyCard(_dragShopIndex))
            {
                Debug.Log($"[DragDrop] Achat shop index {_dragShopIndex}");

                // Animer le token vers la main puis le désactiver
                var target = _draggedObject;
                var handCenter = BoardSurface.Instance != null
                    ? BoardSurface.Instance.GetHandCenter(false)
                    : Vector3.zero;

                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(handCenter + Vector3.up * buyAnimLift, buyAnimDuration).SetEase(Ease.InQuad))
                    .Join(target.DOScale(Vector3.zero, buyAnimDuration).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (target != null) target.gameObject.SetActive(false);
                    });
            }
            else
            {
                ReturnToStart();
            }

            ClearDragState();
        }

        /// <summary>
        /// Drop depuis la main : si sur le board → jouer le minion.
        /// Le serveur va envoyer OnBoardUpdated + OnHandUpdated.
        /// </summary>
        private void HandleHandDrop(Vector3 dropPos)
        {
            if (_draggedDraggable == null)
            {
                ReturnToStart();
                ClearDragState();
                return;
            }

            // Vérifier la limite du board
            if (boardManager != null && boardManager.PlayerMinionCount >= maxBoardSize)
            {
                Debug.Log($"[DragDrop] Board plein ({boardManager.PlayerMinionCount}/{maxBoardSize}) !");
                ReturnToStart();
                ClearDragState();
                return;
            }

            bool inBoardZone = dropZoneBoard != null && dropZoneBoard.Contains(dropPos);
            if (inBoardZone && boardManager != null)
            {
                int slotIndex = boardManager.GetInsertionIndex(dropPos);
                string instanceId = _draggedDraggable.MinionInstanceId;
                GameManager.Instance.Server?.PlayMinionAsync(instanceId, slotIndex);

                Debug.Log($"[DragDrop] Jouer {instanceId} au slot {slotIndex}");

                // Animer la carte vers le board puis la désactiver
                var target = _draggedObject;
                var boardCenter = boardManager.PlayerBoardCenter;
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(boardCenter + Vector3.up * 0.1f, dropDuration).SetEase(Ease.OutQuad))
                    .Join(target.DOScale(Vector3.zero, dropDuration).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (target != null) target.gameObject.SetActive(false);
                    });
            }
            else
            {
                ReturnToStart();
            }

            ClearDragState();
        }

        /// <summary>
        /// Drop depuis le board : réorganiser ou vendre.
        /// </summary>
        private void HandleBoardDrop(Vector3 dropPos)
        {
            if (_draggedDraggable == null)
            {
                ReturnToStart();
                ClearDragState();
                return;
            }

            // Vente si drop dans la zone sell
            bool inSellZone = dropZoneSell != null && dropZoneSell.Contains(dropPos);
            if (inSellZone)
            {
                string instanceId = _draggedDraggable.MinionInstanceId;
                ShopManager.Instance?.SellCard(instanceId);

                Debug.Log($"[DragDrop] Vente {instanceId}");

                var target = _draggedObject;
                RestoreSortingOrder(target);
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOScale(Vector3.zero, sellAnimDuration).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (target != null) target.gameObject.SetActive(false);
                    });

                ClearDragState();
                return;
            }

            // Réorganisation sur le board
            bool inBoard = dropZoneBoard != null && dropZoneBoard.Contains(dropPos);
            if (inBoard && boardManager != null)
            {
                int slotIndex = boardManager.GetInsertionIndex(dropPos);
                string instanceId = _draggedDraggable.MinionInstanceId;

                // Restaurer AVANT l'appel serveur (car OnBoardUpdated est synchrone
                // et le BoardManager lancera un DOMove qu'il ne faut pas tuer)
                RestoreSortingOrder(_draggedObject);
                _draggedObject.DOKill();
                _draggedObject.localScale = _dragStartScale;

                Debug.Log($"[DragDrop] Déplacer {instanceId} vers slot {slotIndex}");
                GameManager.Instance.Server?.MoveMinionAsync(instanceId, slotIndex);
            }
            else
            {
                // Hors zone → retour à la position de départ
                ReturnToStart();
            }

            ClearDragState();
        }

        private void ReturnToStart()
        {
            if (_draggedObject == null) return;
            var target = _draggedObject;

            RestoreSortingOrder(target);
            target.DOKill();
            DOTween.Sequence()
                .Append(target.DOMove(_dragStartPosition, returnDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(_dragStartScale, returnDuration));
        }

        private void ClearDragState()
        {
            _draggedObject = null;
            _draggedDraggable = null;
        }

        private void CancelDrag()
        {
            _isDragging = false;
            IsDragging = false;
            _dragSource = DragSource.None;
            ClearDragState();
            SetDropZonesVisible(false);
        }

        private void SetDropZonesVisible(bool visible)
        {
            if (dropZoneBoard != null) dropZoneBoard.SetVisible(visible);
            if (dropZoneSell != null) dropZoneSell.SetVisible(visible);
        }

        /// <summary>
        /// Passe l'objet draggé dans le Sorting Layer "Drag" (au-dessus de tout).
        /// Sauvegarde le layer original pour le restaurer au drop.
        /// </summary>
        private void RaiseSortingOrder(Transform target)
        {
            _savedSortingOrders.Clear();
            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                _savedSortingOrders[sr] = sr.sortingOrder;
            }
            SortingLayerHelper.SetSortingLayer(target.gameObject, "Drag");
        }

        /// <summary>
        /// Restaure le Sorting Layer et les sorting orders originaux.
        /// </summary>
        private void RestoreSortingOrder(Transform target)
        {
            // Déterminer le layer original selon la source du drag
            string originalLayer = _dragSource switch
            {
                DragSource.Shop => "Shop",
                DragSource.Hand => "Hand",
                DragSource.Board => "Board",
                _ => "Default"
            };
            SortingLayerHelper.SetSortingLayer(target.gameObject, originalLayer);

            // Restaurer les sorting orders individuels
            foreach (var kvp in _savedSortingOrders)
            {
                if (kvp.Key != null)
                    kvp.Key.sortingOrder = kvp.Value;
            }
            _savedSortingOrders.Clear();
        }
    }

    /// <summary>
    /// Interface pour les objets draggables (tokens et cartes).
    /// </summary>
    public interface IDraggable
    {
        bool CanDrag { get; }
        string MinionInstanceId { get; }
    }
}
