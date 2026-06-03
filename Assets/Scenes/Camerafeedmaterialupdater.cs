using UnityEngine;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Attach this to the root (or any child with a Renderer) of your camera-feed prefab.
    ///
    /// On Start it finds the CameraFeedReceiver singleton and binds its LiveTexture
    /// to this object's material. Because XRRayARPlanePlacer calls Instantiate on the
    /// prefab, every placed copy automatically gets the live feed — no placer changes needed.
    ///
    /// Setup checklist for the prefab:
    ///   1. Create a Quad (or keep your cube — anything with a MeshRenderer).
    ///   2. Assign a material that uses the Standard shader (or Unlit/Texture for brighter output).
    ///   3. Add this script to the same GameObject as the Renderer.
    ///   4. (Optional) Tick 'Use Unlit Material' below to auto-create a clean unlit material
    ///      so the feed isn't affected by scene lighting.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class CameraFeedMaterialUpdater : MonoBehaviour
    {
        [Header("Material")]
        [Tooltip("If true, an Unlit/Texture material is created at runtime so the feed " +
                 "is not dimmed by scene lighting. Recommended for camera feeds.")]
        public bool useUnlitMaterial = true;

        [Tooltip("Name of the texture property to set on the material. " +
                 "Standard shader: '_MainTex'. Unlit/Texture: '_MainTex'. URP Lit: '_BaseMap'.")]
        public string texturePropertyName = "_MainTex";

        [Header("Aspect Ratio")]
        [Tooltip("Automatically adjust this object's X scale to match the incoming frame's " +
                 "aspect ratio so the feed is never stretched.")]
        public bool autoAspectRatio = true;

        Renderer  m_Renderer;
        Material  m_Material;      // instance material — safe to modify
        bool      m_TextureBound;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();

            if (useUnlitMaterial)
            {
                // Create a private material instance so multiple placed objects don't share state
                m_Material = new Material(Shader.Find("Unlit/Texture"));
                m_Renderer.material = m_Material;
            }
            else
            {
                // Use an instance of whatever material is already assigned
                m_Material = m_Renderer.material; // .material already returns an instance
            }
        }

        void Start()
        {
            if (CameraFeedReceiver.Instance == null)
            {
                Debug.LogWarning("[CameraFeedMaterialUpdater] No CameraFeedReceiver found in scene. " +
                                 "Add the CameraFeedReceiver prefab/component to your AR1 scene.");
                return;
            }

            // Bind immediately if a frame is already available
            TryBindTexture();
        }

        void Update()
        {
            if (!m_TextureBound)
            {
                // Keep trying until the first frame arrives
                TryBindTexture();
                return;
            }

            // Once bound, just keep aspect ratio in sync as resolution can change
            if (autoAspectRatio)
                ApplyAspectRatio();
        }

        void TryBindTexture()
        {
            if (CameraFeedReceiver.Instance == null)
                return;

            var tex = CameraFeedReceiver.Instance.LiveTexture;
            if (tex == null)
                return;

            m_Material.SetTexture(texturePropertyName, tex);
            m_TextureBound = true;

            if (autoAspectRatio)
                ApplyAspectRatio();

            Debug.Log("[CameraFeedMaterialUpdater] Live texture bound successfully.");
        }

        void ApplyAspectRatio()
        {
            var tex = CameraFeedReceiver.Instance?.LiveTexture;
            if (tex == null || tex.height == 0)
                return;

            float aspect = (float)tex.width / tex.height;
            var s = transform.localScale;

            // Keep Y and Z as authored; only adjust X for aspect ratio
            // For a Quad the authored scale is usually (1,1,1) making it 1:1.
            // We store the Y scale as the "height unit" and derive X from it.
            transform.localScale = new Vector3(s.y * aspect, s.y, s.z);
        }

        void OnDestroy()
        {
            // Clean up the instance material to avoid memory leaks
            if (m_Material != null)
                Destroy(m_Material);
        }
    }
}