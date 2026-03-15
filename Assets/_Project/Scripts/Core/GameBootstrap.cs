using UnityEngine;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;
using AutoBattler.Client.Cards;
using System.Collections.Generic;

namespace AutoBattler.Client.Core
{
    /// <summary>
    /// Script de test temporaire qui lance une partie mock au démarrage,
    /// crée les cartes visuelles du shop, et affiche les events dans la Console.
    /// À remplacer par le vrai flux UI plus tard.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Shop Card Positions")]
        [Tooltip("Position Y des cartes du shop")]
        [SerializeField] private float shopY = 0.1f;
        [Tooltip("Position Z des cartes du shop")]
        [SerializeField] private float shopZ = 5.5f;
        [Tooltip("Espacement entre les cartes du shop")]
        [SerializeField] private float shopSpacing = 1.8f;

        private List<CardVisual> _shopCards = new List<CardVisual>();

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null)
            {
                Debug.LogError("[Bootstrap] GameManager.Server est null — vérifier que GameManager est dans la scène");
                return;
            }

            SubscribeToEvents(server);
            GameManager.Instance.StartMockGame();
        }

        private void SubscribeToEvents(IServerBridge server)
        {
            server.OnConnected += (id, name) =>
                Debug.Log($"<color=green>[Event] Connecté : {name} ({id})</color>");

            server.OnLobbyJoined += (players, readyIds) =>
                Debug.Log($"<color=green>[Event] Lobby rejoint : {players.Count} joueurs</color>");

            server.OnGameFound += (gameId, players) =>
                Debug.Log($"<color=cyan>[Event] Partie trouvée : {gameId}</color>");

            server.OnHeroOffered += (offers) =>
            {
                Debug.Log($"<color=cyan>[Event] Héros proposés : {offers.Count}</color>");
                server.SelectHeroAsync(offers[0].Id);
            };

            server.OnAllHeroesSelected += () =>
                Debug.Log("<color=cyan>[Event] Tous les héros sélectionnés !</color>");

            server.OnPhaseStarted += (phase, turn, duration) =>
                Debug.Log($"<color=yellow>[Event] Phase : {phase} — Tour {turn} ({duration}s)</color>");

            // Créer les cartes visuelles du shop
            server.OnShopRefreshed += OnShopRefreshed;

            server.OnPlayersUpdated += (players) =>
                Debug.Log($"<color=white>[Event] Joueurs : {players.Count} actifs</color>");

            server.OnMinionBought += (id, name, gold) =>
                Debug.Log($"<color=green>[Event] Acheté : {name} — Or restant : {gold}</color>");

            server.OnMinionSold += (id, gold) =>
                Debug.Log($"<color=red>[Event] Vendu — Or : {gold}</color>");

            server.OnBoardUpdated += (minions) =>
                Debug.Log($"<color=white>[Event] Board : {minions.Count} minions</color>");

            server.OnHandUpdated += (minions) =>
                Debug.Log($"<color=white>[Event] Main : {minions.Count} cartes</color>");

            server.OnCombatReplay += (events, playerBoard, opponentBoard) =>
                Debug.Log($"<color=red>[Event] Combat Replay : {events.Count} actions — {playerBoard.Count}v{opponentBoard.Count}</color>");

            server.OnCombatResult += (opponentId, didWin, damage, log) =>
                Debug.Log($"<color={(didWin ? "green" : "red")}>[Event] Combat : {log}</color>");

            server.OnHeroHealthUpdated += (health, armor) =>
                Debug.Log($"<color=red>[Event] PV Héros : {health} (+{armor} armure)</color>");

            server.OnEliminated += (rank) =>
                Debug.Log($"<color=red>[Event] ÉLIMINÉ — Rang final : {rank}</color>");

            server.OnPlayerEliminated += (id, rank) =>
                Debug.Log($"<color=grey>[Event] Joueur {id} éliminé — Rang : {rank}</color>");

            server.OnGameOver += (rankings) =>
            {
                Debug.Log("<color=yellow>[Event] PARTIE TERMINÉE</color>");
                foreach (var r in rankings)
                    Debug.Log($"  #{r.FinalRank} — {r.PlayerName}");
            };

            server.OnError += (msg) =>
                Debug.LogWarning($"[Event] Erreur : {msg}");
        }

        /// <summary>
        /// Quand le shop est refreshed, on crée les cartes visuelles.
        /// </summary>
        private void OnShopRefreshed(List<ShopOfferState> offers, int gold, int upgradeCost)
        {
            Debug.Log($"<color=yellow>[Event] Shop : {offers.Count} offres — Or : {gold} — Upgrade : {upgradeCost}</color>");

            if (CardFactory.Instance == null)
            {
                Debug.LogWarning("[Bootstrap] CardFactory manquant — pas de cartes visuelles");
                return;
            }

            // Nettoyer les anciennes cartes du shop
            foreach (var card in _shopCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _shopCards.Clear();

            // Créer les nouvelles cartes du shop
            float totalWidth = (offers.Count - 1) * shopSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];

                // Convertir ShopOfferState en MinionState pour CardVisual
                var minionData = new MinionState
                {
                    InstanceId = offer.InstanceId,
                    Name = offer.Name,
                    Attack = offer.Attack,
                    Health = offer.Health,
                    Tier = offer.Tier,
                    Keywords = offer.Keywords,
                    Description = offer.Description,
                    Tribes = offer.Tribes,
                    IsGolden = false
                };

                var card = CardFactory.Instance.CreateCard(minionData);
                var pos = new Vector3(startX + i * shopSpacing, shopY, shopZ);
                card.transform.position = pos;
                card.SetBasePosition(pos);

                _shopCards.Add(card);

                Debug.Log($"  → [{offer.Tier}★] {offer.Name} ({offer.Attack}/{offer.Health}) {offer.Tribes}");
            }
        }
    }
}
