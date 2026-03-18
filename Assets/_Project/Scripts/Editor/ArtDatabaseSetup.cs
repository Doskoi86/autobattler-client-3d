using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ArtDatabaseSetup
{
    public static void Execute()
    {
        // Charger le SO
        var db = AssetDatabase.LoadAssetAtPath<AutoBattler.Client.Cards.MinionArtDatabase>(
            "Assets/_Project/Data/MinionArtDatabase.asset");
        if (db == null)
        {
            Debug.LogError("[ArtDatabaseSetup] MinionArtDatabase.asset introuvable dans Assets/_Project/Data/");
            return;
        }

        // Charger le sprite par défaut
        var defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cards/Art/dragon.png");

        // Tous les minions du JSON avec leurs noms exacts
        string[] allMinions = {
            // Tier 1
            "Recrue Humaine",
            "Louveteau",
            "Méca-Assembleur",
            "Diablotin",
            "Dragonnet",
            "Elfe Archer",
            // Tier 2
            "Garde Taurène",
            "Loup Alpha",
            "Méca-Blindé",
            "Nécro-Servant",
            "Fée Soigneuse",
            // Tier 3
            "Champion Orc",
            "Dragon de Bronze",
            "Golem de Fer",
            "Démon Vorace",
            "Esprit Sylvestre",
            // Tier 4
            "Seigneur de Guerre",
            "Hydre Sauvage",
            "Mécageant",
            "Liche Mineure",
            // Tier 5
            "Roi Dragon",
            "Berserker Orc",
            "Archi-Fée",
            // Tier 6
            "Aspect Draconique",
            "Titan Mécanique",
            "Archidémon"
        };

        var so = new SerializedObject(db);
        var entries = so.FindProperty("entries");
        var defaultArt = so.FindProperty("defaultArtwork");

        // Vider les entrées existantes
        entries.ClearArray();

        // Ajouter chaque minion
        for (int i = 0; i < allMinions.Length; i++)
        {
            entries.InsertArrayElementAtIndex(i);
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("minionName").stringValue = allMinions[i];
            entry.FindPropertyRelative("artwork").objectReferenceValue = null; // À remplir manuellement
        }

        // Default artwork = dragon
        if (defaultSprite != null)
            defaultArt.objectReferenceValue = defaultSprite;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // Câbler dans CardFactory
        var cf = GameObject.Find("CardFactory");
        if (cf != null)
        {
            var comp = cf.GetComponent<AutoBattler.Client.Cards.CardFactory>();
            if (comp != null)
            {
                var cfSo = new SerializedObject(comp);
                cfSo.FindProperty("minionArtDatabase").objectReferenceValue = db;
                cfSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(cf);
            }
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[ArtDatabaseSetup] {allMinions.Length} entrées créées, default=dragon, câblé dans CardFactory");
        Debug.Log("[ArtDatabaseSetup] Ouvrir Assets/_Project/Data/MinionArtDatabase et glisser les sprites manquants");
    }
}
