using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;

public class SortingLayerSetup
{
    // Les layers dans l'ordre de rendu (bas → haut)
    private static readonly string[] Layers = {
        "Background",  // Plateau de jeu
        "Board",       // Tokens sur le board
        "Shop",        // Tokens dans le shop
        "Hand",        // Cartes en main
        "Drag",        // Objet en cours de drag
        "Tooltip",     // Preview tooltip
        "Overlay"      // Transitions, écrans blur, résultats
    };

    public static void Execute()
    {
        // Créer les Sorting Layers
        foreach (var layer in Layers)
        {
            AddSortingLayer(layer);
        }

        Debug.Log($"[SortingLayerSetup] {Layers.Length} sorting layers créés/vérifiés");
        Debug.Log("[SortingLayerSetup] Vérifier dans Edit → Project Settings → Tags and Layers → Sorting Layers");
    }

    private static void AddSortingLayer(string layerName)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var sortingLayers = tagManager.FindProperty("m_SortingLayers");

        // Vérifier si le layer existe déjà
        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName)
            {
                Debug.Log($"[SortingLayerSetup] Layer '{layerName}' existe déjà");
                return;
            }
        }

        // Ajouter le layer
        sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
        var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
        newLayer.FindPropertyRelative("name").stringValue = layerName;
        newLayer.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode();

        tagManager.ApplyModifiedProperties();
        Debug.Log($"[SortingLayerSetup] Layer '{layerName}' créé");
    }
}
