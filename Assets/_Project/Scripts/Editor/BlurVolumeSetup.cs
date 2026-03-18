using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public class BlurVolumeSetup
{
    public static void Execute()
    {
        // Créer le Volume Profile
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Ajouter Depth of Field (Gaussian blur)
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(0f);
        dof.gaussianEnd.Override(0.1f);  // Très proche = tout est flou
        dof.gaussianMaxRadius.Override(1f);

        // Sauvegarder le profile
        var profilePath = "Assets/_Project/Data/TransitionBlurProfile.asset";
        AssetDatabase.CreateAsset(profile, profilePath);

        // Créer le Volume GameObject
        var overlay = GameObject.Find("PhaseTransitionOverlay");
        if (overlay == null)
        {
            Debug.LogError("[BlurVolumeSetup] PhaseTransitionOverlay introuvable !");
            return;
        }

        var volumeGo = new GameObject("BlurVolume");
        volumeGo.transform.SetParent(overlay.transform, false);

        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10; // Au-dessus du Global Volume
        volume.weight = 0f;   // Désactivé par défaut
        volume.sharedProfile = profile;

        EditorUtility.SetDirty(volumeGo);

        // Câbler le Volume dans PhaseTransitionOverlay
        var comp = overlay.GetComponent<AutoBattler.Client.UI.PhaseTransitionOverlay>();
        if (comp != null)
        {
            var so = new SerializedObject(comp);
            var prop = so.FindProperty("blurVolume");
            if (prop != null)
            {
                prop.objectReferenceValue = volume;
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(overlay);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BlurVolumeSetup] BlurVolume créé, profile: {profilePath}");
    }
}
