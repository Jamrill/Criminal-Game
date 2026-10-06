using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MixamoFixOnce
{
    static MixamoFixOnce() { EditorApplication.update += Run; }
    static void Run()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        EditorApplication.update -= Run;
        const string key = "MixamoFix.JumpSprintTurns.v3";
        if (SessionState.GetBool(key, false)) return;
        SessionState.SetBool(key, true);
        try
        {
            PlayerAnimationExpansion.ConfigureAndValidate();
            System.IO.File.WriteAllText("Documentation/FemaleWalkSetupResult.txt", "PASS " + System.DateTime.Now.ToString("O"));
        }
        catch (System.Exception e)
        {
            System.IO.File.WriteAllText("Documentation/FemaleWalkSetupResult.txt", e.ToString());
            Debug.LogException(e);
        }
    }
}
