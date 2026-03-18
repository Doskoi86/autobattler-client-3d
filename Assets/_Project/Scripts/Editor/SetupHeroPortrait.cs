using UnityEngine;
using UnityEditor;
using TMPro;

public class SetupHeroPortrait
{
    public static void Execute()
    {
        // Supprimer l'ancien si existe
        var existing = GameObject.Find("HeroPortrait");
        if (existing != null) Object.DestroyImmediate(existing);

        // Récupérer les sprites des badges existants (health_badge du token)
        Sprite healthSprite = null;
        Sprite armorSprite = null;
        Sprite frameSprite = null;

        // Chercher les sprites sur un token existant ou dans les assets
        var tokenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/MinionToken.prefab");
        if (tokenPrefab != null)
        {
            var hb = FindChildRecursive(tokenPrefab.transform, "HealthBadge");
            if (hb != null)
            {
                var sr = hb.GetComponent<SpriteRenderer>();
                if (sr != null) healthSprite = sr.sprite;
            }
            var ab = FindChildRecursive(tokenPrefab.transform, "AttackBadge");
            if (ab != null)
            {
                var sr = ab.GetComponent<SpriteRenderer>();
                if (sr != null) armorSprite = sr.sprite; // Réutiliser le badge attack comme placeholder armure
            }
            var frame = FindChildRecursive(tokenPrefab.transform, "Frame");
            if (frame != null)
            {
                var sr = frame.GetComponent<SpriteRenderer>();
                if (sr != null) frameSprite = sr.sprite;
            }
        }

        // Trouver l'ancre
        var anchor = FindByPath("--- Surface ---/Layout/HeroPortraitAnchor");
        Vector3 pos = anchor != null ? anchor.transform.position : new Vector3(0f, 0.1f, -0.58f);

        // =====================================================
        // ROOT
        // =====================================================
        var root = new GameObject("HeroPortrait");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        root.transform.localScale = Vector3.one * 1.4f; // Plus grand que les tokens

        // =====================================================
        // PORTRAIT FRAME (cadre)
        // =====================================================
        var frameGO = new GameObject("PortraitFrame");
        frameGO.transform.SetParent(root.transform, false);
        var frameSR = frameGO.AddComponent<SpriteRenderer>();
        frameSR.sprite = frameSprite;
        frameSR.sortingLayerName = "Board";
        frameSR.sortingOrder = 0;
        frameSR.color = new Color(0.85f, 0.7f, 0.4f, 1f); // Doré

        // =====================================================
        // PORTRAIT ART (placeholder coloré)
        // =====================================================
        var artGO = new GameObject("PortraitArt");
        artGO.transform.SetParent(root.transform, false);
        artGO.transform.localScale = Vector3.one * 0.65f;
        var artSR = artGO.AddComponent<SpriteRenderer>();
        artSR.sortingLayerName = "Board";
        artSR.sortingOrder = 1;
        artSR.color = new Color(0.3f, 0.2f, 0.4f, 1f); // Violet placeholder

        // Chercher le sprite carteFondBlanc pour le placeholder
        var placeholderSprites = AssetDatabase.FindAssets("carteFondBlanc t:Sprite");
        if (placeholderSprites.Length > 0)
        {
            var spritePath = AssetDatabase.GUIDToAssetPath(placeholderSprites[0]);
            artSR.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        // =====================================================
        // HEALTH BADGE
        // =====================================================
        var healthBadgeGO = new GameObject("HealthBadge");
        healthBadgeGO.transform.SetParent(root.transform, false);
        healthBadgeGO.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        healthBadgeGO.transform.localScale = Vector3.one * 0.7f;
        var healthBadgeSR = healthBadgeGO.AddComponent<SpriteRenderer>();
        healthBadgeSR.sprite = healthSprite;
        healthBadgeSR.sortingLayerName = "Board";
        healthBadgeSR.sortingOrder = 3;

        // Health Text
        var healthTextGO = new GameObject("HealthText");
        healthTextGO.transform.SetParent(healthBadgeGO.transform, false);
        healthTextGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var healthTMP = healthTextGO.AddComponent<TextMeshPro>();
        healthTMP.text = "30";
        healthTMP.fontSize = 4f;
        healthTMP.fontStyle = FontStyles.Bold;
        healthTMP.alignment = TextAlignmentOptions.Center;
        healthTMP.color = Color.white;
        healthTMP.sortingLayerID = SortingLayer.NameToID("Board");
        healthTMP.sortingOrder = 4;
        var healthRect = healthTextGO.GetComponent<RectTransform>();
        if (healthRect != null) healthRect.sizeDelta = new Vector2(1f, 0.5f);

        // =====================================================
        // ARMOR BADGE (à gauche du health)
        // =====================================================
        var armorBadgeGO = new GameObject("ArmorBadge");
        armorBadgeGO.transform.SetParent(root.transform, false);
        armorBadgeGO.transform.localPosition = new Vector3(-0.35f, -0.45f, 0f);
        armorBadgeGO.transform.localScale = Vector3.one * 0.55f;
        var armorBadgeSR = armorBadgeGO.AddComponent<SpriteRenderer>();
        armorBadgeSR.sprite = armorSprite;
        armorBadgeSR.sortingLayerName = "Board";
        armorBadgeSR.sortingOrder = 3;
        armorBadgeSR.color = new Color(0.7f, 0.75f, 0.8f, 1f); // Gris-bleu pour l'armure

        // Armor Text
        var armorTextGO = new GameObject("ArmorText");
        armorTextGO.transform.SetParent(armorBadgeGO.transform, false);
        armorTextGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var armorTMP = armorTextGO.AddComponent<TextMeshPro>();
        armorTMP.text = "5";
        armorTMP.fontSize = 3.5f;
        armorTMP.fontStyle = FontStyles.Bold;
        armorTMP.alignment = TextAlignmentOptions.Center;
        armorTMP.color = Color.white;
        armorTMP.sortingLayerID = SortingLayer.NameToID("Board");
        armorTMP.sortingOrder = 4;
        var armorRect = armorTextGO.GetComponent<RectTransform>();
        if (armorRect != null) armorRect.sizeDelta = new Vector2(1f, 0.5f);

        // =====================================================
        // NAME TEXT (sous le portrait)
        // =====================================================
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        nameGO.transform.localPosition = new Vector3(0f, 0.5f, -0.01f);
        var nameTMP = nameGO.AddComponent<TextMeshPro>();
        nameTMP.text = "";
        nameTMP.fontSize = 2.8f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = new Color(1f, 0.9f, 0.6f, 1f);
        nameTMP.sortingLayerID = SortingLayer.NameToID("Board");
        nameTMP.sortingOrder = 2;
        var nameRect = nameGO.GetComponent<RectTransform>();
        if (nameRect != null) nameRect.sizeDelta = new Vector2(2f, 0.4f);

        // =====================================================
        // COMPOSANT + CÂBLAGE
        // =====================================================
        var display = root.AddComponent<AutoBattler.Client.UI.HeroPortraitDisplay>();
        var so = new SerializedObject(display);
        so.FindProperty("portraitFrame").objectReferenceValue = frameSR;
        so.FindProperty("portraitArt").objectReferenceValue = artSR;
        so.FindProperty("healthBadge").objectReferenceValue = healthBadgeSR;
        so.FindProperty("healthText").objectReferenceValue = healthTMP;
        so.FindProperty("armorBadge").objectReferenceValue = armorBadgeSR;
        so.FindProperty("armorText").objectReferenceValue = armorTMP;
        so.FindProperty("nameText").objectReferenceValue = nameTMP;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(root);
        Debug.Log($"[Setup] HeroPortrait créé à {pos} !");
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindByPath(string path)
    {
        var parts = path.Split('/');
        GameObject current = null;
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name == parts[0]) { current = root; break; }
        }
        if (current == null) return null;
        for (int i = 1; i < parts.Length; i++)
        {
            var child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }
        return current;
    }
}
