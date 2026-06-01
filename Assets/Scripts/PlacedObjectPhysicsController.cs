using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Switches placed objects between held and released physics states.
    /// Released objects simulate with gravity. Held objects keep collisions active but suspend gravity.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacedObjectPhysicsController : MonoBehaviour
    {
        static readonly Collider[] s_OverlapResults = new Collider[32];
        const float kReleaseSeparationPadding = 0.005f;
        const int kMaxReleaseResolveIterations = 6;

        [SerializeField]
        [Tooltip("Grab interactable that drives the held/released state.")]
        XRGrabInteractable m_GrabInteractable;

        [SerializeField]
        [Tooltip("Rigidbody driven by this controller.")]
        Rigidbody m_Rigidbody;

        readonly List<Collider> m_OwnColliders = new();

        void Reset()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            CacheOwnColliders();
        }

        void Awake()
        {
            if (m_GrabInteractable == null)
                m_GrabInteractable = GetComponent<XRGrabInteractable>();

            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            CacheOwnColliders();
            ApplyReleasedPhysicsState();
        }

        public void SetReleasedPhysicsEnabled(bool isEnabled)
        {
            if (m_GrabInteractable == null)
                m_GrabInteractable = GetComponent<XRGrabInteractable>();

            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            CacheOwnColliders();
            if (isEnabled)
            {
                ApplyReleasedPhysicsState();
                enabled = true;
                return;
            }

            ApplyDisabledPhysicsState();
            enabled = false;
        }

        void OnEnable()
        {
            if (m_GrabInteractable == null)
                return;

            m_GrabInteractable.selectEntered.AddListener(OnSelectEntered);
            m_GrabInteractable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            if (m_GrabInteractable == null)
                return;

            m_GrabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        void OnSelectEntered(SelectEnterEventArgs _)
        {
            BeginHeldPhysicsState();
        }

        void OnSelectExited(SelectExitEventArgs _)
        {
            ResolveOverlaps();
            ApplyReleasedPhysicsState();
        }

        void BeginHeldPhysicsState()
        {
            if (m_Rigidbody == null || m_GrabInteractable == null)
                return;

            // Hold objects fully kinematic while grabbed so they follow the interactor without physics noise.
            m_GrabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.linearDamping = 0f;
            m_Rigidbody.angularDamping = 0f;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void ApplyReleasedPhysicsState()
        {
            if (m_Rigidbody == null || m_GrabInteractable == null)
                return;

            m_GrabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = true;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.linearDamping = 0f;
            m_Rigidbody.angularDamping = 0.05f;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void ApplyDisabledPhysicsState()
        {
            if (m_Rigidbody == null || m_GrabInteractable == null)
                return;

            m_GrabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.linearDamping = 0f;
            m_Rigidbody.angularDamping = 0f;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void ResolveOverlaps()
        {
            if (m_OwnColliders.Count == 0)
                return;

            Physics.SyncTransforms();

            for (var iteration = 0; iteration < kMaxReleaseResolveIterations; ++iteration)
            {
                var ownBounds = CalculateOwnBounds();
                var overlapCount = Physics.OverlapBoxNonAlloc(
                    ownBounds.center,
                    ownBounds.extents + Vector3.one * 0.01f,
                    s_OverlapResults,
                    Quaternion.identity,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                var totalSeparation = Vector3.zero;
                var foundPenetration = false;

                for (var overlapIndex = 0; overlapIndex < overlapCount; ++overlapIndex)
                {
                    var otherCollider = s_OverlapResults[overlapIndex];
                    if (otherCollider == null || m_OwnColliders.Contains(otherCollider))
                        continue;

                    for (var ownColliderIndex = 0; ownColliderIndex < m_OwnColliders.Count; ++ownColliderIndex)
                    {
                        var ownCollider = m_OwnColliders[ownColliderIndex];
                        if (ownCollider == null || !ownCollider.enabled)
                            continue;

                        if (!Physics.ComputePenetration(
                                ownCollider, ownCollider.transform.position, ownCollider.transform.rotation,
                                otherCollider, otherCollider.transform.position, otherCollider.transform.rotation,
                                out var separationDirection, out var separationDistance))
                        {
                            continue;
                        }

                        totalSeparation += separationDirection * (separationDistance + kReleaseSeparationPadding);
                        foundPenetration = true;
                    }
                }

                if (!foundPenetration || totalSeparation.sqrMagnitude < 0.000001f)
                    break;

                transform.position += totalSeparation;
                Physics.SyncTransforms();
            }
        }

        void CacheOwnColliders()
        {
            m_OwnColliders.Clear();
            GetComponentsInChildren(true, m_OwnColliders);
        }

        Bounds CalculateOwnBounds()
        {
            var hasBounds = false;
            var bounds = new Bounds(transform.position, Vector3.one * 0.05f);

            for (var i = 0; i < m_OwnColliders.Count; ++i)
            {
                var collider = m_OwnColliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }
    }
}
