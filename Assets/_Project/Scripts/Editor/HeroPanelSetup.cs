using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class HeroPanelSetup
{
    public static void Execute()
    {
        // === Créer le prefab PlayerListEntry dans la scène ===
        var entry = new GameObject("PlayerListEntry");
        entry.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        entry.AddComponent<AutoBattler.Client.UI.PlayerListEntry>();

        // Background
        var bg = new GameObject("Background");
        bg.transform.SetParent(entry.transform, false);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cards/carteFondBlanc.png");
        if (sprite != null) bgSr.sprite = sprite;
        bgSr.sortingOrder = 10;
        bgSr.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        bg.transform.localScale = new Vector3(0.4f, 0.15f, 1f);

        // HealthBar
        var bar = new GameObject("HealthBar");
        bar.transform.SetParent(entry.transform, false);
        var barSr = bar.AddComponent<SpriteRenderer>();
        if (sprite != null) barSr.sprite = sprite;
        barSr.sortingOrder = 11;
        barSr.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        bar.transform.localPosition = new Vector3(-0.15f, -0.05f, -0.01f);
        bar.transform.localScale = new Vector3(0.35f, 0.03f, 1f);

        // NameText
        var nameGo = new GameObject("NameText");
        nameGo.transform.SetParent(entry.transform, false);
        var nameTmp = nameGo.AddComponent<TextMeshPro>();
        nameTmp.text = "Player";
        nameTmp.fontSize = 2f;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.color = Color.white;
        nameGo.transform.localPosition = new Vector3(-0.35f, 0.02f, -0.01f);

        // HealthText
        var hpGo = new GameObject("HealthText");
        hpGo.transform.SetParent(entry.transform, false);
        var hpTmp = hpGo.AddComponent<TextMeshPro>();
        hpTmp.text = "30";
        hpTmp.fontSize = 2f;
        hpTmp.alignment = TextAlignmentOptions.Right;
        hpTmp.color = Color.white;
        hpGo.transform.localPosition = new Vector3(0.3f, 0.02f, -0.01f);

        // TierText
        var tierGo = new GameObject("TierText");
        tierGo.transform.SetParent(entry.transform, false);
        var tierTmp = tierGo.AddComponent<TextMeshPro>();
        tierTmp.text = "T1";
        tierTmp.fontSize = 1.5f;
        tierTmp.alignment = TextAlignmentOptions.Center;
        tierTmp.color = new Color(1f, 0.85f, 0.3f, 1f);
        tierGo.transform.localPosition = new Vector3(0.35f, 0.02f, -0.01f);

        // Wire PlayerListEntry references
        var ple = entry.GetComponent<AutoBattler.Client.UI.PlayerListEntry>();
        var so = new SerializedObject(ple);
        so.FindProperty("backgroundRenderer").objectReferenceValue = bgSr;
        so.FindProperty("healthBarRenderer").objectReferenceValue = barSr;
        so.FindProperty("nameText").objectReferenceValue = nameTmp;
        so.FindProperty("healthText").objectReferenceValue = hpTmp;
        so.FindProperty("tierText").objectReferenceValue = tierTmp;
        so.ApplyModifiedProperties();

        // Save as prefab
        var prefabPath = "Assets/_Project/Prefabs/PlayerListEntry.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(entry, prefabPath, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(entry);
        Debug.Log($"[HeroPanelSetup] Prefab créé : {prefabPath}");

        // === Créer le HeroPanel dans la scène ===
        var panelGo = GameObject.Find("HeroPanel");
        if (panelGo == null)
        {
            panelGo = new GameObject("HeroPanel");
        }

        // Positionner à gauche
        var heroPanelAnchor = GameObject.Find("BoardSurface")?.transform.Find("HeroPanelAnchor");
        if (heroPanelAnchor != null)
            panelGo.transform.position = heroPanelAnchor.position;
        else
            panelGo.transform.position = new Vector3(-9f, 0.1f, 4f);

        var hp = panelGo.GetComponent<AutoBattler.Client.UI.HeroPanel>();
        if (hp == null)
            hp = panelGo.AddComponent<AutoBattler.Client.UI.HeroPanel>();

        // Wire prefab reference
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var hpSo = new SerializedObject(hp);
        hpSo.FindProperty("entryPrefab").objectReferenceValue = prefab;
        hpSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(panelGo);

        Debug.Log("[HeroPanelSetup] HeroPanel créé et câblé");

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HeroPanelSetup] Scene saved");
    }
}
