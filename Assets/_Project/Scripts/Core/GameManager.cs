using UnityEngine;
using AutoBattler.Client.Network;

namespace AutoBattler.Client.Core
{
    /// <summary>
    /// Point d'entrée principal du jeu. Gère le mode de connexion
    /// (MockServer vs RealServer) et orchestre le flux de jeu.
    /// Singleton persistant entre les scènes.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "GameManager" dans la Hierarchy
    /// 2. Add Component → GameManager
    /// 3. Si mode MockServer : Add Component → MockServerBridge sur le même GameObject
    ///    puis glisser le composant MockServerBridge vers le champ "Mock Server Bridge"
    /// 4. Cocher "Use Mock Server" dans l'Inspector
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Mode de connexion")]
        [Tooltip("Coché = MockServer local. Décoché = connexion au vrai serveur.")]
        [SerializeField] private bool useMockServer = true;

        [Header("Serveur réel (si MockServer décoché)")]
        [Tooltip("URL du serveur SignalR, ex: http://localhost:5192")]
        [SerializeField] private string serverUrl = "http://localhost:5192";

        [Header("Server Bridges — assigner dans l'Inspector")]
        [Tooltip("Référence au MockServerBridge (composant sur ce GameObject ou un autre)")]
        [SerializeField] private MockServerBridge mockServerBridge;

        /// <summary>
        /// Le bridge actif (MockServer ou RealServer). Tout le jeu passe par cette référence.
        /// </summary>
        public IServerBridge Server { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeServerBridge();
        }

        private void InitializeServerBridge()
        {
            if (useMockServer)
            {
                if (mockServerBridge == null)
                {
                    Debug.LogError("[GameManager] mockServerBridge non assigné ! Ajouter MockServerBridge comme composant et l'assigner dans l'Inspector.");
                    return;
                }

                Server = mockServerBridge;
                Debug.Log("[GameManager] Mode MockServer activé");
            }
            else
            {
                Debug.LogWarning($"[GameManager] Mode RealServer pas encore implémenté — URL : {serverUrl}");
                // TODO: Server = realServerBridge; (assigné via [SerializeField])
            }
        }

        /// <summary>
        /// Lance une partie complète en mode mock (pour tester).
        /// Appeler depuis un bouton UI ou un script de test.
        /// </summary>
        public async void StartMockGame()
        {
            if (Server == null)
            {
                Debug.LogError("[GameManager] Server non initialisé !");
                return;
            }

            await Server.ConnectAsync("Joueur Test");
            await Server.JoinLobbyAsync();
            await Server.LobbyReadyAsync();
        }
    }
}
