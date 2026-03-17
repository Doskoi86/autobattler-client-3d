using System.Collections.Generic;
using UnityEngine;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Panneau latéral gauche affichant les 8 joueurs de la partie.
    /// Écoute OnPlayersUpdated pour rafraîchir les entrées.
    ///
    /// Les entrées sont des instances du prefab PlayerListEntry,
    /// positionnées verticalement depuis le haut du panel.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "HeroPanel" dans la Hierarchy
    /// 2. Positionner à gauche du plateau (ex: HeroPanelAnchor de BoardSurface)
    /// 3. Add Component → HeroPanel
    /// 4. Glisser le prefab PlayerListEntry vers "Entry Prefab"
    /// </summary>
    public class HeroPanel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Prefab d'une entrée joueur")]
        [SerializeField] private GameObject entryPrefab;

        [Header("Layout")]
        [Tooltip("Espacement vertical entre les entrées")]
        [SerializeField] private float entrySpacing = 1.2f;

        [Tooltip("Nombre max d'entrées")]
        [SerializeField] private int maxEntries = 8;

        // Entrées instanciées
        private List<PlayerListEntry> _entries = new List<PlayerListEntry>();
        private string _localPlayerId;

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPlayersUpdated += HandlePlayersUpdated;
            server.OnConnected += HandleConnected;
            server.OnPlayerEliminated += HandlePlayerEliminated;
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPlayersUpdated -= HandlePlayersUpdated;
            server.OnConnected -= HandleConnected;
            server.OnPlayerEliminated -= HandlePlayerEliminated;
        }

        private void HandleConnected(string playerId, string playerName)
        {
            _localPlayerId = playerId;
        }

        private void HandlePlayersUpdated(List<PlayerPublicState> players)
        {
            // Créer les entrées si nécessaire
            EnsureEntries(players.Count);

            // Trier : vivants d'abord (par PV décroissant), éliminés en bas
            players.Sort((a, b) =>
            {
                if (a.IsEliminated != b.IsEliminated)
                    return a.IsEliminated ? 1 : -1;
                return b.Health.CompareTo(a.Health);
            });

            // Mettre à jour chaque entrée
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i < players.Count)
                {
                    _entries[i].gameObject.SetActive(true);
                    bool isLocal = players[i].PlayerId == _localPlayerId;
                    _entries[i].UpdateData(players[i], isLocal);
                }
                else
                {
                    _entries[i].gameObject.SetActive(false);
                }
            }
        }

        private void HandlePlayerEliminated(string playerId, int rank)
        {
            // Le prochain OnPlayersUpdated mettra à jour IsEliminated
            Debug.Log($"[HeroPanel] Joueur {playerId} éliminé — Rang {rank}");
        }

        private void EnsureEntries(int count)
        {
            count = Mathf.Min(count, maxEntries);

            while (_entries.Count < count)
            {
                if (entryPrefab == null)
                {
                    Debug.LogError("[HeroPanel] entryPrefab non assigné !");
                    return;
                }

                var go = Instantiate(entryPrefab, transform);
                go.name = $"PlayerEntry_{_entries.Count}";

                // Positionner verticalement (Z dans l'espace world vu d'en haut)
                float zOffset = -_entries.Count * entrySpacing;
                go.transform.localPosition = new Vector3(0f, 0f, zOffset);

                var entry = go.GetComponent<PlayerListEntry>();
                if (entry == null)
                {
                    Debug.LogError("[HeroPanel] Le prefab n'a pas de composant PlayerListEntry !");
                    Destroy(go);
                    return;
                }

                _entries.Add(entry);
            }
        }
    }
}
