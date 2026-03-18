using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace AutoBattler.Client.Combat
{
    /// <summary>
    /// Overlay de résultat de combat : affiche "Victoire !", "Défaite !" ou "Égalité !"
    /// avec les dégâts subis et une animation.
    ///
    /// Fonctionne comme PhaseTransitionOverlay : Canvas Screen Space Overlay
    /// avec des éléments assignés via l'Inspector.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "CombatResultOverlay" → Add Component
    /// 2. Enfant "ResultCanvas" → Canvas (Screen Space Overlay, Sort Order 110)
    ///    - Canvas Scaler → Scale With Screen Size, Ref 1920x1080
    ///    - Enfant "Background" → Image
    ///      → Color (0, 0, 0, 0), Anchors stretch-stretch, tous offsets 0
    ///    - Enfant "ResultText" → TextMeshProUGUI
    ///      → Font Size 90, Bold, Alignment Center Middle
    ///      → Anchor center, Pos (0, 40, 0), Width 800, Height 150
    ///    - Enfant "DamageText" → TextMeshProUGUI
    ///      → Font Size 42, Alignment Center Middle
    ///      → Anchor center, Pos (0, -40, 0), Width 600, Height 80
    /// 3. Câbler les références dans CombatResultOverlay Inspector
    /// 4. Le Canvas doit être DÉSACTIVÉ par défaut (il sera activé par le script)
    /// </summary>
    public class CombatResultOverlay : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas resultCanvas;

        [Header("UI Elements")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI damageText;

        [Header("Colors")]
        [SerializeField] private Color victoryColor = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField] private Color defeatColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color tieColor = new Color(0.9f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.4f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        private Sequence _currentSequence;

        private void Awake()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Affiche le résultat du combat avec animation.
        /// </summary>
        public void Show(CombatResultData result)
        {
            if (result == null) return;

            _currentSequence?.Kill();

            // Déterminer le texte et la couleur
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

            // Configurer les éléments
            if (resultText != null)
            {
                resultText.text = title;
                resultText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                resultText.transform.localScale = Vector3.one * 0.5f;
            }

            if (damageText != null)
            {
                damageText.text = subtitle;
                damageText.color = new Color(1f, 1f, 1f, 0f);
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0f);
            }

            SetVisible(true);

            // Animation d'entrée
            _currentSequence = DOTween.Sequence();

            if (backgroundImage != null)
                _currentSequence.Append(backgroundImage.DOFade(bgColor.a, fadeInDuration));

            if (resultText != null)
            {
                _currentSequence.Join(resultText.DOColor(textColor, fadeInDuration));
                _currentSequence.Join(resultText.transform
                    .DOScale(1f, fadeInDuration)
                    .SetEase(Ease.OutBack));
            }

            if (damageText != null && !string.IsNullOrEmpty(subtitle))
            {
                _currentSequence.Join(damageText.DOFade(1f, fadeInDuration).SetDelay(0.1f));
            }

            Debug.Log($"[CombatResultOverlay] {title} {subtitle}");
        }

        /// <summary>
        /// Masque l'overlay avec une animation de fade out.
        /// </summary>
        public void Hide()
        {
            _currentSequence?.Kill();

            _currentSequence = DOTween.Sequence();

            if (backgroundImage != null)
                _currentSequence.Append(backgroundImage.DOFade(0f, fadeOutDuration));

            if (resultText != null)
                _currentSequence.Join(resultText.DOFade(0f, fadeOutDuration));

            if (damageText != null)
                _currentSequence.Join(damageText.DOFade(0f, fadeOutDuration));

            _currentSequence.OnComplete(() => SetVisible(false));
        }

        private void SetVisible(bool visible)
        {
            if (resultCanvas != null)
                resultCanvas.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            _currentSequence?.Kill();
        }
    }
}
