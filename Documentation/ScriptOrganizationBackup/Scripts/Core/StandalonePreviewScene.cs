using UnityEngine;

namespace JuegoCriminal.Core
{
    /// <summary>
    /// Marks a scene that should run on its own without entering the normal game flow.
    /// Useful for visual tests, shaders and isolated prototypes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StandalonePreviewScene : MonoBehaviour
    {
    }
}
