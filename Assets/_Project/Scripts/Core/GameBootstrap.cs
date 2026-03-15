using UnityEngine;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;
using System.Collections.Generic;

namespace AutoBattler.Client.Core
{
    /// <summary>
    /// Script de test temporaire qui lance une partie mock au démarrage
    /// et affiche les events dans la Console Unity.
    /// À remplacer par le vrai flux UI plus tard.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null)
            {
                Debug.LogError("[Bootstrap] GameManager.Server est null — vérifier que GameManager est dans la scène");
                return;
            }

            // S'abonner aux events pour les logger dans la Console
            SubscribeToEvents(server);

            // Lancer une partie mock
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
                foreach (var hero in offers)
                    Debug.Log($"  → {hero.Name} : {hero.Description}");

                // Auto-sélectionner le premier héros pour le test
                server.SelectHeroAsync(offers[0].Id);
            };

            server.OnAllHeroesSelected += () =>
                Debug.Log("<color=cyan>[Event] Tous les héros sélectionnés !</color>");

            server.OnPhaseStarted += (phase, turn, duration) =>
                Debug.Log($"<color=yellow>[Event] Phase : {phase} — Tour {turn} ({duration}s)</color>");

            server.OnShopRefreshed += (offers, gold, upgradeCost) =>
            {
                Debug.Log($"<color=yellow>[Event] Shop : {offers.Count} offres — Or : {gold} — Upgrade : {upgradeCost}</color>");
                foreach (var offer in offers)
                    Debug.Log($"  → [{offer.Tier}★] {offer.Name} ({offer.Attack}/{offer.Health}) {offer.Tribes}");
            };

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
    }
}
