using UnityEngine;
using AutoBattler.Client.Network.Protocol;
using AutoBattler.Client.Utils;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// Fabrique de cartes et tokens. Instancie les prefabs et initialise les données.
    /// Assigne automatiquement le Sorting Layer selon le contexte.
    /// </summary>
    public class CardFactory : MonoBehaviour
    {
        public static CardFactory Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private GameObject tokenPrefab;

        [Header("Art")]
        [Tooltip("Base de données des artworks par minion")]
        [SerializeField] private MinionArtDatabase minionArtDatabase;

        [Header("Scale")]
        [SerializeField] private float cardScale = 0.85f;
        [SerializeField] private float tokenScale = 1f;

        public float CardScale => cardScale;
        public MinionArtDatabase MinionArtDb => minionArtDatabase;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Instancie une carte complète (main du joueur).
        /// </summary>
        public CardVisual CreateCard(MinionState data, string sortingLayer = "Hand")
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CardFactory] cardPrefab non assigné !");
                return null;
            }

            var card = Instantiate(cardPrefab);
            card.name = $"Card_{data.Name}";
            card.transform.localScale = Vector3.one * cardScale;

            var visual = card.GetComponent<CardVisual>();
            if (visual == null)
            {
                Debug.LogError("[CardFactory] Le prefab Card n'a pas de composant CardVisual !");
                return null;
            }

            visual.SetData(data);
            if (minionArtDatabase != null)
                visual.SetArtwork(minionArtDatabase.GetArtwork(data.Name));
            SortingLayerHelper.SetSortingLayer(card, sortingLayer);
            return visual;
        }

        /// <summary>
        /// Instancie un token compact (shop ou board).
        /// </summary>
        public MinionTokenVisual CreateToken(MinionState data, bool showTier = true, string sortingLayer = "Shop")
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
            if (minionArtDatabase != null)
                visual.SetArtwork(minionArtDatabase.GetArtwork(data.Name));
            SortingLayerHelper.SetSortingLayer(token, sortingLayer);
            return visual;
        }
    }
}
