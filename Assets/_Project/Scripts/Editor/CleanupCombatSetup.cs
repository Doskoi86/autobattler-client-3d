using UnityEngine;
using UnityEditor;

public class CleanupCombatSetup
{
    public static void Execute()
    {
        // Nettoyer la référence resultOverlay du CombatSequencer (le GO a été supprimé)
        var seqGO = GameObject.Find("CombatSequencer");
        if (seqGO != null)
        {
            var seq = seqGO.GetComponent<AutoBattler.Client.Combat.CombatSequencer>();
            if (seq != null)
            {
                var so = new SerializedObject(seq);
                var prop = so.FindProperty("resultOverlay");
                if (prop != null)
                {
                    prop.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(seqGO);
                Debug.Log("[Cleanup] CombatSequencer.resultOverlay nettoyé");
            }
        }

        // Vérifier qu'il n'y a pas d'erreurs de compilation
        Debug.Log("[Cleanup] Terminé !");
    }
}
