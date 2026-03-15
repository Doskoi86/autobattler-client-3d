using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Contrôleur de drag & drop 3D par raycast.
    /// L'utilisateur clique sur un minion, le drag sur un plan invisible
    /// à la hauteur du board, et le drop sur un slot.
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

        [Header("Animation")]
        [SerializeField] private float pickupDuration = 0.15f;
        [SerializeField] private float dropDuration = 0.2f;
        [SerializeField] private float returnDuration = 0.3f;

        // État du drag
        private bool _isDragging;
        private Transform _draggedObject;
        private Vector3 _dragStartPosition;
        private Vector3 _dragStartScale;
        private string _draggedMinionId;
        private Plane _dragPlane;

        // Plan invisible pour projeter le curseur en 3D
        // Sans ça, la carte "flotte" à des profondeurs aléatoires
        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            // Plan horizontal à la hauteur du board
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
        // PICKUP — détecter le clic sur un minion
        // =====================================================

        private void CheckForPickup()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // Vérifier si l'objet touché est un minion draggable
                var draggable = hit.transform.GetComponent<IDraggable>();
                if (draggable != null && draggable.CanDrag)
                {
                    StartDrag(hit.transform, draggable.MinionInstanceId);
                }
            }
        }

        private void StartDrag(Transform target, string minionId)
        {
            _isDragging = true;
            _draggedObject = target;
            _draggedMinionId = minionId;
            _dragStartPosition = target.position;
            _dragStartScale = target.localScale;

            // Animation de pickup : soulever + agrandir
            target.DOKill();
            target.DOMove(_dragStartPosition + Vector3.up * dragLiftHeight, pickupDuration)
                .SetEase(Ease.OutBack);
            target.DOScale(_dragStartScale * dragScale, pickupDuration)
                .SetEase(Ease.OutBack);

            // Montrer les highlights sur les slots valides
            if (boardManager != null)
                boardManager.ShowAllHighlights(true);
        }

        // =====================================================
        // DRAG — suivre le curseur sur le plan invisible
        // =====================================================

        private void UpdateDrag()
        {
            if (_draggedObject == null)
            {
                CancelDrag();
                return;
            }

            // Projeter le curseur sur le plan invisible
            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (_dragPlane.Raycast(ray, out float distance))
            {
                var worldPos = ray.GetPoint(distance);
                // Suivre le curseur en X/Z, garder la hauteur de drag
                _draggedObject.position = new Vector3(
                    worldPos.x,
                    dragPlaneHeight + dragLiftHeight,
                    worldPos.z
                );
            }

            // Relâchement du clic → drop
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        // =====================================================
        // DROP — poser sur un slot ou retourner
        // =====================================================

        private void EndDrag()
        {
            _isDragging = false;

            if (boardManager != null)
                boardManager.HideAllHighlights();

            if (_draggedObject == null) return;

            // Trouver le slot le plus proche
            var slot = boardManager?.GetNearestPlayerSlot(_draggedObject.position);

            if (slot != null)
            {
                // Drop valide → poser sur le slot
                DropOnSlot(slot);
            }
            else
            {
                // Drop invalide → retourner à la position d'origine
                ReturnToStart();
            }
        }

        private void DropOnSlot(BoardSlot slot)
        {
            var target = _draggedObject;
            var targetPos = slot.transform.position + Vector3.up * 0.01f;

            target.DOKill();
            DOTween.Sequence()
                .Append(target.DOMove(targetPos, dropDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(_dragStartScale, dropDuration).SetEase(Ease.OutQuad))
                .Append(target.DOPunchScale(Vector3.one * 0.05f, 0.15f))
                .OnComplete(() =>
                {
                    // Notifier le serveur du placement/déplacement
                    int slotIndex = slot.SlotIndex;
                    var server = Core.GameManager.Instance?.Server;
                    if (server != null && !string.IsNullOrEmpty(_draggedMinionId))
                    {
                        // Déterminer si c'est un Play (main→board) ou un Move (board→board)
                        var existingSlot = boardManager.GetSlotByMinionId(_draggedMinionId);
                        if (existingSlot != null)
                            server.MoveMinionAsync(_draggedMinionId, slotIndex);
                        else
                            server.PlayMinionAsync(_draggedMinionId, slotIndex);
                    }

                    _draggedObject = null;
                    _draggedMinionId = null;
                });
        }

        private void ReturnToStart()
        {
            var target = _draggedObject;

            target.DOKill();
            DOTween.Sequence()
                .Append(target.DOMove(_dragStartPosition, returnDuration).SetEase(Ease.OutQuad))
                .Join(target.DOScale(_dragStartScale, returnDuration).SetEase(Ease.OutQuad));

            _draggedObject = null;
            _draggedMinionId = null;
        }

        private void CancelDrag()
        {
            _isDragging = false;
            _draggedObject = null;
            _draggedMinionId = null;

            if (boardManager != null)
                boardManager.HideAllHighlights();
        }
    }

    /// <summary>
    /// Interface pour les objets draggables (minions sur le board ou en main).
    /// Implémentée par CardVisual (Phase 3).
    /// </summary>
    public interface IDraggable
    {
        bool CanDrag { get; }
        string MinionInstanceId { get; }
    }
}
