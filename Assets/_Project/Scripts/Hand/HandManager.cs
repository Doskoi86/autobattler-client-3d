using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Hand
{
    /// <summary>
    /// Gère la zone de main du joueur : affichage des cartes achetées
    /// en attente d'être posées sur le board.
    ///
    /// Le positionnement est basé sur la HandZone du BoardSurface.
    /// Déplacer la zone dans l'éditeur déplace automatiquement les cartes.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "HandManager" dans la Hierarchy
    /// 2. Add Component → HandManager
    /// 3. Les cartes apparaîtront centrées sur la HandZone du BoardSurface
    /// </summary>
    public class HandManager : MonoBehaviour
    {
        public static HandManager Instance { get; private set; }

        [Header("Hand Layout")]
        [Tooltip("Espacement entre les cartes en main")]
        [SerializeField] private float handSpacing = 1.6f;

        [Header("Animation")]
        [SerializeField] private float repositionDuration = 0.3f;
        [SerializeField] private Ease repositionEase = Ease.OutQuad;

        // Cartes actuellement en main
        private List<CardVisual> _handCards = new List<CardVisual>();
        private Dictionary<string, CardVisual> _cardMap = new Dictionary<string, CardVisual>();

        public IReadOnlyList<CardVisual> HandCards => _handCards;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHandUpdated += HandleHandUpdated;
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHandUpdated -= HandleHandUpdated;
        }

        // =====================================================
        // ZONE DE RÉFÉRENCE
        // =====================================================

        /// <summary>Position centrale de la zone main (depuis le BoardSurface)</summary>
        private Vector3 HandCenter
        {
            get
            {
                var zone = BoardSurface.Instance?.HandZone;
                return zone != null ? zone.position : new Vector3(0f, 0.1f, -3.5f);
            }
        }

        // =====================================================
        // HANDLER SERVEUR
        // =====================================================

        private void HandleHandUpdated(List<MinionState> minions)
        {
            var currentIds = new HashSet<string>(minions.Select(m => m.InstanceId));
            var toRemove = _handCards
                .Where(c => c != null && !currentIds.Contains(c.Data?.InstanceId ?? ""))
                .ToList();

            foreach (var card in toRemove)
            {
                _cardMap.Remove(card.Data?.InstanceId ?? "");
                card.gameObject.SetActive(false);
                _handCards.Remove(card);
            }

            foreach (var minion in minions)
            {
                if (_cardMap.ContainsKey(minion.InstanceId)) continue;
                if (CardFactory.Instance == null) continue;

                var card = CardFactory.Instance.CreateCard(minion);
                if (card == null) continue;

                _handCards.Add(card);
                _cardMap[minion.InstanceId] = card;
            }

            RepositionCards();
        }

        // =====================================================
        // POSITIONNEMENT
        // =====================================================

        private void RepositionCards()
        {
            var center = HandCenter;
            float totalWidth = (_handCards.Count - 1) * handSpacing;
            float startX = center.x - totalWidth / 2f;

            for (int i = 0; i < _handCards.Count; i++)
            {
                if (_handCards[i] == null) continue;
                var targetPos = new Vector3(startX + i * handSpacing, center.y, center.z);
                _handCards[i].transform.DOMove(targetPos, repositionDuration).SetEase(repositionEase);
                _handCards[i].SetBasePosition(targetPos);
            }
        }

        // =====================================================
        // API PUBLIQUE
        // =====================================================

        public CardVisual GetCard(string instanceId)
        {
            _cardMap.TryGetValue(instanceId, out var card);
            return card;
        }

        public void RemoveCard(string instanceId)
        {
            if (_cardMap.TryGetValue(instanceId, out var card))
            {
                _cardMap.Remove(instanceId);
                _handCards.Remove(card);
                RepositionCards();
            }
        }
    }
}
