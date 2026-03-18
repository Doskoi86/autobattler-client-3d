using UnityEngine;
using UnityEditor;

public class SetupCombatSequencer
{
    public static void Execute()
    {
        // Câbler CombatSequencer
        var sequencerGO = GameObject.Find("CombatSequencer");
        if (sequencerGO == null)
        {
            Debug.LogError("CombatSequencer introuvable !");
            return;
        }

        var sequencer = sequencerGO.GetComponent<AutoBattler.Client.Combat.CombatSequencer>();
        if (sequencer == null)
        {
            Debug.LogError("Composant CombatSequencer introuvable !");
            return;
        }

        var boardManagerGO = GameObject.Find("BoardManager");
        var overlayGO = GameObject.Find("CombatResultOverlay");

        var so = new SerializedObject(sequencer);

        if (boardManagerGO != null)
        {
            var bm = boardManagerGO.GetComponent<AutoBattler.Client.Board.BoardManager>();
            if (bm != null)
                so.FindProperty("boardManager").objectReferenceValue = bm;
        }

        if (overlayGO != null)
        {
            var ro = overlayGO.GetComponent<AutoBattler.Client.Combat.CombatResultOverlay>();
            if (ro != null)
                so.FindProperty("resultOverlay").objectReferenceValue = ro;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequencerGO);

        Debug.Log("[Setup] CombatSequencer câblé !");

        // Câbler PhaseLayoutManager — recruit/combat objects
        SetupPhaseLayoutManager();
    }

    private static void SetupPhaseLayoutManager()
    {
        var plmGO = GameObject.Find("PhaseLayoutManager");
        if (plmGO == null)
        {
            Debug.LogError("PhaseLayoutManager introuvable !");
            return;
        }

        var plm = plmGO.GetComponent<AutoBattler.Client.Board.PhaseLayoutManager>();
        if (plm == null)
        {
            Debug.LogError("Composant PhaseLayoutManager introuvable !");
            return;
        }

        var so = new SerializedObject(plm);

        // Recruit Only Objects : shop, boutons, gold, hand
        var recruitProp = so.FindProperty("recruitOnlyObjects");
        var recruitObjects = new GameObject[]
        {
            GameObject.Find("ShopManager"),
            GameObject.Find("ShopButtonsController"),
            GameObject.Find("HandManager"),
            FindByPath("--- Surface ---/--- Gold ---"),
            FindByPath("--- Surface ---/--- Actions ---/Button_Refresh"),
            FindByPath("--- Surface ---/--- Actions ---/Button_Freeze"),
            FindByPath("--- Surface ---/--- Actions ---/Button_Upgrade"),
            FindByPath("--- Surface ---/--- Actions ---/Button_Ready"),
            FindByPath("--- Surface ---/--- Actions ---/DropZone_Sell"),
            FindByPath("--- Surface ---/--- Actions ---/DropZone_Board"),
            FindByPath("--- Surface ---/--- Shop ---")
        };

        // Filtrer les nulls
        var validRecruit = new System.Collections.Generic.List<GameObject>();
        foreach (var go in recruitObjects)
        {
            if (go != null) validRecruit.Add(go);
        }

        recruitProp.arraySize = validRecruit.Count;
        for (int i = 0; i < validRecruit.Count; i++)
        {
            recruitProp.GetArrayElementAtIndex(i).objectReferenceValue = validRecruit[i];
        }

        // Combat Only Objects : board adverse
        var combatProp = so.FindProperty("combatOnlyObjects");
        var combatObjects = new System.Collections.Generic.List<GameObject>();
        var opponentBoard = FindByPath("--- Surface ---/--- Board ---/--- Opponent ---");
        if (opponentBoard != null)
            combatObjects.Add(opponentBoard);

        combatProp.arraySize = combatObjects.Count;
        for (int i = 0; i < combatObjects.Count; i++)
        {
            combatProp.GetArrayElementAtIndex(i).objectReferenceValue = combatObjects[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(plmGO);

        Debug.Log($"[Setup] PhaseLayoutManager câblé : {validRecruit.Count} recruit, {combatObjects.Count} combat objects");
    }

    private static GameObject FindByPath(string path)
    {
        // Unity's Find ne gère pas les paths avec /, on cherche récursivement
        var parts = path.Split('/');
        GameObject current = null;

        // Trouver le root
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }

        if (current == null) return null;

        // Descendre le path
        for (int i = 1; i < parts.Length; i++)
        {
            var child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }

        return current;
    }
}
