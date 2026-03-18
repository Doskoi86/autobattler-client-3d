using UnityEngine;
using System.Collections;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;
using System.Collections.Generic;

namespace AutoBattler.Client.Core
{
    /// <summary>
    /// Script de test temporaire qui lance une partie mock au démarrage.
    /// Le ShopManager gère maintenant les cartes visuelles du shop.
    /// Le GameHUD gère l'affichage or/tier/timer/boutons.
    /// Ce script ne fait plus que lancer la partie et logger les events.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Attendre une frame pour que tous les Awake/Start soient exécutés
            yield return null;
            var server = GameManager.Instance?.Server;
            if (server == null)
            {
                Debug.LogError("[Bootstrap] GameManager.Server est null — vérifier que GameManager est dans la scène");
                yield break;
            }

            SubscribeToEvents(server);
            GameManager.Instance.StartMockGame();
        }

        private void SubscribeToEvents(IServerBridge server)
        {
            server.OnConnected += (id, name) =>
                Debug.Log($"<color=green>[Event] Connecté : {name} ({id})</color>");

            server.OnGameFound += (gameId, players) =>
                Debug.Log($"<color=cyan>[Event] Partie trouvée : {gameId}</color>");

            server.OnHeroOffered += (offers) =>
                Debug.Log($"<color=cyan>[Event] Héros proposés : {offers.Count}</color>");

            server.OnAllHeroesSelected += () =>
                Debug.Log("<color=cyan>[Event] Tous les héros sélectionnés !</color>");

            server.OnCombatReplay += (events, playerBoard, opponentBoard) =>
                Debug.Log($"<color=red>[Event] Combat : {events.Count} actions — {playerBoard.Count}v{opponentBoard.Count}</color>");

            server.OnCombatResult += (opponentId, didWin, damage, log) =>
                Debug.Log($"<color={(didWin ? "green" : "red")}>[Event] Combat : {log}</color>");

            server.OnHeroHealthUpdated += (health, armor) =>
                Debug.Log($"<color=red>[Event] PV Héros : {health} (+{armor} armure)</color>");

            server.OnEliminated += (rank) =>
                Debug.Log($"<color=red>[Event] ÉLIMINÉ — Rang final : {rank}</color>");

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
