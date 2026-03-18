using UnityEngine;
using UnityEditor;

public class VerifySetup
{
    public static void Execute()
    {
        // Vérifier CombatSequencer
        var seqGO = GameObject.Find("CombatSequencer");
        if (seqGO != null)
        {
            var seq = seqGO.GetComponent<AutoBattler.Client.Combat.CombatSequencer>();
            if (seq != null)
            {
                var so = new SerializedObject(seq);
                var bm = so.FindProperty("boardManager");
                var ro = so.FindProperty("resultOverlay");
                Debug.Log($"[Verify] CombatSequencer: boardManager={bm.objectReferenceValue != null}, resultOverlay={ro.objectReferenceValue != null}");
            }
        }

        // Vérifier PhaseLayoutManager
        var plmGO = GameObject.Find("PhaseLayoutManager");
        if (plmGO != null)
        {
            var plm = plmGO.GetComponent<AutoBattler.Client.Board.PhaseLayoutManager>();
            if (plm != null)
            {
                var so = new SerializedObject(plm);
                var recruit = so.FindProperty("recruitOnlyObjects");
                var combat = so.FindProperty("combatOnlyObjects");
                Debug.Log($"[Verify] PhaseLayoutManager: recruit={recruit.arraySize}, combat={combat.arraySize}");

                for (int i = 0; i < recruit.arraySize; i++)
                {
                    var el = recruit.GetArrayElementAtIndex(i);
                    var obj = el.objectReferenceValue as GameObject;
                    Debug.Log($"  recruit[{i}] = {(obj != null ? obj.name : "null")}");
                }
                for (int i = 0; i < combat.arraySize; i++)
                {
                    var el = combat.GetArrayElementAtIndex(i);
                    var obj = el.objectReferenceValue as GameObject;
                    Debug.Log($"  combat[{i}] = {(obj != null ? obj.name : "null")}");
                }
            }
        }

        // Vérifier CombatResultOverlay
        var overlayGO = GameObject.Find("CombatResultOverlay");
        if (overlayGO != null)
        {
            var overlay = overlayGO.GetComponent<AutoBattler.Client.Combat.CombatResultOverlay>();
            if (overlay != null)
            {
                var so = new SerializedObject(overlay);
                Debug.Log($"[Verify] CombatResultOverlay: canvas={so.FindProperty("resultCanvas").objectReferenceValue != null}, " +
                    $"bg={so.FindProperty("backgroundImage").objectReferenceValue != null}, " +
                    $"resultText={so.FindProperty("resultText").objectReferenceValue != null}, " +
                    $"damageText={so.FindProperty("damageText").objectReferenceValue != null}");
            }
        }
    }
}
