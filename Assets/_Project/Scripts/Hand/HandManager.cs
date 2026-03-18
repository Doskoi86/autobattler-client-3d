using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;
using AutoBattler.Client.Utils;

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
        [SerializeField] private float handSpacing = 1.1f;

        [Header("Animation")]
        [SerializeField] private float repositionDuration = 0.3f;
        [SerializeField] private Ease repositionEase = Ease.OutQuad;

        [Header("Sorting")]
        [Tooltip("Offset de sorting order entre chaque carte")]
        [SerializeField] private int sortingOrderStep = 15;

        [Header("Hover")]
        [Tooltip("Décalage Z (vers le haut de l'écran) quand la carte est survolée")]
        [SerializeField] private float hoverLiftZ = 2f;
        [Tooltip("Scale de la carte survolée")]
        [SerializeField] private float hoverScale = 1.3f;
        [Tooltip("Durée de l'animation hover")]
        [SerializeField] private float hoverDuration = 0.15f;
        [Tooltip("Sorting order bonus pour la carte survolée (au-dessus des autres)")]
        [SerializeField] private int hoverSortingBoost = 100;

        [Header("Fan Layout")]
        [Tooltip("Rotation Y max des cartes aux extrémités (en degrés)")]
        [SerializeField] private float fanMaxRotation = 3f;

        [Tooltip("Nombre de cartes minimum pour activer la rotation en éventail")]
        [SerializeField] private int fanMinCards = 4;

        [Tooltip("Courbure de l'arc (les cartes du centre montent en Z)")]
        [SerializeField] private float fanArcHeight = 0.5f;

        [Tooltip("Nombre de cartes minimum pour activer l'arc en Z")]
        [SerializeField] private int arcMinCards = 3;

        [Tooltip("Espacement minimum entre les cartes quand la main est pleine")]
        [SerializeField] private float minHandSpacing = 0.8f;

        [Tooltip("Nombre max de cartes en main")]
        [SerializeField] private int maxHandCards = 10;

        [Tooltip("Marge extérieure de détection hover (proportion du handSpacing)")]
        [SerializeField] private float hoverOuterMargin = 0.8f;

        [Tooltip("Tolérance verticale (Z) pour détecter le hover dans la zone de la main")]
        [SerializeField] private float hoverZoneTolerance = 3f;

        // Cartes actuellement en main
        private List<CardVisual> _handCards = new List<CardVisual>();
        private Dictionary<string, CardVisual> _cardMap = new Dictionary<string, CardVisual>();

        // Sorting orders de base par renderer (sauvegardés à la création, jamais modifiés)
        private Dictionary<Renderer, int> _baseSortingOrders = new Dictionary<Renderer, int>();

        // Hover
        private CardVisual _hoveredCard;
        private Camera _mainCamera;

        public IReadOnlyList<CardVisual> HandCards => _handCards;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (_mainCamera == null || Mouse.current == null || DragDropController.IsDragging || _handCards.Count == 0)
            {
                if (_hoveredCard != null) UnhoverCard();
                return;
            }

            // Convertir la position souris en position world sur le plan de drag
            var ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var dragPlane = new Plane(Vector3.up, new Vector3(0f, 0.5f, 0f));
            if (!dragPlane.Raycast(ray, out float dist)) return;
            var mouseWorld = ray.GetPoint(dist);

            // Vérifier si la souris est dans la zone de la main (en Z)
            var handCenter = HandCenter;
            float handZoneHalfHeight = hoverZoneTolerance;
            if (Mathf.Abs(mouseWorld.z - handCenter.z) > handZoneHalfHeight)
            {
                if (_hoveredCard != null) UnhoverCard();
                return;
            }

            // Trouver la carte dont la base position X est la plus proche de la souris
            // Zone de chaque carte = du milieu gauche au milieu droit (nearest neighbor)
            CardVisual closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < _handCards.Count; i++)
            {
                if (_handCards[i] == null) continue;
                float dx = Mathf.Abs(mouseWorld.x - _handCards[i].BasePosition.x);
                if (dx < closestDist)
                {
                    closestDist = dx;
                    closest = _handCards[i];
                }
            }

            // Bord extérieur : la carte la plus à gauche/droite a une zone étendue
            // (demi-carte au-delà de son centre)
            float outerMargin = handSpacing * hoverOuterMargin;
            bool inRange = closest != null && closestDist <= outerMargin;

            if (closest != null && inRange)
            {
                if (closest != _hoveredCard)
                {
                    if (_hoveredCard != null) UnhoverCard();
                    HoverCard(closest);
                }
            }
            else
            {
                if (_hoveredCard != null) UnhoverCard();
            }
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

        /// <summary>Position centrale de la zone main (dépend de la phase active)</summary>
        private Vector3 HandCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                if (surface == null) return Vector3.zero;
                var layout = FindAnyObjectByType<PhaseLayoutManager>();
                bool isCombat = layout != null && layout.IsCombat;
                return surface.GetHandCenter(isCombat);
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

                // Sauvegarder les sorting orders de base (depuis le prefab)
                SaveBaseSortingOrders(card.gameObject);
            }

            RepositionCards();
        }

        // =====================================================
        // POSITIONNEMENT
        // =====================================================

        private void RepositionCards()
        {
            int count = _handCards.Count;
            if (count == 0) return;

            var center = HandCenter;

            // Espacement adaptatif : diminue quand il y a plus de cartes
            float currentSpacing = count <= 3
                ? handSpacing
                : Mathf.Lerp(handSpacing, minHandSpacing, (float)(count - 3) / (maxHandCards - 3));

            float totalWidth = (count - 1) * currentSpacing;
            float startX = center.x - totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                if (_handCards[i] == null) continue;

                // Arc parabolique : les cartes du centre montent en Z
                float arcOffset = 0f;
                if (count >= arcMinCards && count > 1)
                {
                    float arcNorm = (2f * i / (count - 1)) - 1f;
                    arcOffset = (1f - arcNorm * arcNorm) * fanArcHeight;
                }
                var targetPos = new Vector3(startX + i * currentSpacing, center.y, center.z + arcOffset);
                _handCards[i].transform.DOKill();
                _handCards[i].transform.DOMove(targetPos, repositionDuration).SetEase(repositionEase);
                _handCards[i].SetBasePosition(targetPos);

                // Rotation en éventail
                float yRotation = 0f;
                if (count >= fanMinCards && count > 1)
                {
                    // Normaliser la position entre -1 (gauche) et +1 (droite)
                    float normalized = (2f * i / (count - 1)) - 1f;
                    yRotation = normalized * fanMaxRotation;
                }
                var targetRotation = Quaternion.Euler(90f, yRotation, 0f);
                _handCards[i].transform.DORotateQuaternion(targetRotation, repositionDuration)
                    .SetEase(repositionEase);

                // Sorting order croissant : la carte la plus à droite est au-dessus
                SetCardSortingOffset(_handCards[i].gameObject, i * sortingOrderStep);
            }
        }

        /// <summary>
        /// Sauvegarde les sorting orders de base d'une carte (appelé une seule fois à la création).
        /// </summary>
        private void SaveBaseSortingOrders(GameObject card)
        {
            var renderers = card.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                _baseSortingOrders[r] = r.sortingOrder;
            }
        }

        /// <summary>
        /// Applique un offset de sorting order à tous les renderers d'une carte.
        /// L'offset est ajouté aux ordres de base sauvegardés (pas aux ordres actuels).
        /// </summary>
        private void SetCardSortingOffset(GameObject card, int offset)
        {
            var renderers = card.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (_baseSortingOrders.TryGetValue(r, out int baseOrder))
                    r.sortingOrder = baseOrder + offset;
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
                if (card == _hoveredCard) _hoveredCard = null;
                _cardMap.Remove(instanceId);
                _handCards.Remove(card);
                RepositionCards();
            }
        }

        // =====================================================
        // HOVER
        // =====================================================

        private void HoverCard(CardVisual card)
        {
            _hoveredCard = card;
            var basePos = card.BasePosition;
            var hoverPos = new Vector3(basePos.x, basePos.y, basePos.z + hoverLiftZ);

            card.transform.DOKill();
            card.transform.DOMove(hoverPos, hoverDuration).SetEase(Ease.OutBack);
            card.transform.DOScale(Vector3.one * CardFactory.Instance.CardScale * hoverScale, hoverDuration).SetEase(Ease.OutBack);
            // Redresser la carte (rotation Y = 0) pendant le hover
            card.transform.DORotateQuaternion(Quaternion.Euler(90f, 0f, 0f), hoverDuration).SetEase(Ease.OutQuad);

            // Passer au-dessus des autres cartes en main
            SortingLayerHelper.SetSortingLayer(card.gameObject, "Drag");
            SetCardSortingOffset(card.gameObject, 0);
        }

        private void UnhoverCard()
        {
            if (_hoveredCard == null) return;
            var card = _hoveredCard;
            _hoveredCard = null;

            int index = _handCards.IndexOf(card);

            card.transform.DOKill();
            card.transform.DOMove(card.BasePosition, hoverDuration).SetEase(Ease.OutQuad);
            card.transform.DOScale(Vector3.one * CardFactory.Instance.CardScale, hoverDuration).SetEase(Ease.OutQuad);

            // Remettre la rotation éventail
            float yRotation = 0f;
            int count = _handCards.Count;
            if (count >= fanMinCards && count > 1 && index >= 0)
            {
                float normalized = (2f * index / (count - 1)) - 1f;
                yRotation = normalized * fanMaxRotation;
            }
            card.transform.DORotateQuaternion(Quaternion.Euler(90f, yRotation, 0f), hoverDuration).SetEase(Ease.OutQuad);

            // Restaurer le layer et sorting order normal
            SortingLayerHelper.SetSortingLayer(card.gameObject, "Hand");
            if (index >= 0)
                SetCardSortingOffset(card.gameObject, index * sortingOrderStep);
        }

        /// <summary>
        /// Force le unhover immédiat (sans animation) — appelé avant un drag.
        /// Remet la carte à sa position/scale/sorting de base.
        /// </summary>
        public void ForceUnhover()
        {
            if (_hoveredCard == null) return;
            var card = _hoveredCard;
            _hoveredCard = null;

            int index = _handCards.IndexOf(card);

            card.transform.DOKill();
            card.transform.position = card.BasePosition;
            card.transform.localScale = Vector3.one * CardFactory.Instance.CardScale;
            // Garder la rotation droite (0) pour le drag
            card.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            SortingLayerHelper.SetSortingLayer(card.gameObject, "Hand");
            if (index >= 0)
                SetCardSortingOffset(card.gameObject, index * sortingOrderStep);
        }
    }
}
