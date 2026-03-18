using UnityEngine;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Portrait du héros du joueur affiché sur le plateau.
    /// Montre le portrait, les PV, l'armure, et le nom du héros.
    /// Utilise SpriteRenderers (pas de Canvas) — même approche que les tokens.
    ///
    /// Écoute OnHeroHealthUpdated pour mettre à jour les PV/armure
    /// et OnPhaseStarted("Recruiting") pour s'afficher après la sélection.
    ///
    /// 📋 Structure du prefab :
    ///   HeroPortrait (script, rotation 90,0,0)
    ///     ├── PortraitFrame (SpriteRenderer — cadre orné, Sorting Order 0)
    ///     ├── PortraitArt (SpriteRenderer — art du héros, Sorting Order 1)
    ///     ├── HealthBadge (SpriteRenderer — badge PV, Sorting Order 3)
    ///     │     └── HealthText (TextMeshPro 3D)
    ///     ├── ArmorBadge (SpriteRenderer — bouclier armure, Sorting Order 3, désactivé)
    ///     │     └── ArmorText (TextMeshPro 3D)
    ///     └── NameText (TextMeshPro 3D — nom du héros, Sorting Order 2)
    /// </summary>
    public class HeroPortraitDisplay : MonoBehaviour
    {
        public static HeroPortraitDisplay Instance { get; private set; }
        [Header("Sprite References")]
        [SerializeField] private SpriteRenderer portraitFrame;
        [SerializeField] private SpriteRenderer portraitArt;

        [Header("Health")]
        [SerializeField] private SpriteRenderer healthBadge;
        [SerializeField] private TextMeshPro healthText;

        [Header("Armor")]
        [SerializeField] private SpriteRenderer armorBadge;
        [SerializeField] private TextMeshPro armorText;

        [Header("Name")]
        [SerializeField] private TextMeshPro nameText;

        [Header("Animation")]
        [SerializeField] private float damagePunchScale = 0.15f;
        [SerializeField] private float damagePunchDuration = 0.3f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float spawnDuration = 0.4f;

        // État
        private int _currentHealth;
        private int _currentArmor;
        private bool _initialized;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Buffer pendant le combat — ne pas spoiler le résultat
        private bool _combatBuffering;
        private int _bufferedHealth = -1;
        private int _bufferedArmor = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mpb = new MaterialPropertyBlock();
            SetVisualsVisible(false);
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroHealthUpdated += HandleHeroHealthUpdated;
            server.OnPhaseStarted += HandlePhaseStarted;
        }

        private void OnDestroy()
        {
            transform.DOKill();
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroHealthUpdated -= HandleHeroHealthUpdated;
            server.OnPhaseStarted -= HandlePhaseStarted;
        }

        // =====================================================
        // HANDLERS SERVEUR
        // =====================================================

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            if (phase != "Recruiting") return;

            if (!_initialized)
            {
                var hero = HeroSelectionScreen.SelectedHero;
                if (hero == null) return;

                InitializePortrait(hero);
            }
        }

        private void HandleHeroHealthUpdated(int health, int armor)
        {
            // Pendant le combat, on bufferise pour ne pas spoiler le résultat
            if (_combatBuffering)
            {
                _bufferedHealth = health;
                _bufferedArmor = armor;
                return;
            }

            ApplyHealthUpdate(health, armor);
        }

        private void ApplyHealthUpdate(int health, int armor)
        {
            int previousHealth = _currentHealth;
            _currentHealth = health;
            _currentArmor = armor;

            UpdateHealthDisplay();
            UpdateArmorDisplay();

            if (health < previousHealth && _initialized)
            {
                AnimateDamage();
            }
        }

        /// <summary>
        /// Active le buffering : les updates de PV sont stockées sans être affichées.
        /// Appelé par CombatSequencer au début du combat.
        /// </summary>
        public void StartCombatBuffering()
        {
            _combatBuffering = true;
            _bufferedHealth = -1;
            _bufferedArmor = -1;
        }

        /// <summary>
        /// Applique les PV bufferisés avec animation.
        /// Appelé par CombatSequencer juste avant d'afficher le résultat.
        /// </summary>
        public void FlushCombatBuffer()
        {
            _combatBuffering = false;

            if (_bufferedHealth >= 0)
            {
                ApplyHealthUpdate(_bufferedHealth, _bufferedArmor >= 0 ? _bufferedArmor : _currentArmor);
                _bufferedHealth = -1;
                _bufferedArmor = -1;
            }
        }

        // =====================================================
        // INITIALISATION
        // =====================================================

        private void InitializePortrait(HeroOffer hero)
        {
            _initialized = true;
            _currentHealth = 30;
            _currentArmor = 5;

            // Nom du héros
            if (nameText != null)
                nameText.text = hero.Name;

            // PV et armure initiaux
            UpdateHealthDisplay();
            UpdateArmorDisplay();

            // Afficher avec animation
            SetVisualsVisible(true);
            AnimateSpawn();

            Debug.Log($"[HeroPortrait] Initialisé : {hero.Name} — {_currentHealth} PV, {_currentArmor} armure");
        }

        // =====================================================
        // MISE À JOUR AFFICHAGE
        // =====================================================

        private void UpdateHealthDisplay()
        {
            if (healthText != null)
                healthText.text = _currentHealth.ToString();

            // Couleur rouge si PV bas
            if (healthText != null)
            {
                if (_currentHealth <= 10)
                    healthText.color = new Color(1f, 0.3f, 0.3f, 1f);
                else
                    healthText.color = Color.white;
            }
        }

        private void UpdateArmorDisplay()
        {
            bool hasArmor = _currentArmor > 0;
            if (armorBadge != null)
                armorBadge.gameObject.SetActive(hasArmor);
            if (armorText != null)
                armorText.text = _currentArmor.ToString();
        }

        // =====================================================
        // ANIMATIONS
        // =====================================================

        private void AnimateSpawn()
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, spawnDuration).SetEase(Ease.OutBack);
        }

        private void AnimateDamage()
        {
            // Shake + punch scale sur le portrait
            transform.DOKill();
            transform.DOShakePosition(damagePunchDuration, 0.05f, 15);

            // Flash rouge sur le health badge
            if (healthText != null)
            {
                healthText.transform.DOKill();
                healthText.transform.DOPunchScale(Vector3.one * damagePunchScale, damagePunchDuration)
                    .SetEase(Ease.OutElastic);
            }
        }

        // =====================================================
        // VISIBILITÉ
        // =====================================================

        private void SetVisualsVisible(bool visible)
        {
            if (portraitFrame != null) portraitFrame.enabled = visible;
            if (portraitArt != null) portraitArt.enabled = visible;
            if (healthBadge != null) healthBadge.enabled = visible;
            if (healthText != null) healthText.enabled = visible;
            if (armorBadge != null) armorBadge.gameObject.SetActive(visible && _currentArmor > 0);
            if (armorText != null) armorText.enabled = visible;
            if (nameText != null) nameText.enabled = visible;
        }
    }
}
