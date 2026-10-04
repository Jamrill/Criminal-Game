using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MixamoFixOnce
{
    static MixamoFixOnce() { EditorApplication.update += Run; }
    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Run;
        const string key = "MixamoFix.Draw2x.InPlace.v1";
        if (SessionState.GetBool(key, false)) return;
        SessionState.SetBool(key, true);
        try { PlayerAnimationExpansion.ConfigureAndValidate(); }
        catch (System.Exception e) { Debug.LogException(e); }
    }
}
