using UnityEngine;

namespace AutoBattler.Client.UI.HUD
{
    /// <summary>
    /// OBSOLÈTE — Remplacé par un Canvas prefab.
    ///
    /// Le HUD est désormais un prefab Canvas dans Assets/_Project/Prefabs/UI/HUDCanvas.prefab.
    /// Les références (textes, boutons) sont câblées dans l'Inspector du prefab.
    /// GameHUD lit ces références via [SerializeField] — plus besoin de builder par code.
    ///
    /// 📋 CRÉER LE PREFAB HUDCanvas :
    /// 1. Hierarchy → clic droit → UI → Canvas → renommer "HUDCanvas"
    ///    - Canvas : Render Mode = Screen Space - Overlay, Sort Order = 10
    ///    - Canvas Scaler : Scale With Screen Size, Reference = 1920×1080, Match = 0.5
    /// 2. Enfant de HUDCanvas : clic droit → Create Empty → renommer "TopBar"
    ///    - Ajouter Image (couleur noire alpha 0.6)
    ///    - Anchor haut-centre, Size 600×60, Pos Y = -10
    /// 3. Enfant de TopBar : UI → Text - TextMeshPro → "PhaseText" (24pt, gauche)
    /// 4. Enfant de TopBar : UI → Text - TextMeshPro → "TimerText" (32pt, droite, blanc)
    /// 5. Enfant de HUDCanvas : "TierPanel" (Image, anchor haut-gauche, 220×80)
    ///    - Enfant : "TierText" (28pt, bleu clair)
    ///    - Enfant : "UpgradeCostText" (18pt, jaune)
    /// 6. Enfant de HUDCanvas : "GoldPanel" (Image, anchor haut-droite, 160×60)
    ///    - Enfant : "GoldText" (36pt, jaune)
    /// 7. Enfant de HUDCanvas : "ButtonBar" (Image, anchor bas-centre, 800×60)
    ///    - 4 boutons enfants : RerollButton, FreezeButton, UpgradeButton, ReadyButton
    ///    - Chaque bouton : Image + Button + enfant Text (TextMeshProUGUI 18pt)
    /// 8. Ajouter le composant GameHUD au GameObject HUDCanvas
    /// 9. Assigner tous les textes et boutons dans l'Inspector de GameHUD
    /// 10. Glisser HUDCanvas → Assets/_Project/Prefabs/UI/ pour créer le prefab
    ///
    /// Ce script peut être supprimé une fois le prefab créé.
    /// </summary>
    [System.Obsolete("Utiliser le prefab HUDCanvas à la place. Voir les instructions ci-dessus.")]
    public class HUDBuilder : MonoBehaviour
    {
    }
}
