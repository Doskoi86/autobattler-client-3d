using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Network;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Overlay plein écran pour les transitions entre phases.
    /// Utilise un Screen Space Canvas (non affecté par le post-processing)
    /// + un Volume URP pour le blur du plateau en dessous.
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

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float holdDuration = 1.2f;
        [SerializeField] private float fadeOutDuration = 0.4f;

        private void Awake()
        {
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
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnPhaseStarted -= HandlePhaseStarted;
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
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

            ShowTransition(title, subtitle, textColor);
        }

        private void ShowTransition(string title, string subtitle, Color textColor)
        {
            // Configurer les textes
            if (phaseText != null)
            {
                phaseText.text = title;
                phaseText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
            }
            if (turnText != null)
            {
                turnText.text = subtitle;
                turnText.color = new Color(1f, 1f, 1f, 0f);
            }

            // Background transparent au début
            if (backgroundImage != null)
                backgroundImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0f);

            SetVisible(true);

            // Animation : fade in → hold → fade out
            var seq = DOTween.Sequence();

            // Fade in
            if (backgroundImage != null)
                seq.Append(backgroundImage.DOFade(bgColor.a, fadeInDuration));
            if (blurVolume != null)
                seq.Join(DOTween.To(() => 0f, w => blurVolume.weight = w, 1f, fadeInDuration));
            if (phaseText != null)
            {
                seq.Join(phaseText.DOColor(textColor, fadeInDuration));
                seq.Join(phaseText.transform.DOScale(1.2f, fadeInDuration)
                    .From(0.5f).SetEase(Ease.OutBack));
            }
            if (turnText != null)
                seq.Join(turnText.DOFade(1f, fadeInDuration));

            // Hold
            seq.AppendInterval(holdDuration);

            // Fade out — le blur commence à partir AVANT le fond
            if (blurVolume != null)
                seq.Append(DOTween.To(() => 1f, w => blurVolume.weight = w, 0f, fadeOutDuration));
            if (backgroundImage != null)
                seq.Insert(seq.Duration() - fadeOutDuration * 0.3f, backgroundImage.DOFade(0f, fadeOutDuration));
            if (phaseText != null)
                seq.Join(phaseText.DOFade(0f, fadeOutDuration));
            if (turnText != null)
                seq.Join(turnText.DOFade(0f, fadeOutDuration));

            // Cacher — blur coupé net
            seq.OnComplete(() =>
            {
                SetVisible(false);
                if (blurVolume != null) blurVolume.weight = 0f;
            });
        }

        private void SetVisible(bool visible)
        {
            if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(visible);
        }
    }
}
