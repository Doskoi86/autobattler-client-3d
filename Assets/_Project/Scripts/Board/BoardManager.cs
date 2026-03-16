using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Gère le board joueur et adversaire : placement des tokens,
    /// repositionnement dynamique, synchronisation avec le serveur.
    ///
    /// Écoute OnBoardUpdated pour créer/repositionner les tokens automatiquement.
    /// Plus besoin de BoardSlot — les tokens sont positionnés directement.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        [Header("Board Settings")]
        [Tooltip("Espacement entre les tokens sur le board")]
        [SerializeField] private float slotSpacing = 2.8f;

        [Tooltip("Courbure de l'arc (0 = ligne droite)")]
        [SerializeField] private float arcAmount = 0.3f;

        [Tooltip("Nombre max de minions par côté")]
        [SerializeField] private int maxSlots = 7;

        [Header("Animation")]
        [SerializeField] private float repositionDuration = 0.3f;
        [SerializeField] private Ease repositionEase = Ease.OutQuad;

        // Accesseurs pour le DragDropController
        public float SlotSpacing => slotSpacing;
        public float ArcAmount => arcAmount;

        // Tokens sur le board joueur (InstanceId → MinionTokenVisual)
        private Dictionary<string, MinionTokenVisual> _playerTokens = new Dictionary<string, MinionTokenVisual>();
        private List<MinionState> _playerBoard = new List<MinionState>();

        // Tokens sur le board adverse
        private Dictionary<string, MinionTokenVisual> _opponentTokens = new Dictionary<string, MinionTokenVisual>();

        /// <summary>Centre du board joueur (dépend de la phase active)</summary>
        public Vector3 PlayerBoardCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                if (surface == null) return Vector3.zero;
                var layout = FindAnyObjectByType<PhaseLayoutManager>();
                bool isCombat = layout != null && layout.IsCombat;
                return surface.GetPlayerBoardCenter(isCombat);
            }
        }

        /// <summary>Centre du board adversaire (combat only)</summary>
        public Vector3 OpponentBoardCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                if (surface == null) return Vector3.zero;
                return surface.GetOpponentBoardCenter();
            }
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnBoardUpdated += HandleBoardUpdated;
            Debug.Log("[BoardManager] Abonné à OnBoardUpdated");
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnBoardUpdated -= HandleBoardUpdated;
        }

        // =====================================================
        // HANDLER SERVEUR
        // =====================================================

        private void HandleBoardUpdated(List<MinionState> minions)
        {
            Debug.Log($"[BoardManager] OnBoardUpdated reçu : {minions.Count} minions, center={PlayerBoardCenter}, spacing={slotSpacing}, arc={arcAmount}");
            _playerBoard = minions;
            UpdatePlayerBoard(minions);
        }

        // =====================================================
        // MISE À JOUR DU BOARD
        // =====================================================

        private void UpdatePlayerBoard(List<MinionState> minions)
        {
            int count = minions.Count;
            var positions = BoardLayout.CalculatePositions(count, PlayerBoardCenter, slotSpacing, arcAmount);

            // Retirer les tokens qui ne sont plus sur le board
            var currentIds = new HashSet<string>(minions.Select(m => m.InstanceId));
            var toRemove = _playerTokens.Keys.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (_playerTokens.TryGetValue(id, out var token) && token != null)
                {
                    token.AnimateDeath();
                }
                _playerTokens.Remove(id);
            }

            // Placer/déplacer les tokens
            for (int i = 0; i < count; i++)
            {
                var minionState = minions[i];
                var targetPos = positions[i];

                if (!_playerTokens.TryGetValue(minionState.InstanceId, out var token) || token == null)
                {
                    // Créer un nouveau token
                    token = CardFactory.Instance?.CreateToken(minionState, showTier: false);
                    if (token == null) continue;

                    token.transform.position = targetPos;
                    token.SetBasePosition(targetPos);
                    token.AnimateSpawn(i * 0.05f);

                    _playerTokens[minionState.InstanceId] = token;
                }
                else
                {
                    // Déplacer le token existant
                    Debug.Log($"[BoardManager] Token {minionState.Name} → pos {targetPos}");
                    token.transform.DOKill();
                    token.transform.DOMove(targetPos, repositionDuration).SetEase(repositionEase);
                    token.SetBasePosition(targetPos);
                    token.UpdateStats(minionState.Attack, minionState.Health);
                }
            }

            Debug.Log($"[BoardManager] Board joueur mis à jour : {count} minions");
        }

        // =====================================================
        // API POUR LE DRAG & DROP
        // =====================================================

        /// <summary>
        /// Retourne l'index d'insertion le plus proche pour un drop à une position donnée.
        /// </summary>
        public int GetInsertionIndex(Vector3 worldPosition)
        {
            int count = Mathf.Min(_playerBoard.Count + 1, maxSlots);
            var positions = BoardLayout.CalculatePositions(count, PlayerBoardCenter, slotSpacing, arcAmount);

            float bestDist = float.MaxValue;
            int bestIndex = count - 1;

            for (int i = 0; i < count; i++)
            {
                float dist = Vector3.Distance(worldPosition, positions[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Vérifie si un drop est dans la zone du board joueur.
        /// </summary>
        public bool IsInBoardZone(Vector3 worldPosition, float tolerance = 3f)
        {
            var center = PlayerBoardCenter;
            return Mathf.Abs(worldPosition.z - center.z) < tolerance;
        }

        /// <summary>
        /// Retourne le token d'un minion par son InstanceId.
        /// </summary>
        public MinionTokenVisual GetPlayerToken(string instanceId)
        {
            _playerTokens.TryGetValue(instanceId, out var token);
            return token;
        }

        /// <summary>Nombre de minions sur le board joueur</summary>
        public int PlayerMinionCount => _playerBoard.Count;
    }
}
