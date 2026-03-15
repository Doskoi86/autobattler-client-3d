using UnityEngine;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Représente un emplacement sur le plateau de jeu.
    /// Chaque slot connaît son index, sa position, et peut contenir un minion.
    /// Gère le highlight visuel (glow vert/rouge) pendant le drag & drop.
    /// </summary>
    public class BoardSlot : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private int slotIndex;

        [Header("Visual")]
        [SerializeField] private MeshRenderer highlightRenderer;

        private Color _defaultColor;
        private Color _validColor = new Color(0f, 1f, 0.3f, 0.4f);    // Vert transparent
        private Color _invalidColor = new Color(1f, 0.2f, 0.2f, 0.4f); // Rouge transparent

        /// <summary>Index de ce slot sur le board (0-6)</summary>
        public int SlotIndex => slotIndex;

        /// <summary>Le minion actuellement sur ce slot (null si vide)</summary>
        public Transform OccupiedBy { get; private set; }

        /// <summary>Ce slot est-il libre ?</summary>
        public bool IsEmpty => OccupiedBy == null;

        private void Awake()
        {
            if (highlightRenderer != null)
            {
                _defaultColor = highlightRenderer.material.color;
                highlightRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Configure l'index du slot (appelé par BoardManager à la création).
        /// </summary>
        public void Setup(int index)
        {
            slotIndex = index;
            gameObject.name = $"Slot_{index}";
        }

        /// <summary>
        /// Place un minion sur ce slot.
        /// </summary>
        public void Place(Transform minion)
        {
            OccupiedBy = minion;
        }

        /// <summary>
        /// Libère ce slot.
        /// </summary>
        public void Clear()
        {
            OccupiedBy = null;
        }

        /// <summary>
        /// Active le highlight (pendant le drag & drop).
        /// </summary>
        public void ShowHighlight(bool isValid)
        {
            if (highlightRenderer == null) return;
            highlightRenderer.enabled = true;
            highlightRenderer.material.color = isValid ? _validColor : _invalidColor;
        }

        /// <summary>
        /// Désactive le highlight.
        /// </summary>
        public void HideHighlight()
        {
            if (highlightRenderer == null) return;
            highlightRenderer.enabled = false;
            highlightRenderer.material.color = _defaultColor;
        }
    }
}
