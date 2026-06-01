using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Prevents a grab interactable from being selected unless the interactor attach point
    /// is physically close to one of its colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public class CloseRangeGrabSelectFilter : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField]
        [Tooltip("Optional explicit grab interactable reference. Defaults to the component on this GameObject.")]
        XRGrabInteractable m_GrabInteractable;

        [SerializeField]
        [Tooltip("Interactor attach point must be within this distance of the object to begin a grab.")]
        float m_MaxGrabDistance = 0.18f;

        public bool canProcess => isActiveAndEnabled;

        public float maxGrabDistance
        {
            get => m_MaxGrabDistance;
            set => m_MaxGrabDistance = Mathf.Max(0.01f, value);
        }

        void Reset()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
        }

        void Awake()
        {
            if (m_GrabInteractable == null)
                m_GrabInteractable = GetComponent<XRGrabInteractable>();
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (m_GrabInteractable == null)
                return true;

            if (!ReferenceEquals(interactable, m_GrabInteractable))
                return true;

            if (interactor.IsSelecting(interactable))
                return true;

            var attachTransform = interactor.GetAttachTransform(interactable);
            if (attachTransform == null)
                return false;

            var interactorPosition = attachTransform.position;
            var maxDistanceSqr = m_MaxGrabDistance * m_MaxGrabDistance;

            var colliders = m_GrabInteractable.colliders;
            for (var i = 0; i < colliders.Count; ++i)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                var closestPoint = collider.ClosestPoint(interactorPosition);
                if ((closestPoint - interactorPosition).sqrMagnitude <= maxDistanceSqr)
                    return true;
            }

            return (m_GrabInteractable.transform.position - interactorPosition).sqrMagnitude <= maxDistanceSqr;
        }
    }
}
