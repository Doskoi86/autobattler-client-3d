using UnityEngine;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Représente un emplacement sur le plateau de jeu.
    /// Chaque slot connaît son index, sa position, et peut contenir un minion.
    /// Gère le highlight visuel (glow vert/rouge) pendant le drag & drop.
    ///
    /// 📋 CRÉER LE PREFAB BoardSlot :
    /// 1. Hierarchy → 3D Object → Quad → renommer "BoardSlot"
    /// 2. Rotation X = 90 (à plat), Scale = (1.4, 1.8, 1)
    /// 3. Assigner un Material semi-transparent (Assets/_Project/Materials/SlotDefault.mat)
    /// 4. Add Component → BoardSlot
    /// 5. Glisser le MeshRenderer du Quad vers "Highlight Renderer"
    /// 6. Glisser BoardSlot → Assets/_Project/Prefabs/ pour créer le prefab
    /// </summary>
    public class BoardSlot : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private int slotIndex;

        [Header("Visual")]
        [SerializeField] private MeshRenderer highlightRenderer;

        [SerializeField] private Color validColor = new Color(0f, 1f, 0.3f, 0.4f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.4f);

        private Color _defaultColor;

        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Index de ce slot sur le board (0-6)</summary>
        public int SlotIndex => slotIndex;

        /// <summary>Le minion actuellement sur ce slot (null si vide)</summary>
        public Transform OccupiedBy { get; private set; }

        /// <summary>Ce slot est-il libre ?</summary>
        public bool IsEmpty => OccupiedBy == null;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            if (highlightRenderer != null)
            {
                highlightRenderer.GetPropertyBlock(_mpb);
                _defaultColor = _mpb.GetColor(BaseColorId);
                // Si le MPB n'a pas encore de couleur, lire depuis le sharedMaterial
                if (_defaultColor == Color.clear && highlightRenderer.sharedMaterial != null)
                    _defaultColor = highlightRenderer.sharedMaterial.color;
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
            SetHighlightColor(isValid ? validColor : invalidColor);
        }

        /// <summary>
        /// Désactive le highlight.
        /// </summary>
        public void HideHighlight()
        {
            if (highlightRenderer == null) return;
            highlightRenderer.enabled = false;
            SetHighlightColor(_defaultColor);
        }

        private void SetHighlightColor(Color color)
        {
            highlightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            highlightRenderer.SetPropertyBlock(_mpb);
        }
    }
}
