using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Shop;
using AutoBattler.Client.Hand;
using AutoBattler.Client.Core;

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

        [Header("Zones — seuils Z pour déterminer l'action")]
        [Tooltip("Seuil Z fixe d'achat (utilisé seulement si dropZoneBoard n'est pas assigné)")]
        [SerializeField] private float shopBuyThresholdZFallback = 4f;

        [Tooltip("Seuil Z fixe de vente (utilisé seulement si dropZoneSell n'est pas assigné)")]
        [SerializeField] private float sellThresholdZFallback = 4f;

        [Header("Drop Zone Indicators")]
        [Tooltip("Indicateur visuel de la zone board (affiché pendant le drag)")]
        [SerializeField] private GameObject dropZoneBoard;
        [Tooltip("Indicateur visuel de la zone de vente (affiché pendant le drag)")]
        [SerializeField] private GameObject dropZoneSell;

        [Header("Animation")]
        [SerializeField] private float pickupDuration = 0.15f;
        [SerializeField] private float dropDuration = 0.2f;
        [SerializeField] private float returnDuration = 0.3f;

        private enum DragSource { None, Shop, Hand, Board }

        [Header("Sorting")]
        [Tooltip("Offset de sorting order appliqué pendant le drag pour passer au-dessus de tout")]
        [SerializeField] private int dragSortingOffset = 100;

        // État du drag
        private bool _isDragging;
        private Transform _draggedObject;
        private IDraggable _draggedDraggable;
        private Vector3 _dragStartPosition;
        private Vector3 _dragStartScale;
        private DragSource _dragSource;
        private int _dragShopIndex;
        private Plane _dragPlane;
        private Dictionary<SpriteRenderer, int> _savedSortingOrders = new Dictionary<SpriteRenderer, int>();

        /// <summary>
        /// Seuil Z d'achat calculé depuis le bord haut de la drop zone board.
        /// Drop au-dessus de ce seuil depuis le shop = pas d'achat.
        /// Drop en-dessous = achat.
        /// </summary>
        private float ShopBuyThresholdZ
        {
            get
            {
                if (dropZoneBoard == null) return shopBuyThresholdZFallback;
                var t = dropZoneBoard.transform;
                float halfDepth = 10f * t.lossyScale.z / 2f;
                return t.position.z + halfDepth;
            }
        }

        /// <summary>
        /// Seuil Z de vente calculé dynamiquement depuis le bord bas de la drop zone sell.
        /// Si la drop zone n'est pas assignée, utilise le fallback.
        /// </summary>
        private float SellThresholdZ
        {
            get
            {
                if (dropZoneSell == null) return sellThresholdZFallback;
                // Un Plane Unity fait 10 unités de base. Scale Z = profondeur / 10.
                // Le bord bas = position.z - (10 * scaleZ / 2)
                var t = dropZoneSell.transform;
                float halfDepth = 10f * t.lossyScale.z / 2f;
                return t.position.z - halfDepth;
            }
        }

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

            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

            // Chercher un IDraggable (token ou carte)
            var draggable = hit.transform.GetComponent<IDraggable>();
            if (draggable == null || !draggable.CanDrag) return;

            // Déterminer la source
            var shopClick = hit.transform.GetComponent<ShopCardClick>();
            if (shopClick != null && shopClick.enabled)
            {
                StartDrag(hit.transform, draggable, DragSource.Shop, shopClick.ShopIndex);
                return;
            }

            // Vérifier si c'est une carte en main
            var cardVisual = hit.transform.GetComponent<CardVisual>();
            if (cardVisual != null && HandManager.Instance != null &&
                HandManager.Instance.GetCard(cardVisual.MinionInstanceId) != null)
            {
                StartDrag(hit.transform, draggable, DragSource.Hand, -1);
                return;
            }

            // Sinon c'est un token sur le board
            StartDrag(hit.transform, draggable, DragSource.Board, -1);
        }

        private void StartDrag(Transform target, IDraggable draggable, DragSource source, int shopIndex)
        {
            _isDragging = true;
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
                    dragPlaneHeight + 0.15f,
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
            if (dropPos.z < ShopBuyThresholdZ &&
                ShopManager.Instance != null &&
                ShopManager.Instance.BuyCard(_dragShopIndex))
            {
                Debug.Log($"[DragDrop] Achat shop index {_dragShopIndex}");

                // Animer le token vers la main puis le désactiver
                var target = _draggedObject;
                var handCenter = BoardSurface.Instance != null
                    ? BoardSurface.Instance.GetHandCenter(false)
                    : new Vector3(0f, 0.5f, -2.78f);

                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOMove(handCenter + Vector3.up * 0.5f, 0.3f).SetEase(Ease.InQuad))
                    .Join(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack))
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

            if (boardManager != null && boardManager.IsInBoardZone(dropPos))
            {
                int slotIndex = boardManager.GetInsertionIndex(dropPos);
                string instanceId = _draggedDraggable.MinionInstanceId;
                GameManager.Instance.Server?.PlayMinionAsync(instanceId, slotIndex);

                Debug.Log($"[DragDrop] Jouer {instanceId} au slot {slotIndex}");

                // Animer la carte vers le board puis la désactiver
                // (le serveur enverra OnBoardUpdated qui créera le token sur le board)
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

            // Vente si drop au-dessus du seuil
            float sellZ = SellThresholdZ;
            if (dropPos.z > sellZ)
            {
                string instanceId = _draggedDraggable.MinionInstanceId;
                ShopManager.Instance?.SellCard(instanceId);

                Debug.Log($"[DragDrop] Vente {instanceId}");

                var target = _draggedObject;
                RestoreSortingOrder(target);
                target.DOKill();
                DOTween.Sequence()
                    .Append(target.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (target != null) target.gameObject.SetActive(false);
                    });

                ClearDragState();
                return;
            }

            // Réorganisation sur le board
            if (boardManager != null && boardManager.IsInBoardZone(dropPos))
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
            _dragSource = DragSource.None;
            ClearDragState();
            SetDropZonesVisible(false);
        }

        private void SetDropZonesVisible(bool visible)
        {
            if (dropZoneBoard != null) dropZoneBoard.SetActive(visible);
            if (dropZoneSell != null) dropZoneSell.SetActive(visible);
        }

        /// <summary>
        /// Monte tous les SpriteRenderers de l'objet draggé au-dessus de tout.
        /// </summary>
        private void RaiseSortingOrder(Transform target)
        {
            _savedSortingOrders.Clear();
            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                _savedSortingOrders[sr] = sr.sortingOrder;
                sr.sortingOrder += dragSortingOffset;
            }
        }

        /// <summary>
        /// Restaure les sorting orders originaux.
        /// </summary>
        private void RestoreSortingOrder(Transform target)
        {
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
