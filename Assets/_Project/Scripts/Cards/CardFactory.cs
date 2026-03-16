using UnityEngine;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Fabrique de cartes 3D. Instancie le Card prefab et initialise les données.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer le prefab Card (voir CardVisual pour la hiérarchie)
    /// 2. Sélectionner le GameObject "CardFactory" dans la Hierarchy
    /// 3. Dans l'Inspector, glisser le prefab Card depuis Assets/_Project/Prefabs/ vers "Card Prefab"
    /// </summary>
    public class CardFactory : MonoBehaviour
    {
        public static CardFactory Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("Prefab de carte avec CardVisual + tous les sous-objets")]
        [SerializeField] private GameObject cardPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Instancie une carte depuis le prefab et applique les données serveur.
        /// </summary>
        public CardVisual CreateCard(MinionState data)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CardFactory] cardPrefab non assigné ! Glisser le prefab Card dans l'Inspector.");
                return null;
            }

            var card = Instantiate(cardPrefab);
            card.name = $"Card_{data.Name}";

            var visual = card.GetComponent<CardVisual>();
            if (visual == null)
            {
                Debug.LogError("[CardFactory] Le prefab Card n'a pas de composant CardVisual !");
                return null;
            }

            visual.SetData(data);
            return visual;
        }
    }
}
