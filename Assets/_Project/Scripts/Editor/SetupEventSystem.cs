using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;

public class SetupEventSystem
{
    public static void Execute()
    {
        // Vérifier s'il y en a déjà un
        var existing = Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            Debug.Log($"[Setup] EventSystem déjà présent : {existing.gameObject.name}");
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // Placer sous --- Core ---
        var core = GameObject.Find("--- Core ---");
        if (core != null)
            go.transform.SetParent(core.transform, false);

        EditorUtility.SetDirty(go);
        Debug.Log("[Setup] EventSystem créé avec InputSystemUIInputModule !");
    }
}
