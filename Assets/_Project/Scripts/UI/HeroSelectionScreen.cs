using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Écran de sélection du héros au début de la partie.
    /// Affiche 2 à 4 cartes de héros avec nom et description du pouvoir.
    /// Le joueur clique sur un héros pour le sélectionner.
    ///
    /// Flux :
    ///   OnHeroOffered → affiche les cartes → clic joueur → SelectHeroAsync
    ///   → animation sélection → OnAllHeroesSelected → masque l'écran
    ///
    /// 📋 DANS UNITY EDITOR :
    /// Créé automatiquement par SetupHeroSelection.cs (execute_script).
    /// Structure :
    ///   HeroSelectionScreen (script)
    ///     └── SelectionCanvas (Screen Space Overlay, Sort Order 95)
    ///           ├── Background (Image, noir semi-transparent)
    ///           ├── Title ("Choisissez votre héros")
    ///           ├── TimerText ("30")
    ///           └── CardContainer (Horizontal Layout Group)
    ///                 ├── HeroCard_0
    ///                 ├── HeroCard_1
    ///                 ├── HeroCard_2
    ///                 └── HeroCard_3
    /// Chaque HeroCard :
    ///   HeroCard_N (Button + Image frame)
    ///     ├── HeroName (TMP)
    ///     ├── HeroDescription (TMP)
    ///     └── TribeLabel (TMP, petit texte tribu/type)
    /// </summary>
    public class HeroSelectionScreen : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas selectionCanvas;

        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Transform cardContainer;

        [Header("Blur")]
        [SerializeField] private Volume blurVolume;

        [Header("Card Template")]
        [Tooltip("Prefab de carte héros (sera instancié pour chaque offre)")]
        [SerializeField] private GameObject heroCardPrefab;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color cardNormalColor = new Color(0.15f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color cardHoverColor = new Color(0.25f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color cardSelectedColor = new Color(0.3f, 0.5f, 0.2f, 1f);

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float cardStagger = 0.1f;
        [SerializeField] private float selectionAnimDuration = 0.4f;
        [SerializeField] private float postSelectionDelay = 1.2f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        // État public
        public static bool IsShowing { get; private set; }

        /// <summary>Le héros choisi par le joueur (null avant sélection).</summary>
        public static HeroOffer SelectedHero { get; private set; }

        // État interne
        private List<HeroOffer> _offers;
        private List<HeroCardUI> _cards = new();
        private bool _hasSelected;
        private float _timerRemaining;
        private bool _timerActive;

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroOffered += HandleHeroOffered;
            server.OnAllHeroesSelected += HandleAllHeroesSelected;

            SetVisible(false);
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnHeroOffered -= HandleHeroOffered;
            server.OnAllHeroesSelected -= HandleAllHeroesSelected;
        }

        private void Update()
        {
            if (!_timerActive) return;

            _timerRemaining -= Time.deltaTime;
            if (_timerRemaining <= 0f)
            {
                _timerRemaining = 0f;
                _timerActive = false;

                // Auto-sélection si le timer expire
                if (!_hasSelected && _offers != null && _offers.Count > 0)
                {
                    SelectHero(0);
                }
            }

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(_timerRemaining).ToString();
        }

        // =====================================================
        // HANDLERS SERVEUR
        // =====================================================

        private void HandleHeroOffered(List<HeroOffer> offers)
        {
            _offers = offers;
            _hasSelected = false;
            _timerRemaining = 30f;
            _timerActive = true;

            Debug.Log($"[HeroSelection] {offers.Count} héros proposés");
            ShowSelection(offers);
        }

        private void HandleAllHeroesSelected()
        {
            Debug.Log("[HeroSelection] Tous les héros sélectionnés — fermeture");
            _timerActive = false;
            HideSelection();
        }

        // =====================================================
        // AFFICHAGE
        // =====================================================

        private void ShowSelection(List<HeroOffer> offers)
        {
            // Nettoyer les anciennes cartes
            ClearCards();

            // Setup UI
            if (titleText != null)
            {
                titleText.text = "Choisissez votre héros";
                titleText.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0f);
            }
            if (timerText != null)
            {
                timerText.text = "30";
                timerText.color = new Color(1f, 1f, 1f, 0f);
            }
            if (backgroundImage != null)
                backgroundImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0f);

            SetVisible(true);

            // Créer les cartes
            for (int i = 0; i < offers.Count; i++)
            {
                var card = CreateHeroCard(offers[i], i);
                _cards.Add(card);
            }

            // Animation d'entrée
            var seq = DOTween.Sequence();

            // Fade in background
            if (backgroundImage != null)
                seq.Append(backgroundImage.DOFade(bgColor.a, fadeInDuration));
            if (blurVolume != null)
                seq.Join(DOTween.To(() => 0f, w => blurVolume.weight = w, 1f, fadeInDuration));

            // Fade in titre et timer
            if (titleText != null)
                seq.Join(titleText.DOColor(titleColor, fadeInDuration));
            if (timerText != null)
                seq.Join(timerText.DOFade(1f, fadeInDuration));

            // Cartes apparaissent une par une
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                float delay = fadeInDuration * 0.5f + i * cardStagger;

                card.Root.transform.localScale = Vector3.one * 0.5f;
                var cg = card.CanvasGroup;
                if (cg != null) cg.alpha = 0f;

                seq.Insert(delay, card.Root.transform
                    .DOScale(1f, 0.4f).SetEase(Ease.OutBack));
                if (cg != null)
                    seq.Insert(delay, DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.3f));
            }
        }

        private void HideSelection()
        {
            var seq = DOTween.Sequence();

            // Fade out tout
            if (titleText != null)
                seq.Append(titleText.DOFade(0f, fadeOutDuration));
            if (timerText != null)
                seq.Join(timerText.DOFade(0f, fadeOutDuration));

            foreach (var card in _cards)
            {
                var cg = card.CanvasGroup;
                if (cg != null)
                    seq.Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, fadeOutDuration));
            }

            if (backgroundImage != null)
                seq.Join(backgroundImage.DOFade(0f, fadeOutDuration));
            if (blurVolume != null)
                seq.Join(DOTween.To(() => blurVolume.weight, w => blurVolume.weight = w, 0f, fadeOutDuration));

            seq.OnComplete(() =>
            {
                SetVisible(false);
                ClearCards();
                if (blurVolume != null) blurVolume.weight = 0f;
            });
        }

        // =====================================================
        // CRÉATION DES CARTES
        // =====================================================

        private HeroCardUI CreateHeroCard(HeroOffer offer, int index)
        {
            if (heroCardPrefab == null || cardContainer == null)
            {
                Debug.LogError("[HeroSelection] heroCardPrefab ou cardContainer non assigné !");
                return new HeroCardUI();
            }

            var go = Instantiate(heroCardPrefab, cardContainer);
            go.name = $"HeroCard_{offer.Id}";

            var cardUI = new HeroCardUI { Root = go };

            // Trouver les composants enfants
            var nameText = go.transform.Find("HeroName")?.GetComponent<TextMeshProUGUI>();
            var descText = go.transform.Find("HeroDescription")?.GetComponent<TextMeshProUGUI>();
            var tribeText = go.transform.Find("TribeLabel")?.GetComponent<TextMeshProUGUI>();
            var button = go.GetComponent<Button>();
            var cardBg = go.GetComponent<Image>();

            cardUI.CanvasGroup = go.GetComponent<CanvasGroup>();
            if (cardUI.CanvasGroup == null)
                cardUI.CanvasGroup = go.AddComponent<CanvasGroup>();

            // Remplir les données
            if (nameText != null) nameText.text = offer.Name;
            if (descText != null) descText.text = offer.Description;
            if (tribeText != null) tribeText.text = ""; // pas de tribe dans HeroOffer pour l'instant
            if (cardBg != null) cardBg.color = cardNormalColor;

            // Hover
            if (button != null && cardBg != null)
            {
                var eventTrigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();

                var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((_) =>
                {
                    if (_hasSelected) return;
                    go.transform.DOKill();
                    go.transform.DOScale(1.08f, 0.15f).SetEase(Ease.OutBack);
                    cardBg.DOColor(cardHoverColor, 0.15f);
                });
                eventTrigger.triggers.Add(enterEntry);

                var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) =>
                {
                    if (_hasSelected) return;
                    go.transform.DOKill();
                    go.transform.DOScale(1f, 0.15f).SetEase(Ease.OutQuad);
                    cardBg.DOColor(cardNormalColor, 0.15f);
                });
                eventTrigger.triggers.Add(exitEntry);

                // Clic = sélection
                int capturedIndex = index;
                button.onClick.AddListener(() => SelectHero(capturedIndex));
            }

            return cardUI;
        }

        // =====================================================
        // SÉLECTION
        // =====================================================

        private void SelectHero(int index)
        {
            if (_hasSelected) return;
            if (_offers == null || index < 0 || index >= _offers.Count) return;

            _hasSelected = true;
            _timerActive = false;

            var selectedOffer = _offers[index];
            SelectedHero = selectedOffer;
            Debug.Log($"[HeroSelection] Héros choisi : {selectedOffer.Name} ({selectedOffer.Id})");

            // Animation de sélection
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.Root == null) continue;

                card.Root.transform.DOKill();

                if (i == index)
                {
                    // Carte sélectionnée : scale up + changement de couleur
                    card.Root.transform.DOScale(1.15f, selectionAnimDuration).SetEase(Ease.OutBack);
                    var bg = card.Root.GetComponent<Image>();
                    if (bg != null) bg.DOColor(cardSelectedColor, selectionAnimDuration);
                }
                else
                {
                    // Autres cartes : fade out + scale down
                    card.Root.transform.DOScale(0.85f, selectionAnimDuration).SetEase(Ease.InQuad);
                    var cg = card.CanvasGroup;
                    if (cg != null)
                        DOTween.To(() => cg.alpha, a => cg.alpha = a, 0.3f, selectionAnimDuration);
                }
            }

            // Envoyer la sélection au serveur
            GameManager.Instance?.Server?.SelectHeroAsync(selectedOffer.Id);
        }

        // =====================================================
        // UTILITAIRES
        // =====================================================

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card.Root != null) Destroy(card.Root);
            }
            _cards.Clear();
        }

        private void SetVisible(bool visible)
        {
            IsShowing = visible;
            if (selectionCanvas != null)
                selectionCanvas.gameObject.SetActive(visible);
        }

        /// <summary>Données internes d'une carte héros à l'écran.</summary>
        private class HeroCardUI
        {
            public GameObject Root;
            public CanvasGroup CanvasGroup;
        }
    }
}
