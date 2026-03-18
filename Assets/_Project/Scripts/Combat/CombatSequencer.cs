using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Board;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network.Protocol;
using AutoBattler.Client.UI;
using AutoBattler.Client.Utils;

namespace AutoBattler.Client.Combat
{
    /// <summary>
    /// Orchestre toute la phase combat côté client.
    ///
    /// Reçoit les events du serveur (OnCombatReplay), installe les boards
    /// (joueur repositionné, adversaire créé), joue chaque event comme
    /// une animation séquentielle, puis affiche le résultat (Victoire/Défaite/Égalité).
    ///
    /// Le serveur envoie les events dans l'ordre exact — le client ne simule rien,
    /// il se contente de rejouer le combat visuellement.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "CombatSequencer" dans la Hierarchy
    /// 2. Add Component → CombatSequencer
    /// 3. Glisser BoardManager dans le champ "Board Manager"
    /// </summary>
    public class CombatSequencer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager;

        [Header("Timing")]
        [Tooltip("Délai avant de setup les boards (temps pour l'overlay 'Combat !')")]
        [SerializeField] private float setupDelay = 2.2f;

        [Tooltip("Délai entre chaque event de combat")]
        [SerializeField] private float delayBetweenEvents = 0.4f;

        [Tooltip("Délai avant d'afficher le résultat")]
        [SerializeField] private float delayBeforeResult = 0.8f;

        [Tooltip("Durée d'affichage du résultat")]
        [SerializeField] private float resultDisplayDuration = 2.5f;

        [Header("Spawn")]
        [Tooltip("Délai stagger entre chaque token adverse au spawn")]
        [SerializeField] private float opponentSpawnStagger = 0.08f;

        // État public
        /// <summary>True si le combat est en cours d'animation.</summary>
        public static bool IsPlayingCombat { get; private set; }

        // État interne
        private CombatResultData _pendingResult;

        // Tracking des tokens de combat (par InstanceId)
        private Dictionary<string, MinionTokenVisual> _combatTokens = new();
        private List<string> _playerAliveIds = new();
        private List<string> _opponentAliveIds = new();
        private HashSet<string> _playerSideHistory = new(); // track player-side même après mort
        private HashSet<string> _spawnedTokenIds = new();   // tokens créés pendant le combat (à nettoyer)

        // Stats originales des tokens joueur (sauvegardées avant le combat, restaurées après)
        private Dictionary<string, (int attack, int health)> _savedPlayerStats = new();

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnCombatReplay += HandleCombatReplay;
            server.OnCombatResult += HandleCombatResult;
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnCombatReplay -= HandleCombatReplay;
            server.OnCombatResult -= HandleCombatResult;
        }

        // =====================================================
        // HANDLERS SERVEUR
        // =====================================================

        private void HandleCombatReplay(List<CombatEventData> events,
            List<BoardSnapshot> playerSnap, List<BoardSnapshot> opponentSnap)
        {
            Debug.Log($"[CombatSequencer] CombatReplay reçu : {events.Count} events, " +
                      $"player={playerSnap.Count}, opponent={opponentSnap.Count}");
            _pendingResult = null;
            StartCoroutine(PlayCombatSequence(events, playerSnap, opponentSnap));
        }

        private void HandleCombatResult(string opponentId, bool didWin, int damage, string log)
        {
            Debug.Log($"[CombatSequencer] CombatResult : {(didWin ? "Victoire" : (damage == 0 ? "Égalité" : "Défaite"))} — {damage} dmg");
            _pendingResult = new CombatResultData
            {
                OpponentId = opponentId,
                DidWin = didWin,
                Damage = damage,
                Log = log
            };
        }

        // =====================================================
        // SÉQUENCE PRINCIPALE
        // =====================================================

        private IEnumerator PlayCombatSequence(List<CombatEventData> events,
            List<BoardSnapshot> playerSnap, List<BoardSnapshot> opponentSnap)
        {
            IsPlayingCombat = true;

            // Bufferiser les dégâts héros pour ne pas spoiler le résultat
            HeroPortraitDisplay.Instance?.StartCombatBuffering();

            // 1. Attendre la fin de l'overlay "Combat !"
            yield return new WaitForSeconds(setupDelay);

            // 2. Installer les boards de combat
            SetupCombatBoards(playerSnap, opponentSnap);
            yield return new WaitForSeconds(0.6f);

            // 3. Jouer chaque event séquentiellement
            for (int i = 0; i < events.Count; i++)
            {
                yield return PlayEvent(events[i]);

                // Petit délai entre les events (sauf le dernier)
                if (i < events.Count - 1)
                    yield return new WaitForSeconds(delayBetweenEvents);
            }

            // 4. Attendre que le résultat soit reçu (si pas encore)
            float waitTimeout = 0f;
            while (_pendingResult == null && waitTimeout < 5f)
            {
                waitTimeout += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(delayBeforeResult);

            // 5. Appliquer les dégâts héros bufferisés (juste avant le résultat)
            HeroPortraitDisplay.Instance?.FlushCombatBuffer();
            yield return new WaitForSeconds(0.5f);

            // 6. Afficher le résultat via le PhaseTransitionOverlay (même Canvas)
            if (_pendingResult != null && PhaseTransitionOverlay.Instance != null)
            {
                PhaseTransitionOverlay.Instance.ShowResult(_pendingResult);
                yield return new WaitForSeconds(resultDisplayDuration);
                PhaseTransitionOverlay.Instance.HideResult();
                yield return new WaitForSeconds(0.3f);
            }

            // 6. Nettoyer et signaler la fin
            CleanupCombat();
            IsPlayingCombat = false;

            GameManager.Instance?.Server?.AnimationDoneAsync();
            Debug.Log("[CombatSequencer] Combat terminé — AnimationDone envoyé");
        }

        // =====================================================
        // SETUP DES BOARDS DE COMBAT
        // =====================================================

        private void SetupCombatBoards(List<BoardSnapshot> playerSnap, List<BoardSnapshot> opponentSnap)
        {
            _combatTokens.Clear();
            _playerAliveIds.Clear();
            _opponentAliveIds.Clear();
            _playerSideHistory.Clear();
            _spawnedTokenIds.Clear();
            _savedPlayerStats.Clear();

            // --- Board joueur : repositionner les tokens existants vers la position combat ---
            var playerCenter = BoardSurface.Instance.GetPlayerBoardCenter(isCombat: true);
            var playerPositions = BoardLayout.CalculatePositions(
                playerSnap.Count, playerCenter, boardManager.SlotSpacing, boardManager.ArcAmount);

            for (int i = 0; i < playerSnap.Count; i++)
            {
                var snap = playerSnap[i];
                var token = boardManager.GetPlayerToken(snap.InstanceId);

                if (token != null)
                {
                    // Sauvegarder les stats AVANT de les modifier pour le combat
                    _savedPlayerStats[snap.InstanceId] = (token.Data.Attack, token.Data.Health);

                    token.transform.DOKill();
                    token.transform.DOMove(playerPositions[i], 0.5f).SetEase(Ease.OutQuad);
                    token.SetBasePosition(playerPositions[i]);
                    token.UpdateStats(snap.Attack, snap.Health);
                    token.DragEnabled = false;
                    _combatTokens[snap.InstanceId] = token;
                }
                else
                {
                    // Fallback : créer un token si pas trouvé dans BoardManager
                    token = CreateCombatToken(snap, playerPositions[i]);
                    if (token != null)
                    {
                        _combatTokens[snap.InstanceId] = token;
                        _spawnedTokenIds.Add(snap.InstanceId);
                    }
                }

                _playerAliveIds.Add(snap.InstanceId);
                _playerSideHistory.Add(snap.InstanceId);
            }

            // --- Board adverse : créer tous les tokens ---
            var opponentCenter = BoardSurface.Instance.GetOpponentBoardCenter();
            var opponentPositions = BoardLayout.CalculatePositions(
                opponentSnap.Count, opponentCenter, boardManager.SlotSpacing, boardManager.ArcAmount);

            for (int i = 0; i < opponentSnap.Count; i++)
            {
                var snap = opponentSnap[i];
                var token = CreateCombatToken(snap, opponentPositions[i]);
                if (token != null)
                {
                    token.AnimateSpawn(i * opponentSpawnStagger);
                    _combatTokens[snap.InstanceId] = token;
                    _spawnedTokenIds.Add(snap.InstanceId);
                }
                _opponentAliveIds.Add(snap.InstanceId);
            }

            Debug.Log($"[CombatSequencer] Boards installés : {_playerAliveIds.Count} joueur, {_opponentAliveIds.Count} adversaire");
        }

        /// <summary>
        /// Crée un token de combat (adversaire ou fallback joueur).
        /// Ces tokens sont TEMPORAIRES et seront détruits à la fin du combat.
        /// </summary>
        private MinionTokenVisual CreateCombatToken(BoardSnapshot snap, Vector3 position)
        {
            var data = new MinionState
            {
                InstanceId = snap.InstanceId,
                Name = snap.Name,
                Attack = snap.Attack,
                Health = snap.Health,
                Tier = snap.Tier,
                Keywords = "",
                IsGolden = false
            };

            var token = CardFactory.Instance?.CreateToken(data, showTier: false, sortingLayer: "Board");
            if (token == null) return null;

            token.transform.position = position;
            token.SetBasePosition(position);
            token.DragEnabled = false;
            return token;
        }

        // =====================================================
        // LECTURE DES EVENTS
        // =====================================================

        private IEnumerator PlayEvent(CombatEventData evt)
        {
            switch (evt.Type)
            {
                case "Attack":
                    yield return PlayAttack(evt);
                    break;

                case "CleaveHit":
                    yield return PlayCleaveHit(evt);
                    break;

                case "DeathrattleTrigger":
                    yield return PlayDeathrattle(evt);
                    break;

                case "RebornTrigger":
                    yield return PlayReborn(evt);
                    break;

                default:
                    Debug.LogWarning($"[CombatSequencer] Type d'event inconnu : {evt.Type}");
                    break;
            }
        }

        // =====================================================
        // ATTACK
        // =====================================================

        private IEnumerator PlayAttack(CombatEventData evt)
        {
            _combatTokens.TryGetValue(evt.AttackerId, out var attacker);
            _combatTokens.TryGetValue(evt.TargetId, out var target);

            if (attacker == null || target == null)
            {
                Debug.LogWarning($"[CombatSequencer] Attack : token manquant — " +
                    $"attacker={evt.AttackerName}({evt.AttackerId}), target={evt.TargetName}({evt.TargetId})");
                yield break;
            }

            // Passer l'attaquant au-dessus de tout pendant l'animation
            SortingLayerHelper.SetSortingLayer(attacker.gameObject, "Drag");

            // L'attaquant fonce sur la cible
            attacker.AnimateAttack(target.transform.position);

            // Attendre l'impact (forward duration + petit délai)
            yield return new WaitForSeconds(attacker.AttackForwardDuration + 0.08f);

            // Impact : dégâts sur la cible
            if (evt.DivineShieldPopped)
            {
                target.AnimateDivineShieldPop();
            }
            else
            {
                target.AnimateDamage(evt.TargetHealthAfter);
            }

            // Contre-attaque : dégâts sur l'attaquant
            if (evt.TargetAttack > 0)
            {
                attacker.AnimateDamage(evt.AttackerHealthAfter);
            }

            // Attendre la fin de l'animation de retour
            yield return new WaitForSeconds(attacker.AttackReturnDuration + 0.15f);

            // Remettre l'attaquant dans son layer normal
            if (attacker != null && attacker.gameObject.activeSelf)
                SortingLayerHelper.SetSortingLayer(attacker.gameObject, "Board");

            // Morts (target d'abord, comme le vrai Hearthstone)
            if (evt.TargetDied)
            {
                yield return AnimateDeath(evt.TargetId);
            }
            if (evt.AttackerDied)
            {
                yield return AnimateDeath(evt.AttackerId);
            }
        }

        // =====================================================
        // CLEAVE HIT (dégât adjacent)
        // =====================================================

        private IEnumerator PlayCleaveHit(CombatEventData evt)
        {
            _combatTokens.TryGetValue(evt.TargetId, out var target);
            if (target == null) yield break;

            if (evt.DivineShieldPopped)
            {
                target.AnimateDivineShieldPop();
            }
            else
            {
                target.AnimateDamage(evt.TargetHealthAfter);
            }

            yield return new WaitForSeconds(0.25f);

            if (evt.TargetDied)
            {
                yield return AnimateDeath(evt.TargetId);
            }
        }

        // =====================================================
        // DEATHRATTLE
        // =====================================================

        private IEnumerator PlayDeathrattle(CombatEventData evt)
        {
            if (string.IsNullOrEmpty(evt.TokenName)) yield break;

            // Déterminer le côté du minion mort
            bool isPlayerSide = _playerSideHistory.Contains(evt.AttackerId);
            var aliveIds = isPlayerSide ? _playerAliveIds : _opponentAliveIds;

            // Créer un token pour le deathrattle
            string tokenId = System.Guid.NewGuid().ToString();
            var snap = new BoardSnapshot
            {
                InstanceId = tokenId,
                Name = evt.TokenName,
                Attack = 1,
                Health = 1,
                Tier = 1
            };

            // Position : en bout de ligne du côté approprié
            aliveIds.Add(tokenId);
            if (isPlayerSide) _playerSideHistory.Add(tokenId);

            var center = isPlayerSide
                ? BoardSurface.Instance.GetPlayerBoardCenter(true)
                : BoardSurface.Instance.GetOpponentBoardCenter();

            var positions = BoardLayout.CalculatePositions(
                aliveIds.Count, center, boardManager.SlotSpacing, boardManager.ArcAmount);

            var token = CreateCombatToken(snap, positions[aliveIds.Count - 1]);
            if (token != null)
            {
                token.AnimateSpawn();
                _combatTokens[tokenId] = token;
                _spawnedTokenIds.Add(tokenId);
            }

            // Repositionner tout le côté
            RepositionSide(isPlayerSide);

            yield return new WaitForSeconds(0.4f);
        }

        // =====================================================
        // REBORN
        // =====================================================

        private IEnumerator PlayReborn(CombatEventData evt)
        {
            bool isPlayerSide = _playerSideHistory.Contains(evt.AttackerId);
            var aliveIds = isPlayerSide ? _playerAliveIds : _opponentAliveIds;

            string rebornId = System.Guid.NewGuid().ToString();
            var snap = new BoardSnapshot
            {
                InstanceId = rebornId,
                Name = evt.AttackerName,
                Attack = evt.AttackerAttack,
                Health = 1,
                Tier = 1
            };

            aliveIds.Add(rebornId);
            if (isPlayerSide) _playerSideHistory.Add(rebornId);

            var center = isPlayerSide
                ? BoardSurface.Instance.GetPlayerBoardCenter(true)
                : BoardSurface.Instance.GetOpponentBoardCenter();

            var positions = BoardLayout.CalculatePositions(
                aliveIds.Count, center, boardManager.SlotSpacing, boardManager.ArcAmount);

            var token = CreateCombatToken(snap, positions[aliveIds.Count - 1]);
            if (token != null)
            {
                token.AnimateSpawn();
                _combatTokens[rebornId] = token;
                _spawnedTokenIds.Add(rebornId);
            }

            RepositionSide(isPlayerSide);

            yield return new WaitForSeconds(0.4f);
        }

        // =====================================================
        // MORT ET REPOSITIONNEMENT
        // =====================================================

        private IEnumerator AnimateDeath(string instanceId)
        {
            if (!_combatTokens.TryGetValue(instanceId, out var token)) yield break;
            if (token == null) yield break;

            token.AnimateDeath();

            // Retirer de la liste alive
            bool wasPlayer = _playerAliveIds.Remove(instanceId);
            bool wasOpponent = _opponentAliveIds.Remove(instanceId);

            yield return new WaitForSeconds(0.35f);

            // Repositionner le côté restant après la mort
            if (wasPlayer)
                RepositionSide(isPlayerSide: true);
            else if (wasOpponent)
                RepositionSide(isPlayerSide: false);
        }

        /// <summary>
        /// Repositionne tous les tokens vivants d'un côté en arc centré.
        /// </summary>
        private void RepositionSide(bool isPlayerSide)
        {
            var aliveIds = isPlayerSide ? _playerAliveIds : _opponentAliveIds;
            var center = isPlayerSide
                ? BoardSurface.Instance.GetPlayerBoardCenter(true)
                : BoardSurface.Instance.GetOpponentBoardCenter();

            var positions = BoardLayout.CalculatePositions(
                aliveIds.Count, center, boardManager.SlotSpacing, boardManager.ArcAmount);

            for (int i = 0; i < aliveIds.Count; i++)
            {
                if (_combatTokens.TryGetValue(aliveIds[i], out var token) && token != null
                    && token.gameObject.activeSelf)
                {
                    token.transform.DOKill();
                    token.transform.DOMove(positions[i], 0.3f).SetEase(Ease.OutQuad);
                    token.SetBasePosition(positions[i]);
                }
            }
        }

        // =====================================================
        // CLEANUP
        // =====================================================

        /// <summary>
        /// Détruit les tokens temporaires (adversaire, deathrattle, reborn)
        /// et restaure TOUS les tokens joueur originaux (même ceux morts au combat).
        /// Le combat ne modifie pas le board réel — les minions reviennent.
        /// </summary>
        private void CleanupCombat()
        {
            // Détruire tous les tokens temporaires (adversaire + spawns combat)
            foreach (var id in _spawnedTokenIds)
            {
                if (_combatTokens.TryGetValue(id, out var token) && token != null)
                {
                    Destroy(token.gameObject);
                }
            }

            // Restaurer TOUS les tokens joueur originaux (de BoardManager)
            // Ils ont pu être désactivés/réduits par AnimateDeath pendant le combat
            // mais le board serveur n'a pas changé — ils doivent revenir
            foreach (var id in _playerSideHistory)
            {
                // Ne pas toucher aux tokens spawned (déjà détruits au-dessus)
                if (_spawnedTokenIds.Contains(id)) continue;

                var token = boardManager.GetPlayerToken(id);
                if (token == null) continue;

                token.transform.DOKill();
                token.gameObject.SetActive(true);
                token.transform.localScale = Vector3.one * CardFactory.Instance.TokenScale;
                token.DragEnabled = true;

                // Restaurer les stats originales (le combat ne modifie pas le vrai board)
                if (_savedPlayerStats.TryGetValue(id, out var saved))
                {
                    token.UpdateStats(saved.attack, saved.health);
                }
            }

            _combatTokens.Clear();
            _playerAliveIds.Clear();
            _opponentAliveIds.Clear();
            _playerSideHistory.Clear();
            _spawnedTokenIds.Clear();
            _savedPlayerStats.Clear();
            _pendingResult = null;

            Debug.Log("[CombatSequencer] Cleanup terminé — tokens joueur restaurés");
        }
    }

    /// <summary>
    /// Données du résultat de combat, reçues du serveur.
    /// </summary>
    public class CombatResultData
    {
        public string OpponentId;
        public bool DidWin;
        public int Damage;
        public string Log;
    }
}
