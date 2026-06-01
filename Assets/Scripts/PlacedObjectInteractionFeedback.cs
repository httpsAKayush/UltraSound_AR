using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Gives placed interactables a visible hover/select state without modifying shared materials.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacedObjectInteractionFeedback : MonoBehaviour
    {
        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_ColorId = Shader.PropertyToID("_Color");
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly Color s_HoverColor = new(0.25f, 0.9f, 1f, 1f);
        static readonly Color s_SelectColor = new(1f, 0.78f, 0.2f, 1f);
        const float kHoverScaleMultiplier = 1.03f;
        const float kSelectScaleMultiplier = 1.08f;

        [SerializeField]
        [Tooltip("Interactable that drives the hover/select state.")]
        XRBaseInteractable m_Interactable;

        [SerializeField]
        [Tooltip("Renderers tinted during hover/select. Defaults to all child renderers.")]
        Renderer[] m_Renderers;

        MaterialPropertyBlock m_PropertyBlock;
        Vector3 m_OriginalScale;
        int m_HoverCount;
        int m_SelectCount;
        bool m_Initialized;

        void Reset()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
            m_Renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void OnEnable()
        {
            EnsureInitialized();

            if (m_Interactable == null)
                return;

            m_Interactable.firstHoverEntered.AddListener(OnFirstHoverEntered);
            m_Interactable.lastHoverExited.AddListener(OnLastHoverExited);
            m_Interactable.firstSelectEntered.AddListener(OnFirstSelectEntered);
            m_Interactable.lastSelectExited.AddListener(OnLastSelectExited);
            ApplyVisualState();
        }

        void OnDisable()
        {
            if (m_Interactable != null)
            {
                m_Interactable.firstHoverEntered.RemoveListener(OnFirstHoverEntered);
                m_Interactable.lastHoverExited.RemoveListener(OnLastHoverExited);
                m_Interactable.firstSelectEntered.RemoveListener(OnFirstSelectEntered);
                m_Interactable.lastSelectExited.RemoveListener(OnLastSelectExited);
            }

            m_HoverCount = 0;
            m_SelectCount = 0;
            ClearVisuals();
        }

        void OnFirstHoverEntered(HoverEnterEventArgs _)
        {
            m_HoverCount++;
            ApplyVisualState();
        }

        void OnLastHoverExited(HoverExitEventArgs _)
        {
            m_HoverCount = Mathf.Max(0, m_HoverCount - 1);
            ApplyVisualState();
        }

        void OnFirstSelectEntered(SelectEnterEventArgs _)
        {
            m_SelectCount++;
            ApplyVisualState();
        }

        void OnLastSelectExited(SelectExitEventArgs _)
        {
            m_SelectCount = Mathf.Max(0, m_SelectCount - 1);
            ApplyVisualState();
        }

        void ApplyVisualState()
        {
            EnsureInitialized();

            if (m_SelectCount > 0)
            {
                ApplyTint(s_SelectColor, kSelectScaleMultiplier, 1.6f);
                return;
            }

            if (m_HoverCount > 0)
            {
                ApplyTint(s_HoverColor, kHoverScaleMultiplier, 0.8f);
                return;
            }

            ClearVisuals();
        }

        void ApplyTint(Color tintColor, float scaleMultiplier, float emissionIntensity)
        {
            EnsureInitialized();

            transform.localScale = m_OriginalScale * scaleMultiplier;

            m_PropertyBlock.Clear();
            m_PropertyBlock.SetColor(s_BaseColorId, tintColor);
            m_PropertyBlock.SetColor(s_ColorId, tintColor);
            m_PropertyBlock.SetColor(s_EmissionColorId, tintColor * emissionIntensity);

            for (var i = 0; i < m_Renderers.Length; ++i)
            {
                var renderer = m_Renderers[i];
                if (renderer == null)
                    continue;

                renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void ClearVisuals()
        {
            if (!m_Initialized)
                return;

            transform.localScale = m_OriginalScale;
            m_PropertyBlock.Clear();

            if (m_Renderers == null)
                return;

            for (var i = 0; i < m_Renderers.Length; ++i)
            {
                var renderer = m_Renderers[i];
                if (renderer == null)
                    continue;

                renderer.SetPropertyBlock(null);
            }
        }

        void EnsureInitialized()
        {
            if (m_Initialized)
                return;

            if (m_Interactable == null)
                m_Interactable = GetComponent<XRBaseInteractable>();

            if (m_Renderers == null || m_Renderers.Length == 0)
                m_Renderers = GetComponentsInChildren<Renderer>(true);

            m_PropertyBlock = new MaterialPropertyBlock();
            m_OriginalScale = transform.localScale;
            m_Initialized = true;
        }
    }
}
