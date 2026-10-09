using System.Collections;
using UnityEngine;
using JuegoCriminal.Environment;
namespace JuegoCriminal.Core
{
    public static class LoadingShaderPreparation
    {
        public static bool IsPreparing { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => IsPreparing=false;
        public static IEnumerator Prepare()
        {
            // Let runtime reflection/material owners initialize behind the loading panel.
            yield return null;
            yield return null;
            IsPreparing=true;
#if UNITY_EDITOR
            bool previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
#endif
            try
            {
                yield return null;
                // Includes currently loaded scene, catalog and rendering-pipeline shaders.
                // Does not claim to cover future assets or every platform-specific PSO.
                Shader.WarmupAllShaders();
                foreach(var cycle in Object.FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None))
                    yield return cycle.PrepareLightingVariants();
#if UNITY_EDITOR
                while(UnityEditor.ShaderUtil.anythingCompiling) yield return null;
#endif
            }
            finally
            {
#if UNITY_EDITOR
                UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
#endif
                foreach(var reflections in Object.FindObjectsByType<ReflectionQualityController>(FindObjectsSortMode.None))
                    reflections.InvalidateAfterLightingPreparation();
                IsPreparing=false;
            }
        }
    }
}
