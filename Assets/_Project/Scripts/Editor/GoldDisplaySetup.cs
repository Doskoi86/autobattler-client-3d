using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class GoldDisplaySetup
{
    public static void Execute()
    {
        // Trouver le GoldAnchor
        var boardSurface = GameObject.Find("BoardSurface");
        Vector3 goldPos = new Vector3(-4f, 0.1f, -4.5f);
        if (boardSurface != null)
        {
            var anchor = boardSurface.transform.Find("GoldAnchor");
            if (anchor != null) goldPos = anchor.position;
        }

        // Créer le GoldDisplay
        var go = new GameObject("GoldDisplay");
        go.transform.position = goldPos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Background (coin-like circle)
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cards/carteRondNiveau.png");
        if (sprite != null) bgSr.sprite = sprite;
        bgSr.sortingOrder = 10;
        bgSr.color = new Color(1f, 0.85f, 0.2f, 1f); // Doré
        bg.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        // Gold text
        var textGo = new GameObject("GoldText");
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = "0";
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        // Label "Or" à côté
        var labelGo = new GameObject("GoldLabel");
        labelGo.transform.SetParent(go.transform, false);
        var labelTmp = labelGo.AddComponent<TextMeshPro>();
        labelTmp.text = "Or";
        labelTmp.fontSize = 2.5f;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.color = new Color(1f, 0.85f, 0.3f, 1f);
        labelGo.transform.localPosition = new Vector3(0.4f, 0f, -0.01f);

        EditorUtility.SetDirty(go);

        // Câbler dans ShopButtonsController
        var sbc = GameObject.Find("ShopButtonsController");
        if (sbc != null)
        {
            var comp = sbc.GetComponent<AutoBattler.Client.UI.ShopButtonsController>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                so.FindProperty("goldDisplay").objectReferenceValue = tmp;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(sbc);
                Debug.Log("[GoldDisplaySetup] goldDisplay câblé dans ShopButtonsController");
            }
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[GoldDisplaySetup] GoldDisplay créé à {goldPos}");
    }
}
