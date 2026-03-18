using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Gère la transition visuelle entre les phases Recruit et Combat.
    ///
    /// En Recruit : le shop est visible, le board adverse est masqué,
    ///              les boutons et l'or sont affichés.
    /// En Combat  : le shop disparaît, le board adverse apparaît,
    ///              les boutons et l'or sont masqués, le board joueur
    ///              se repositionne.
    ///
    /// Les éléments à masquer/afficher sont assignés via l'Inspector.
    /// Ce script ne crée rien, il active/désactive et anime.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "PhaseLayoutManager" dans la Hierarchy
    /// 2. Add Component → PhaseLayoutManager
    /// 3. Glisser les GameObjects dans les champs :
    ///    - "Recruit Only Objects" : ShopManager GO, boutons, gold counter
    ///    - "Combat Only Objects" : le conteneur du board adverse
    /// </summary>
    public class PhaseLayoutManager : MonoBehaviour
    {
        [Header("Éléments visibles uniquement en Recruit")]
        [Tooltip("GameObjects à activer en Recruit, désactiver en Combat")]
        [SerializeField] private GameObject[] recruitOnlyObjects;

        [Header("Éléments visibles uniquement en Combat")]
        [Tooltip("GameObjects à activer en Combat, désactiver en Recruit")]
        [SerializeField] private GameObject[] combatOnlyObjects;

        [Header("Animation de transition")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private Ease transitionEase = Ease.InOutQuad;

        private bool _isCombat;

        /// <summary>True si on est en phase combat.</summary>
        public bool IsCombat => _isCombat;

        /// <summary>Event déclenché quand la phase change.</summary>
        public event System.Action<bool> OnPhaseChanged;

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPhaseStarted += HandlePhaseStarted;

            // État initial : recruit
            SetPhase(false, animate: false);
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPhaseStarted -= HandlePhaseStarted;
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            // En combat ET en résultats → garder le layout combat
            // Revenir en recruit uniquement quand "Recruiting" commence
            if (phase == "Combat")
                SetPhase(true, animate: true);
            else if (phase == "Recruiting")
                SetPhase(false, animate: true);
            // "Results" → on reste en mode combat, pas de changement
        }

        /// <summary>
        /// Bascule entre le layout Recruit et le layout Combat.
        /// </summary>
        public void SetPhase(bool isCombat, bool animate = true)
        {
            _isCombat = isCombat;

            // Activer/désactiver les éléments
            if (recruitOnlyObjects != null)
            {
                foreach (var go in recruitOnlyObjects)
                {
                    if (go != null) go.SetActive(!isCombat);
                }
            }

            if (combatOnlyObjects != null)
            {
                foreach (var go in combatOnlyObjects)
                {
                    if (go != null) go.SetActive(isCombat);
                }
            }

            OnPhaseChanged?.Invoke(isCombat);
        }
    }
}
