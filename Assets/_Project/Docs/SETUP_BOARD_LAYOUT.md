# Instructions — Reconstruction du plateau de jeu

Ces instructions remplacent l'ancien système de zones (4 Quads colorés) par le nouveau
système d'ancres avec support des phases Recruit/Combat.

## Pré-requis

- Ouvrir la scène `Assets/Scenes/MainGame.unity`
- Tous les scripts doivent compiler sans erreur

---

## Étape 1 — Nettoyer BoardSurface

1. Sélectionner **BoardSurface** dans la Hierarchy
2. Remettre le **Transform** à :
   - Position : `(0, 0, 0)`
   - Rotation : `(0, 0, 0)`
   - Scale : `(1, 1, 1)`
3. **Supprimer les 4 enfants Quad existants** :
   - Clic droit sur chaque enfant (ShopZone, PlayerBoardZone, OpponentBoardZone, HandZone) → Delete
   - Ce sont les anciens rectangles colorés, on n'en a plus besoin

---

## Étape 2 — Créer le fond du plateau

1. Clic droit sur **BoardSurface** → 3D Object → Quad → renommer **"BoardBackground"**
2. Transform du BoardBackground :
   - Position : `(0.90, -0.01, 1.00)`
   - Rotation : `(90, 0, 0)`
   - Scale : `(21.30, 11.98, 1)`
3. Material :
   - Sélectionner BoardBackground dans l'Inspector
   - Dans le MeshRenderer → Materials → Element 0
   - Cliquer sur le petit cercle → choisir un material existant (par exemple `Mat_BoardZone`)
   - Ensuite dans le material, changer la couleur en brun chaud : `(0.35, 0.22, 0.12, 1)` (Albedo/Base Color)
   - Plus tard on remplacera par une vraie texture bois
4. **Sorting** : ce Quad doit être derrière tout. S'assurer qu'il est à Y = -0.01 (sous les autres éléments).

---

## Étape 3 — Créer les ancres permanentes

Pour chaque ancre, faire : clic droit sur **BoardSurface** → Create Empty → renommer.

| Nom | Position | Usage |
|-----|----------|-------|
| **PlayerBoardAnchor** | `(0.90, 0.01, 2.26)` | Centre du board joueur en recruit |
| **HandAnchor** | `(0.90, 0.01, -2.78)` | Centre de la main en recruit |
| **HeroPortraitAnchor** | `(0.90, 0.01, -0.58)` | Position du portrait du héros |
| **HeroPanelAnchor** | `(-10.31, 0.01, 1.00)` | Position du panel héros (gauche) |

---

## Étape 4 — Créer les ancres Recruit

| Nom | Position | Usage |
|-----|----------|-------|
| **ShopAnchor** | `(0.90, 0.01, 5.41)` | Centre de la zone shop |
| **GoldAnchor** | `(-4.00, 0.01, -4.50)` | Position du compteur d'or |

---

## Étape 5 — Créer les ancres Combat

| Nom | Position | Usage |
|-----|----------|-------|
| **OpponentBoardAnchor** | `(0.90, 0.01, 4.91)` | Centre du board adverse en combat |
| **CombatPlayerBoardAnchor** | `(0.90, 0.01, 1.13)` | Board joueur repositionné en combat |
| **CombatHandAnchor** | `(0.90, 0.01, -3.04)` | Main repositionnée en combat |

---

## Étape 6 — Câbler BoardSurface dans l'Inspector

1. Sélectionner **BoardSurface** dans la Hierarchy
2. Dans le composant **BoardSurface (Script)** de l'Inspector :

| Champ Inspector | Glisser depuis la Hierarchy |
|----------------|---------------------------|
| Board Background | BoardBackground |
| Player Board Anchor | PlayerBoardAnchor |
| Hand Anchor | HandAnchor |
| Hero Portrait Anchor | HeroPortraitAnchor |
| Hero Panel Anchor | HeroPanelAnchor |
| Shop Anchor | ShopAnchor |
| Gold Anchor | GoldAnchor |
| Opponent Board Anchor | OpponentBoardAnchor |
| Combat Player Board Anchor | CombatPlayerBoardAnchor |
| Combat Hand Anchor | CombatHandAnchor |

---

## Étape 7 — Créer le PhaseLayoutManager

1. Hierarchy → clic droit → Create Empty → renommer **"PhaseLayoutManager"**
2. Add Component → **PhaseLayoutManager**
3. Dans l'Inspector :
   - **Recruit Only Objects** : pour l'instant vide (on ajoutera les boutons et le gold counter quand ils existeront)
   - **Combat Only Objects** : pour l'instant vide (on ajoutera le conteneur du board adverse plus tard)
   - **Transition Duration** : `0.5`
   - **Transition Ease** : `InOutQuad`

---

## Étape 8 — Vérifier les managers existants

### ShopManager
- Le spacing est passé à `2.0` (au lieu de 1.8)
- Le script lit maintenant `BoardSurface.GetShopCenter()` au lieu de `ShopZone.position`
- Rien à changer dans l'Inspector

### BoardManager
- Le spacing est passé à `1.6` (au lieu de 1.8)
- Le script lit maintenant `BoardSurface.GetPlayerBoardCenter(isCombat)` au lieu de `PlayerBoardZone.position`
- Rien à changer dans l'Inspector (sauf le slotPrefab qui était déjà non assigné)

### HandManager
- Le spacing est passé à `1.1` (au lieu de 1.6)
- Le script lit maintenant `BoardSurface.GetHandCenter(isCombat)` au lieu de `HandZone.position`
- Rien à changer dans l'Inspector

---

## Étape 9 — Tester

1. **Ctrl+P** (Play)
2. Vérifier dans la Console : pas d'erreur rouge liée à BoardSurface
3. Les tokens du shop doivent apparaître centrés autour de Z ≈ 5.41
4. Le board joueur est centré autour de Z ≈ 2.26
5. La main est en bas autour de Z ≈ -2.78
6. Le fond brun doit couvrir quasiment tout l'écran

### Si les cartes sont hors écran :
- Vérifier que la caméra est bien à Position `(0, 20, -0.75)`, Rotation `(85, 0, 0)`, FOV `35`
- Vérifier que BoardSurface est à scale `(1, 1, 1)` et position `(0, 0, 0)`

---

## Résultat attendu

Visuellement, c'est encore brut (un rectangle brun avec des cartes dessus), mais la **fondation est posée** :
- Les zones sont correctement positionnées et dimensionnées
- Le système d'ancres supporte les deux phases (Recruit/Combat)
- Les spacings sont adaptés aux futurs tokens compacts
- Le code est prêt pour les prochaines étapes (prefabs MinionToken, HandCard, boutons)
