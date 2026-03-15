using UnityEngine;
using AutoBattler.Client.Network;

namespace AutoBattler.Client.Core
{
    /// <summary>
    /// Point d'entrée principal du jeu. Gère le mode de connexion
    /// (MockServer vs RealServer) et orchestre le flux de jeu.
    /// Singleton persistant entre les scènes.
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
                // Le MockServerBridge est un MonoBehaviour, on l'ajoute au même GameObject
                var mock = gameObject.AddComponent<MockServerBridge>();
                Server = mock;
                Debug.Log("[GameManager] Mode MockServer activé");
            }
            else
            {
                // RealServerBridge sera implémenté en Phase 1.5 (connexion SignalR)
                Debug.LogWarning($"[GameManager] Mode RealServer pas encore implémenté — URL : {serverUrl}");
                // TODO: Server = gameObject.AddComponent<RealServerBridge>();
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
