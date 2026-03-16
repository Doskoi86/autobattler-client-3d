using UnityEngine;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Fabrique de cartes et tokens. Instancie les prefabs et initialise les données.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Sélectionner le GameObject "CardFactory" dans la Hierarchy
    /// 2. Glisser le prefab Card depuis Assets/_Project/Prefabs/ vers "Card Prefab"
    /// 3. Glisser le prefab MinionToken depuis Assets/_Project/Prefabs/ vers "Token Prefab"
    /// </summary>
    public class CardFactory : MonoBehaviour
    {
        public static CardFactory Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("Prefab de carte complète (pour la main du joueur)")]
        [SerializeField] private GameObject cardPrefab;

        [Tooltip("Prefab de token compact (pour le shop et le board)")]
        [SerializeField] private GameObject tokenPrefab;

        [Tooltip("Scale du token à l'instanciation (ajuster si les sprites sont trop petits/grands)")]
        [SerializeField] private float tokenScale = 3f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Instancie une carte complète (pour la main du joueur).
        /// </summary>
        public CardVisual CreateCard(MinionState data)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CardFactory] cardPrefab non assigné !");
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

        /// <summary>
        /// Instancie un token compact (pour le shop et le board).
        /// </summary>
        public MinionTokenVisual CreateToken(MinionState data, bool showTier = true)
        {
            if (tokenPrefab == null)
            {
                Debug.LogError("[CardFactory] tokenPrefab non assigné !");
                return null;
            }

            var token = Instantiate(tokenPrefab);
            token.name = $"Token_{data.Name}";
            token.transform.localScale = Vector3.one * tokenScale;

            var visual = token.GetComponent<MinionTokenVisual>();
            if (visual == null)
            {
                Debug.LogError("[CardFactory] Le prefab MinionToken n'a pas de composant MinionTokenVisual !");
                return null;
            }

            visual.Init(data, showTier);
            return visual;
        }
    }
}
