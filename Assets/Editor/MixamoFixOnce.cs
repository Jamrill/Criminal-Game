using UnityEditor;
using UnityEngine;

public static class MixamoFixOnce
{
    // Legacy migration. Run explicitly only: reopening Unity must never regenerate
    // an already configured controller or execute historical validation tests.
    public static void Run()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return;
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
