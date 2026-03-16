using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Gère le plateau de jeu : création des slots, placement des minions,
    /// réorganisation dynamique et synchronisation avec le serveur.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer le prefab BoardSlot (voir BoardSlot.cs pour les instructions)
    /// 2. Sélectionner le GameObject "BoardManager" dans la Hierarchy
    /// 3. Glisser le prefab BoardSlot depuis Assets/_Project/Prefabs/ vers "Slot Prefab"
    ///    ⚠️ Ce champ est OBLIGATOIRE — le board ne fonctionnera pas sans prefab
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        [Header("Board Settings")]
        [Tooltip("Espacement entre les slots (adapté aux tokens compacts ~1.2u)")]
        [SerializeField] private float slotSpacing = 1.6f;

        [Tooltip("Courbure de l'arc (0 = ligne droite)")]
        [SerializeField] private float arcAmount = 0.5f;

        [Tooltip("Nombre max de slots par côté")]
        [SerializeField] private int maxSlots = 7;

        [Header("References")]
        [Tooltip("Prefab d'un slot (Quad + BoardSlot component + Material)")]
        [SerializeField] private GameObject slotPrefab;

        [Header("Animation")]
        [SerializeField] private float repositionDuration = 0.3f;
        [SerializeField] private Ease repositionEase = Ease.OutQuad;

        // Slots instanciés
        private List<BoardSlot> _playerSlots = new List<BoardSlot>();
        private List<BoardSlot> _opponentSlots = new List<BoardSlot>();

        // Minions actuellement sur le board (InstanceId → GameObject)
        private Dictionary<string, GameObject> _playerMinions = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> _opponentMinions = new Dictionary<string, GameObject>();

        /// <summary>Liste des slots joueur</summary>
        public IReadOnlyList<BoardSlot> PlayerSlots => _playerSlots;

        /// <summary>Liste des slots adversaire</summary>
        public IReadOnlyList<BoardSlot> OpponentSlots => _opponentSlots;

        /// <summary>Centre du board joueur (dépend de la phase active)</summary>
        private Vector3 PlayerBoardCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                if (surface == null) return new Vector3(0.9f, 0.01f, 2.26f);
                var layout = FindAnyObjectByType<PhaseLayoutManager>();
                bool isCombat = layout != null && layout.IsCombat;
                return surface.GetPlayerBoardCenter(isCombat);
            }
        }

        /// <summary>Centre du board adversaire (combat only)</summary>
        private Vector3 OpponentBoardCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                if (surface == null) return new Vector3(0.9f, 0.01f, 4.91f);
                return surface.GetOpponentBoardCenter();
            }
        }

        private void Awake()
        {
            if (slotPrefab == null)
            {
                Debug.LogError("[BoardManager] slotPrefab non assigné ! Glisser le prefab BoardSlot dans l'Inspector.");
                return;
            }

            CreateSlots(_playerSlots, PlayerBoardCenter);
            CreateSlots(_opponentSlots, OpponentBoardCenter);
        }

        // =====================================================
        // CRÉATION DES SLOTS
        // =====================================================

        private void CreateSlots(List<BoardSlot> slotList, Vector3 center)
        {
            var positions = BoardLayout.CalculatePositions(maxSlots, center, slotSpacing, arcAmount);

            for (int i = 0; i < maxSlots; i++)
            {
                var slotObj = Instantiate(slotPrefab, positions[i], Quaternion.identity, transform);

                var slot = slotObj.GetComponent<BoardSlot>();
                if (slot == null)
                {
                    Debug.LogError("[BoardManager] Le prefab BoardSlot n'a pas de composant BoardSlot !");
                    continue;
                }

                slot.Setup(i);
                slotObj.SetActive(false); // Masqué par défaut, activé selon le nombre d'unités
                slotList.Add(slot);
            }
        }

        // =====================================================
        // PLACEMENT ET REPOSITIONNEMENT
        // =====================================================

        /// <summary>
        /// Met à jour le board joueur à partir de l'état serveur.
        /// Repositionne tous les slots et minions avec animation.
        /// </summary>
        public Tween UpdatePlayerBoard(List<MinionState> minions, System.Func<MinionState, GameObject> getOrCreateVisual)
        {
            return UpdateBoard(_playerSlots, _playerMinions, minions, PlayerBoardCenter, getOrCreateVisual);
        }

        /// <summary>
        /// Met à jour le board adversaire (pour le combat).
        /// </summary>
        public Tween UpdateOpponentBoard(List<MinionState> minions, System.Func<MinionState, GameObject> getOrCreateVisual)
        {
            return UpdateBoard(_opponentSlots, _opponentMinions, minions, OpponentBoardCenter, getOrCreateVisual);
        }

        private Tween UpdateBoard(
            List<BoardSlot> slots,
            Dictionary<string, GameObject> minionMap,
            List<MinionState> minions,
            Vector3 center,
            System.Func<MinionState, GameObject> getOrCreateVisual)
        {
            int count = minions.Count;
            var positions = BoardLayout.CalculatePositions(count, center, slotSpacing, arcAmount);

            // Activer/désactiver les slots selon le nombre
            for (int i = 0; i < slots.Count; i++)
                slots[i].gameObject.SetActive(i < count);

            // Déplacer les slots aux bonnes positions
            var sequence = DOTween.Sequence();

            // Retirer les minions qui ne sont plus sur le board
            var currentIds = new HashSet<string>(minions.Select(m => m.InstanceId));
            var toRemove = minionMap.Keys.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (minionMap.TryGetValue(id, out var obj) && obj != null)
                    obj.SetActive(false); // Retour au pool plus tard
                minionMap.Remove(id);
            }

            // Placer/déplacer les minions
            for (int i = 0; i < count; i++)
            {
                var minionState = minions[i];
                var targetPos = positions[i];

                // Déplacer le slot
                slots[i].transform.DOMove(targetPos, repositionDuration).SetEase(repositionEase);

                // Obtenir ou créer le visuel du minion
                if (!minionMap.TryGetValue(minionState.InstanceId, out var minionObj) || minionObj == null)
                {
                    minionObj = getOrCreateVisual(minionState);
                    minionMap[minionState.InstanceId] = minionObj;
                }

                // Animer le minion vers sa position (légèrement au-dessus du slot)
                var minionTargetPos = targetPos + Vector3.up * 0.01f;
                sequence.Join(
                    minionObj.transform.DOMove(minionTargetPos, repositionDuration)
                        .SetEase(repositionEase)
                );

                minionObj.SetActive(true);
                slots[i].Place(minionObj.transform);
            }

            return sequence;
        }

        // =====================================================
        // RECHERCHE DE SLOT
        // =====================================================

        /// <summary>
        /// Trouve le slot joueur le plus proche d'une position world.
        /// Utilisé par le drag & drop pour déterminer où poser un minion.
        /// </summary>
        public BoardSlot GetNearestPlayerSlot(Vector3 worldPosition, float maxDistance = 2f)
        {
            BoardSlot nearest = null;
            float bestDist = maxDistance;

            foreach (var slot in _playerSlots)
            {
                if (!slot.gameObject.activeSelf) continue;
                float dist = Vector3.Distance(worldPosition, slot.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = slot;
                }
            }

            // Aussi vérifier le prochain slot libre (pour ajouter un minion)
            int activeCount = _playerSlots.Count(s => s.gameObject.activeSelf);
            if (activeCount < maxSlots)
            {
                var nextSlot = _playerSlots[activeCount];
                var positions = BoardLayout.CalculatePositions(activeCount + 1, PlayerBoardCenter, slotSpacing, arcAmount);
                float dist = Vector3.Distance(worldPosition, positions[activeCount]);
                if (dist < bestDist)
                    nearest = nextSlot;
            }

            return nearest;
        }

        /// <summary>
        /// Retourne le slot qui contient un minion donné (par InstanceId).
        /// </summary>
        public BoardSlot GetSlotByMinionId(string instanceId)
        {
            if (!_playerMinions.TryGetValue(instanceId, out var minionObj)) return null;

            foreach (var slot in _playerSlots)
            {
                if (slot.OccupiedBy == minionObj.transform)
                    return slot;
            }

            return null;
        }

        /// <summary>
        /// Retourne l'index d'insertion pour un drop à une position world donnée.
        /// </summary>
        public int GetInsertionIndex(Vector3 worldPosition)
        {
            int activeCount = _playerSlots.Count(s => s.gameObject.activeSelf);
            int count = Mathf.Min(activeCount + 1, maxSlots);
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

        // =====================================================
        // HIGHLIGHT (pour le drag & drop)
        // =====================================================

        /// <summary>Montre les highlights sur tous les slots joueur</summary>
        public void ShowAllHighlights(bool isValid)
        {
            foreach (var slot in _playerSlots)
                slot.ShowHighlight(isValid);
        }

        /// <summary>Cache tous les highlights</summary>
        public void HideAllHighlights()
        {
            foreach (var slot in _playerSlots)
                slot.HideHighlight();
        }
    }
}
