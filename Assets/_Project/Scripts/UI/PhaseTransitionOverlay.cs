using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Combat;
using AutoBattler.Client.Network;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Overlay plein écran pour les transitions entre phases ET les résultats de combat.
    /// Utilise un Screen Space Canvas (non affecté par le post-processing)
    /// + un Volume URP pour le blur du plateau en dessous.
    ///
    /// Flux combat → recrutement sans coupure :
    ///   1. ShowResult()  → fade in bg+blur+texte résultat
    ///   2. HideResult()  → fade out TEXTE seulement (bg+blur restent)
    ///   3. "Recruiting"  → détecte overlay déjà visible → fade in texte recrutement
    ///   4.               → hold → fade out complet (bg+blur+texte)
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "PhaseTransitionOverlay" → Add Component
    /// 2. Enfant "OverlayCanvas" → Canvas (Screen Space Overlay, Sort Order 100)
    ///    - "Background" → Image (couleur noire, alpha 0)
    ///    - "PhaseText" → TextMeshProUGUI (centré, fontSize 72)
    ///    - "TurnText" → TextMeshProUGUI (centré, fontSize 36, sous PhaseText)
    /// 3. Enfant "BlurVolume" → Volume (URP, Depth of Field Gaussian)
    /// 4. Câbler les références
    /// </summary>
    public class PhaseTransitionOverlay : MonoBehaviour
    {
        public static PhaseTransitionOverlay Instance { get; private set; }

        [Header("Canvas References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private Canvas overlayCanvas;

        [Header("Blur")]
        [SerializeField] private Volume blurVolume;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color combatTextColor = new Color(1f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color recruitTextColor = new Color(0.3f, 1f, 0.5f, 1f);
        [SerializeField] private Color victoryColor = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField] private Color defeatColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color tieColor = new Color(0.9f, 0.85f, 0.3f, 1f);

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float holdDuration = 1.2f;
        [SerializeField] private float fadeOutDuration = 0.4f;

        private Sequence _currentSequence;

        /// <summary>
        /// True quand le fond+blur sont maintenus entre le résultat et la prochaine phase.
        /// Le texte a été fade out mais l'overlay reste visible.
        /// </summary>
        private bool _holdingOverlay;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetVisible(false);
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPhaseStarted += HandlePhaseStarted;
        }

        private void OnDestroy()
        {
            _currentSequence?.Kill();

            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPhaseStarted -= HandlePhaseStarted;
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            // "Results" n'affiche pas de transition — le résultat est géré par ShowResult()
            if (phase == "Results") return;

            string title;
            string subtitle;
            Color textColor;

            switch (phase)
            {
                case "Combat":
                    title = "Combat !";
                    subtitle = $"Tour {turn}";
                    textColor = combatTextColor;
                    break;
                case "Recruiting":
                    title = "Recrutement";
                    subtitle = $"Tour {turn} — {duration}s";
                    textColor = recruitTextColor;
                    break;
                default:
                    title = phase;
                    subtitle = $"Tour {turn}";
                    textColor = Color.white;
                    break;
            }

            // Si l'overlay est maintenu (après HideResult), enchaîner sans refaire le fond
            if (_holdingOverlay)
                ShowTransitionContinued(title, subtitle, textColor);
            else
                ShowTransition(title, subtitle, textColor);
        }

        // =====================================================
        // TRANSITION STANDARD (fond + blur + texte complet)
        // =====================================================

        private void ShowTransition(string title, string subtitle, Color textColor)
        {
            _currentSequence?.Kill();
            _holdingOverlay = false;

            SetupTexts(title, subtitle, textColor);
            SetVisible(true);

            _currentSequence = DOTween.Sequence();

            // Fade in complet (fond + blur + texte)
            AppendFadeInFull(_currentSequence, textColor);

            // Hold
            _currentSequence.AppendInterval(holdDuration);

            // Fade out complet
            AppendFadeOutFull(_currentSequence);

            _currentSequence.OnComplete(() =>
            {
                SetVisible(false);
                if (blurVolume != null) blurVolume.weight = 0f;
            });
        }

        // =====================================================
        // TRANSITION ENCHAÎNÉE (fond+blur déjà visibles)
        // =====================================================

        /// <summary>
        /// Enchaîne une transition SANS refaire le fade in du fond/blur.
        /// Le fond et le blur sont déjà affichés — on anime seulement le texte.
        /// </summary>
        private void ShowTransitionContinued(string title, string subtitle, Color textColor)
        {
            _currentSequence?.Kill();
            _holdingOverlay = false;

            // Préparer les textes (transparents, prêts à fade in)
            if (phaseText != null)
            {
                phaseText.text = title;
                phaseText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                phaseText.transform.localScale = Vector3.one * 0.5f;
            }
            if (turnText != null)
            {
                turnText.text = subtitle;
                turnText.color = new Color(1f, 1f, 1f, 0f);
            }

            _currentSequence = DOTween.Sequence();

            // Fade in TEXTE seulement (fond+blur sont déjà à leur valeur)
            AppendFadeInTextOnly(_currentSequence, textColor);

            // Hold
            _currentSequence.AppendInterval(holdDuration);

            // Fade out COMPLET (fond + blur + texte)
            AppendFadeOutFull(_currentSequence);

            _currentSequence.OnComplete(() =>
            {
                SetVisible(false);
                if (blurVolume != null) blurVolume.weight = 0f;
            });
        }

        // =====================================================
        // RÉSULTAT DE COMBAT (contrôlé par CombatSequencer)
        // =====================================================

        /// <summary>
        /// Affiche le résultat de combat. Reste visible jusqu'à HideResult().
        /// </summary>
        public void ShowResult(CombatResultData result)
        {
            if (result == null) return;
            _currentSequence?.Kill();
            _holdingOverlay = false;

            string title;
            string subtitle;
            Color textColor;

            if (result.DidWin)
            {
                title = "Victoire !";
                subtitle = "";
                textColor = victoryColor;
            }
            else if (result.Damage == 0)
            {
                title = "Égalité !";
                subtitle = "";
                textColor = tieColor;
            }
            else
            {
                title = "Défaite !";
                subtitle = $"-{result.Damage} PV";
                textColor = defeatColor;
            }

            SetupTexts(title, subtitle, textColor);
            SetVisible(true);

            _currentSequence = DOTween.Sequence();
            AppendFadeInFull(_currentSequence, textColor);
        }

        /// <summary>
        /// Fade out du TEXTE seulement. Le fond+blur restent visibles
        /// pour enchaîner avec la prochaine phase sans coupure.
        /// </summary>
        public void HideResult()
        {
            _currentSequence?.Kill();
            _holdingOverlay = true;

            _currentSequence = DOTween.Sequence();

            // Fade out TEXTE seulement — fond et blur restent
            if (phaseText != null)
                _currentSequence.Append(phaseText.DOFade(0f, fadeOutDuration));
            if (turnText != null)
                _currentSequence.Join(turnText.DOFade(0f, fadeOutDuration));
        }

        // =====================================================
        // BUILDING BLOCKS D'ANIMATION
        // =====================================================

        private void SetupTexts(string title, string subtitle, Color textColor)
        {
            if (phaseText != null)
            {
                phaseText.text = title;
                phaseText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                phaseText.transform.localScale = Vector3.one * 0.5f;
            }
            if (turnText != null)
            {
                turnText.text = subtitle;
                turnText.color = new Color(1f, 1f, 1f, 0f);
            }
            if (backgroundImage != null)
                backgroundImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0f);
        }

        /// <summary>Fade in fond + blur + texte (depuis zéro)</summary>
        private void AppendFadeInFull(Sequence seq, Color textColor)
        {
            if (backgroundImage != null)
                seq.Append(backgroundImage.DOFade(bgColor.a, fadeInDuration));
            if (blurVolume != null)
                seq.Join(DOTween.To(() => 0f, w => blurVolume.weight = w, 1f, fadeInDuration));
            if (phaseText != null)
            {
                seq.Join(phaseText.DOColor(textColor, fadeInDuration));
                seq.Join(phaseText.transform.DOScale(1.2f, fadeInDuration).SetEase(Ease.OutBack));
            }
            if (turnText != null)
                seq.Join(turnText.DOFade(1f, fadeInDuration));
        }

        /// <summary>Fade in texte seulement (fond+blur déjà visibles)</summary>
        private void AppendFadeInTextOnly(Sequence seq, Color textColor)
        {
            if (phaseText != null)
            {
                seq.Append(phaseText.DOColor(textColor, fadeInDuration));
                seq.Join(phaseText.transform.DOScale(1.2f, fadeInDuration).SetEase(Ease.OutBack));
            }
            if (turnText != null)
                seq.Join(turnText.DOFade(1f, fadeInDuration));
        }

        /// <summary>Fade out complet (fond + blur + texte)</summary>
        private void AppendFadeOutFull(Sequence seq)
        {
            if (blurVolume != null)
                seq.Append(DOTween.To(() => blurVolume.weight, w => blurVolume.weight = w, 0f, fadeOutDuration));
            if (backgroundImage != null)
                seq.Join(backgroundImage.DOFade(0f, fadeOutDuration));
            if (phaseText != null)
                seq.Join(phaseText.DOFade(0f, fadeOutDuration));
            if (turnText != null)
                seq.Join(turnText.DOFade(0f, fadeOutDuration));
        }

        private void SetVisible(bool visible)
        {
            if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(visible);
        }
    }
}
