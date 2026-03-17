using UnityEngine;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Zone de drop invisible en jeu, visible uniquement dans la Scene view.
    /// Détecte si une position world est à l'intérieur de la zone via les bounds.
    ///
    /// Le MeshRenderer est désactivé au runtime (invisible en jeu).
    /// Pour voir la zone dans l'éditeur : cocher MeshRenderer dans l'Inspector,
    /// ou utiliser les Gizmos (rectangle dessiné dans la Scene view).
    ///
    /// 📋 CRÉER UNE DROP ZONE :
    /// 1. Hierarchy → 3D Object → Quad → renommer (ex: "DropZone_Board")
    /// 2. Rotation (90, 0, 0) pour être à plat
    /// 3. Positionner et scaler pour couvrir la zone souhaitée
    /// 4. Add Component → DropZone
    /// 5. Supprimer le MeshCollider (pas besoin)
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DropZone : MonoBehaviour
    {
        [Header("Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.3f, 0.3f);

        private MeshRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            // Toujours invisible en jeu
            _renderer.enabled = false;
        }

        /// <summary>
        /// Vérifie si une position world (XZ) est à l'intérieur de cette zone.
        /// Utilise les bounds du mesh (basé sur position + scale du Quad).
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            // Calculer les bounds manuellement depuis le transform
            // (le renderer est désactivé donc bounds pourrait être vide)
            var center = transform.position;
            var halfSize = transform.lossyScale * 0.5f;

            // Un Quad est sur le plan XY local. Avec rotation (90,0,0) il est sur XZ world.
            // lossyScale.x = largeur, lossyScale.y = profondeur (car rotated)
            float halfWidth = Mathf.Abs(halfSize.x);
            float halfDepth = Mathf.Abs(halfSize.y);

            return worldPosition.x >= center.x - halfWidth && worldPosition.x <= center.x + halfWidth
                && worldPosition.z >= center.z - halfDepth && worldPosition.z <= center.z + halfDepth;
        }

        /// <summary>
        /// Inutilisé — la zone est toujours invisible en jeu.
        /// Gardé pour compatibilité avec DragDropController.
        /// </summary>
        public void SetVisible(bool visible)
        {
            // Intentionnellement vide — la zone reste invisible
        }

#if UNITY_EDITOR
        /// <summary>
        /// Dessine un rectangle coloré dans la Scene view pour visualiser la zone.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Un Quad fait 1×1 unité, scalé par le transform
            var size = new Vector3(transform.lossyScale.x, transform.lossyScale.y, 0.01f);
            Gizmos.DrawCube(Vector3.zero, size);

            // Contour
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
#endif
    }
}
