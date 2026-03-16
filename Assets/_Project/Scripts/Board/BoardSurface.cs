using UnityEngine;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Conteneur de références vers les ancres du plateau.
    /// Ne crée RIEN — les ancres sont des GameObjects Empty dans la scène,
    /// positionnés dans l'éditeur et assignés via l'Inspector.
    ///
    /// Le plateau a DEUX layouts qui alternent :
    /// - Recruit : shop (haut) + board joueur (centre) + héros/boutons + main (bas)
    /// - Combat  : board adverse (haut) + board joueur (centre) + main (bas)
    ///
    /// Les ancres définissent la position CENTRALE de chaque zone.
    /// Les managers (ShopManager, BoardManager, HandManager) lisent ces positions
    /// pour placer leurs éléments.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Sélectionner le GameObject "BoardSurface" dans la Hierarchy
    /// 2. Remettre son scale à (1, 1, 1) et sa position à (0, 0, 0)
    /// 3. Créer les enfants suivants (Create Empty pour chacun) :
    ///
    ///    Enfants permanents :
    ///    - "BoardBackground" → Quad, rotation (90,0,0), pos (0.90, -0.01, 1.00),
    ///      scale (21.30, 11.98, 1), material bois/brun
    ///    - "PlayerBoardAnchor" → Empty, pos (0.90, 0.01, 2.26)
    ///    - "HandAnchor" → Empty, pos (0.90, 0.01, -2.78)
    ///    - "HeroPortraitAnchor" → Empty, pos (0.90, 0.01, -0.58)
    ///    - "HeroPanelAnchor" → Empty, pos (-10.31, 0.01, 1.00)
    ///
    ///    Enfants Recruit only :
    ///    - "ShopAnchor" → Empty, pos (0.90, 0.01, 5.41)
    ///    - "GoldAnchor" → Empty, pos (-4.0, 0.01, -4.5)
    ///
    ///    Enfants Combat only :
    ///    - "OpponentBoardAnchor" → Empty, pos (0.90, 0.01, 4.91)
    ///    - "CombatPlayerBoardAnchor" → Empty, pos (0.90, 0.01, 1.13)
    ///    - "CombatHandAnchor" → Empty, pos (0.90, 0.01, -3.04)
    ///
    /// 4. Glisser chaque enfant vers le champ correspondant dans l'Inspector
    /// </summary>
    public class BoardSurface : MonoBehaviour
    {
        public static BoardSurface Instance { get; private set; }

        [Header("Plateau")]
        [Tooltip("Le Quad de fond du plateau (texture bois)")]
        [SerializeField] private Transform boardBackground;

        [Header("Ancres permanentes")]
        [Tooltip("Centre du board joueur en phase Recruit")]
        [SerializeField] private Transform playerBoardAnchor;

        [Tooltip("Centre de la main du joueur en phase Recruit")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("Position du portrait du héros joueur")]
        [SerializeField] private Transform heroPortraitAnchor;

        [Tooltip("Position du panel des 8 héros (gauche)")]
        [SerializeField] private Transform heroPanelAnchor;

        [Header("Ancres Recruit")]
        [Tooltip("Centre de la zone shop (Bob's Tavern)")]
        [SerializeField] private Transform shopAnchor;

        [Tooltip("Position du compteur d'or")]
        [SerializeField] private Transform goldAnchor;

        [Header("Ancres Combat")]
        [Tooltip("Centre du board adversaire en combat")]
        [SerializeField] private Transform opponentBoardAnchor;

        [Tooltip("Centre du board joueur en combat (peut différer de recruit)")]
        [SerializeField] private Transform combatPlayerBoardAnchor;

        [Tooltip("Centre de la main en combat (peut différer de recruit)")]
        [SerializeField] private Transform combatHandAnchor;

        // --- Propriétés publiques ---

        // Permanentes
        public Transform BoardBackground => boardBackground;
        public Transform HeroPortraitAnchor => heroPortraitAnchor;
        public Transform HeroPanelAnchor => heroPanelAnchor;

        // Recruit
        public Transform ShopAnchor => shopAnchor;
        public Transform GoldAnchor => goldAnchor;

        // Combat
        public Transform OpponentBoardAnchor => opponentBoardAnchor;

        /// <summary>
        /// Position du board joueur selon la phase active.
        /// En recruit : playerBoardAnchor. En combat : combatPlayerBoardAnchor.
        /// </summary>
        public Vector3 GetPlayerBoardCenter(bool isCombat)
        {
            if (isCombat && combatPlayerBoardAnchor != null)
                return combatPlayerBoardAnchor.position;
            if (playerBoardAnchor != null)
                return playerBoardAnchor.position;
            return new Vector3(0f, 0.1f, 2.26f);
        }

        /// <summary>
        /// Position de la main selon la phase active.
        /// </summary>
        public Vector3 GetHandCenter(bool isCombat)
        {
            if (isCombat && combatHandAnchor != null)
                return combatHandAnchor.position;
            if (handAnchor != null)
                return handAnchor.position;
            return new Vector3(0f, 0.1f, -2.78f);
        }

        /// <summary>
        /// Position du shop (recruit only).
        /// </summary>
        public Vector3 GetShopCenter()
        {
            if (shopAnchor != null)
                return shopAnchor.position;
            return new Vector3(0f, 0.1f, 5.41f);
        }

        /// <summary>
        /// Position du board adverse (combat only).
        /// </summary>
        public Vector3 GetOpponentBoardCenter()
        {
            if (opponentBoardAnchor != null)
                return opponentBoardAnchor.position;
            return new Vector3(0f, 0.1f, 4.91f);
        }

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
