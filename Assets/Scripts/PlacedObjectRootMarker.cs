using UnityEngine;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Marks the GameObject that should be treated as the destroy/reset root for a placed object.
    /// This avoids accidentally destroying the XROrigin when placed content lives under anchors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedObjectRootMarker : MonoBehaviour
    {
    }
}
