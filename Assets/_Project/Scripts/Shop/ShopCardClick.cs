using UnityEngine;

namespace AutoBattler.Client.Shop
{
    /// <summary>
    /// Marqueur ajouté aux cartes du shop pour identifier leur index.
    /// Le DragDropController utilise ce composant pour savoir que
    /// le drag vient du shop et déclencher un achat au drop.
    /// </summary>
    public class ShopCardClick : MonoBehaviour
    {
        public int ShopIndex { get; private set; }

        public void Setup(int shopIndex, ShopManager manager)
        {
            ShopIndex = shopIndex;
        }
    }
}
