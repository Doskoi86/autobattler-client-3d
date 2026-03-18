using UnityEngine;
using UnityEditor;
using TMPro;

public class SetupHeroPower
{
    public static void Execute()
    {
        // Vérifier si le bouton existe déjà
        var existing = GameObject.Find("Button_HeroPower");
        if (existing != null)
        {
            Debug.Log("[Setup] Button_HeroPower existe déjà, suppression...");
            Object.DestroyImmediate(existing);
        }

        // Créer le bouton Hero Power (structure identique aux autres WorldButtons)
        var buttonGO = new GameObject("Button_HeroPower");
        buttonGO.transform.position = new Vector3(5.2f, 0.01f, -0.58f);
        buttonGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        buttonGO.transform.localScale = new Vector3(1.8f, 1.0f, 1f);

        // BoxCollider
        var collider = buttonGO.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 1f, 0.02f);

        // Background — SpriteRenderer
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(buttonGO.transform, false);
        var bgSR = bgGO.AddComponent<SpriteRenderer>();
        bgSR.sortingLayerName = "Board";
        bgSR.sortingOrder = 10;
        bgSR.color = new Color(0.4f, 0.2f, 0.5f, 1f);
        // Utiliser le même sprite que les autres boutons si possible
        var readyButton = GameObject.Find("Button_Ready");
        if (readyButton != null)
        {
            var readyBG = readyButton.transform.Find("Background")?.GetComponent<SpriteRenderer>();
            if (readyBG != null && readyBG.sprite != null)
                bgSR.sprite = readyBG.sprite;
        }

        // CostLabel — TextMeshPro 3D (en haut)
        var costGO = new GameObject("CostLabel");
        costGO.transform.SetParent(buttonGO.transform, false);
        costGO.transform.localPosition = new Vector3(0f, 0.2f, -0.01f);
        var costTMP = costGO.AddComponent<TextMeshPro>();
        costTMP.text = "Pouvoir";
        costTMP.fontSize = 3.5f;
        costTMP.fontStyle = FontStyles.Bold;
        costTMP.alignment = TextAlignmentOptions.Center;
        costTMP.color = new Color(1f, 0.9f, 0.5f, 1f);
        costTMP.sortingLayerID = SortingLayer.NameToID("Board");
        costTMP.sortingOrder = 11;
        var costRect = costGO.GetComponent<RectTransform>();
        if (costRect != null) costRect.sizeDelta = new Vector2(1f, 0.5f);

        // PowerLabel — TextMeshPro 3D (en bas)
        var powerGO = new GameObject("PowerLabel");
        powerGO.transform.SetParent(buttonGO.transform, false);
        powerGO.transform.localPosition = new Vector3(0f, -0.15f, -0.01f);
        var powerTMP = powerGO.AddComponent<TextMeshPro>();
        powerTMP.text = "";
        powerTMP.fontSize = 2.5f;
        powerTMP.alignment = TextAlignmentOptions.Center;
        powerTMP.color = Color.white;
        powerTMP.sortingLayerID = SortingLayer.NameToID("Board");
        powerTMP.sortingOrder = 11;
        var powerRect = powerGO.GetComponent<RectTransform>();
        if (powerRect != null) powerRect.sizeDelta = new Vector2(1f, 0.4f);

        // Ajouter le composant HeroPowerButton
        var hpb = buttonGO.AddComponent<AutoBattler.Client.UI.HeroPowerButton>();

        // Câbler les références
        var so = new SerializedObject(hpb);
        so.FindProperty("backgroundRenderer").objectReferenceValue = bgSR;
        so.FindProperty("costLabel").objectReferenceValue = costTMP;
        so.FindProperty("powerLabel").objectReferenceValue = powerTMP;
        so.ApplyModifiedProperties();

        // Placer sous --- Surface --- / --- Actions ---
        var actionsParent = FindByPath("--- Surface ---/--- Actions ---");
        if (actionsParent != null)
            buttonGO.transform.SetParent(actionsParent.transform, true);

        // Ajouter aux recruitOnlyObjects du PhaseLayoutManager
        var plmGO = GameObject.Find("PhaseLayoutManager");
        if (plmGO != null)
        {
            var plm = plmGO.GetComponent<AutoBattler.Client.Board.PhaseLayoutManager>();
            if (plm != null)
            {
                var plmSO = new SerializedObject(plm);
                var recruitProp = plmSO.FindProperty("recruitOnlyObjects");
                int newSize = recruitProp.arraySize + 1;
                recruitProp.arraySize = newSize;
                recruitProp.GetArrayElementAtIndex(newSize - 1).objectReferenceValue = buttonGO;
                plmSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(plmGO);
            }
        }

        EditorUtility.SetDirty(buttonGO);
        Debug.Log("[Setup] Button_HeroPower créé et câblé !");
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
