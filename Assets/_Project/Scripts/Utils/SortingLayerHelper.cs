using UnityEngine;

namespace AutoBattler.Client.Utils
{
    /// <summary>
    /// Utilitaire pour changer le Sorting Layer de tous les Renderers
    /// (SpriteRenderer + MeshRenderer/TextMeshPro) d'un GameObject et ses enfants.
    /// </summary>
    public static class SortingLayerHelper
    {
        /// <summary>
        /// Change le sorting layer de TOUS les renderers du GameObject
        /// (SpriteRenderers et MeshRenderers incluant TextMeshPro 3D).
        /// Les sorting orders relatifs sont préservés.
        /// </summary>
        public static void SetSortingLayer(GameObject go, string layerName)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sortingLayerName = layerName;
            }
        }
    }
}
