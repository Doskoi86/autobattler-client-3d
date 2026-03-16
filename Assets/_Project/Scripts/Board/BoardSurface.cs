using UnityEngine;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Conteneur de références vers les zones du plateau.
    /// Ne crée RIEN — les zones sont des GameObjects dans la scène,
    /// assignés via l'Inspector en drag & drop.
    ///
    /// Chaque zone est un Quad visible dans la Scene view.
    /// Déplacer/redimensionner une zone repositionne automatiquement
    /// les cartes et slots qui s'y réfèrent.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "BoardSurface", y ajouter ce script
    /// 2. Créer 4 enfants Quad :
    ///    - "ShopZone" (zone du shop, en haut)
    ///    - "PlayerBoardZone" (board joueur, au centre)
    ///    - "OpponentBoardZone" (board adversaire, au fond)
    ///    - "HandZone" (main du joueur, en bas)
    /// 3. Positionner et redimensionner chaque zone dans la Scene view
    /// 4. Glisser les 4 quads dans les champs ci-dessous
    /// </summary>
    public class BoardSurface : MonoBehaviour
    {
        public static BoardSurface Instance { get; private set; }

        [Header("Zones — assigner les GameObjects de la scène")]
        [SerializeField] private Transform shopZone;
        [SerializeField] private Transform playerBoardZone;
        [SerializeField] private Transform opponentBoardZone;
        [SerializeField] private Transform handZone;

        public Transform ShopZone => shopZone;
        public Transform PlayerBoardZone => playerBoardZone;
        public Transform OpponentBoardZone => opponentBoardZone;
        public Transform HandZone => handZone;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }
}
