using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Places a prefab on an AR plane using the current interactor ray projected
    /// through ARRaycastManager, with optional placement on previously placed objects.
    /// </summary>
    [DisallowMultipleComponent]
    public class XRRayARPlanePlacer : MonoBehaviour
    {
        static readonly List<ARRaycastHit> s_ARRaycastHits = new();

        readonly struct PlacementHitInfo
        {
            public PlacementHitInfo(Vector3 point, Vector3 normal, float distance, string sourceLabel, ARPlane plane = null)
            {
                this.point = point;
                this.normal = normal;
                this.distance = distance;
                this.sourceLabel = sourceLabel;
                this.plane = plane;
            }

            public Vector3 point { get; }
            public Vector3 normal { get; }
            public float distance { get; }
            public string sourceLabel { get; }
            public ARPlane plane { get; }
        }

        public event Action<XRRayARPlanePlacer, GameObject> PlacementCompleted;

        [SerializeField]
        [Tooltip("Input interactor used for select state, UI blocking, and generic ray fallback.")]
        XRBaseInputInteractor m_InputInteractor;

        [SerializeField]
        [Tooltip("Used when the input interactor does not provide direct AR hits.")]
        ARRaycastManager m_RaycastManager;

        [SerializeField]
        [Tooltip("Used to resolve ARPlane instances from AR raycast hits.")]
        ARPlaneManager m_PlaneManager;

        [SerializeField]
        [Tooltip("Prefab to place on a detected plane.")]
        GameObject m_PlacePrefab;

        [SerializeField]
        [Tooltip("Optional anchor manager used to stabilize placed content on device.")]
        ARAnchorManager m_AnchorManager;

        [SerializeField]
        [Tooltip("Placed objects can only begin a grab when the interactor is within this distance.")]
        float m_MaxGrabDistance = 0.18f;

        [SerializeField]
        [Tooltip("Extra gap between the hand/controller and the nearest face of the grabbed object.")]
        float m_GrabAttachForwardOffset = 0.05f;

        [SerializeField]
        [Tooltip("Minimum time between successful placements for this placer.")]
        float m_MinSecondsBetweenPlacements = 0.2f;

        [SerializeField]
        bool m_EnableDebugLogs = true;

        [SerializeField]
        [Tooltip("If false, this placer ignores placement input until it is explicitly armed.")]
        bool m_IsPlacementArmed = true;

        bool m_AttemptPlacement;
        bool m_EverHadSelection;
        bool m_IsPlacing;
        bool m_WaitForFreshPlacementGesture;
        bool m_LastLoggedSelectActive;
        bool m_LastLoggedActivateActive;
        bool m_LastLoggedHasSelection;
        float m_LastPlacementTime = float.NegativeInfinity;
        readonly List<IXRSelectFilter> m_SelectFilters = new();

        public bool isPlacementArmed => m_IsPlacementArmed;

        void Awake()
        {
            if (m_InputInteractor == null)
                m_InputInteractor = GetComponent<XRBaseInputInteractor>();

            if (m_RaycastManager == null)
                m_RaycastManager = FindFirstObjectByType<ARRaycastManager>();

            if (m_PlaneManager == null)
                m_PlaneManager = FindFirstObjectByType<ARPlaneManager>();

            if (m_AnchorManager == null)
                m_AnchorManager = FindFirstObjectByType<ARAnchorManager>();

            LogDebug(
                $"Awake complete. InputInteractor={m_InputInteractor != null}, " +
                $"RaycastManager={m_RaycastManager != null}, " +
                $"PlaneManager={m_PlaneManager != null}, " +
                $"AnchorManager={m_AnchorManager != null}, " +
                $"AllowSelectFallback=True, " +
                $"PlacedObjectPhysics=False");
        }

        void OnEnable()
        {
            LogDebug("Enabled.");
        }

        void Update()
        {
            if (m_InputInteractor == null || m_IsPlacing)
                return;

            if (!m_IsPlacementArmed)
                return;

            if (m_AttemptPlacement)
            {
                m_AttemptPlacement = false;

                if (m_InputInteractor.hasSelection)
                {
                    LogDebug("Placement cancelled because the input interactor has a selection.");
                    return;
                }

                if (IsPointerOverUI())
                {
                    LogDebug("Placement cancelled because pointer is over UI.");
                    return;
                }

                if (!TryGetPlacementHit(out var placementHit))
                    return;

                var pose = BuildPlacementPose(placementHit);
                LogDebug($"Placement hit accepted. Source={placementHit.sourceLabel}, position={pose.position}, normal={placementHit.normal}");
                PlaceObjectAsync(placementHit, pose);
                return;
            }

            var selectState = m_InputInteractor.logicalSelectState;
            var activateState = m_InputInteractor.logicalActivateState;
            var isSelectStartedThisFrame = selectState.wasPerformedThisFrame;
            var isSelectHeld = selectState.active;
            var isSelectReleasedThisFrame = selectState.wasCompletedThisFrame;
            var isActivateStartedThisFrame = activateState.wasPerformedThisFrame;
            var isActivateHeld = activateState.active;
            var isActivateReleasedThisFrame = activateState.wasCompletedThisFrame;
            var isCurrentlySelectingObject = m_InputInteractor.hasSelection;

            LogInteractorInputState(
                isSelectStartedThisFrame,
                isSelectHeld,
                isSelectReleasedThisFrame,
                isActivateStartedThisFrame,
                isActivateHeld,
                isActivateReleasedThisFrame,
                isCurrentlySelectingObject);

            if (m_WaitForFreshPlacementGesture)
            {
                var gestureIsStillSettling =
                    isSelectStartedThisFrame ||
                    isSelectHeld ||
                    isSelectReleasedThisFrame ||
                    isActivateStartedThisFrame ||
                    isActivateHeld ||
                    isActivateReleasedThisFrame ||
                    isCurrentlySelectingObject;

                if (gestureIsStillSettling)
                    return;

                m_WaitForFreshPlacementGesture = false;
                LogDebug("Placement input re-armed after waiting for a fresh gesture.");
                return;
            }

            // Track whether the current select gesture was used for object interaction.
            // This prevents a grab/release gesture from turning into a placement attempt on release.
            if (isSelectStartedThisFrame)
            {
                m_EverHadSelection = isCurrentlySelectingObject;
            }
            else if (isSelectHeld || isCurrentlySelectingObject)
            {
                m_EverHadSelection |= isCurrentlySelectingObject;
            }

            if (isActivateReleasedThisFrame)
            {
                // Activate is the dedicated placement channel. Selection/grabbing stays on
                // the interactor's Select channel, so only place while nothing is held.
                m_AttemptPlacement = !isCurrentlySelectingObject && CanQueuePlacementAttempt();
                m_EverHadSelection = false;
                LogDebug($"ActivateAttempt completed. attemptPlacement={m_AttemptPlacement}");
            }
            else if (isSelectReleasedThisFrame)
            {
                var gestureWasUsedForObjectInteraction = isCurrentlySelectingObject || m_EverHadSelection;
                m_AttemptPlacement = !gestureWasUsedForObjectInteraction && CanQueuePlacementAttempt();
                m_EverHadSelection = false;
                LogDebug($"ActivateAttempt select fallback completed. attemptPlacement={m_AttemptPlacement}");
            }
        }

        void LogInteractorInputState(
            bool isSelectStartedThisFrame,
            bool isSelectHeld,
            bool isSelectReleasedThisFrame,
            bool isActivateStartedThisFrame,
            bool isActivateHeld,
            bool isActivateReleasedThisFrame,
            bool isCurrentlySelectingObject)
        {
            if (!m_EnableDebugLogs)
                return;

            if (isSelectStartedThisFrame)
                LogDebug("Select started this frame.");

            if (isSelectReleasedThisFrame)
                LogDebug("Select released this frame.");

            if (isActivateStartedThisFrame)
                LogDebug("Activate started this frame.");

            if (isActivateReleasedThisFrame)
                LogDebug("Activate released this frame.");

            if (isSelectHeld != m_LastLoggedSelectActive)
            {
                m_LastLoggedSelectActive = isSelectHeld;
                LogDebug($"Select active changed: {isSelectHeld}");
            }

            if (isActivateHeld != m_LastLoggedActivateActive)
            {
                m_LastLoggedActivateActive = isActivateHeld;
                LogDebug($"Activate active changed: {isActivateHeld}");
            }

            if (isCurrentlySelectingObject != m_LastLoggedHasSelection)
            {
                m_LastLoggedHasSelection = isCurrentlySelectingObject;
                LogDebug($"Has selection changed: {isCurrentlySelectingObject}");
            }
        }

        bool IsPointerOverUI()
        {
            if (m_InputInteractor is XRRayInteractor xrRayInteractor &&
                xrRayInteractor.TryGetCurrentUIRaycastResult(out var xrRayUIResult))
            {
                return xrRayUIResult.isValid;
            }

            if (m_InputInteractor is NearFarInteractor nearFarInteractor &&
                nearFarInteractor.TryGetCurrentUIRaycastResult(out var nearFarUIResult))
            {
                return nearFarUIResult.isValid;
            }

            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1);
        }

        bool TryGetPlacementHit(out PlacementHitInfo hitInfo)
        {
            hitInfo = default;

            if (m_InputInteractor is not IXRRayProvider rayProvider)
            {
                LogDebug("Input interactor does not implement IXRRayProvider.");
                return false;
            }

            if (m_RaycastManager == null)
            {
                LogDebug("ARRaycastManager is missing for generic ray fallback.");
                return false;
            }

            var rayOrigin = rayProvider.GetOrCreateRayOrigin();
            if (rayOrigin == null)
            {
                LogDebug("Generic ray fallback failed because the ray origin is null.");
                return false;
            }

            var direction = rayProvider.rayEndPoint - rayOrigin.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                LogDebug("Generic ray fallback failed because the ray direction is too small.");
                return false;
            }

            var rayLength = direction.magnitude;
            var ray = new Ray(rayOrigin.position, direction / rayLength);

            var hasPlacedObjectHit = TryGetPlacedObjectHit(ray, rayLength, out var placedObjectHitInfo);
            var hasPlaneHit = TryGetPlaneHit(ray, out var planeHitInfo);

            if (!hasPlacedObjectHit && !hasPlaneHit)
            {
                LogDebug($"ARRaycastManager.Raycast returned no hits. origin={ray.origin}, direction={ray.direction}");
                return false;
            }

            if (hasPlacedObjectHit && (!hasPlaneHit || placedObjectHitInfo.distance <= planeHitInfo.distance))
            {
                hitInfo = placedObjectHitInfo;
                LogDebug("Using placed object surface hit.");
                return true;
            }

            hitInfo = planeHitInfo;
            LogDebug("Using generic interactor ray fallback hit.");
            return true;
        }

        bool TryGetPlaneHit(Ray ray, out PlacementHitInfo hitInfo)
        {
            hitInfo = default;

            if (!m_RaycastManager.Raycast(ray, s_ARRaycastHits, TrackableType.PlaneWithinPolygon))
                return false;

            var raycastHit = s_ARRaycastHits[0];
            var plane = m_PlaneManager != null ? m_PlaneManager.GetPlane(raycastHit.trackableId) : null;
            if (plane == null)
            {
                LogDebug($"ARRaycast hit {raycastHit.trackableId}, but ARPlaneManager could not resolve the plane.");
                return false;
            }

            hitInfo = new PlacementHitInfo(
                raycastHit.pose.position,
                raycastHit.pose.up,
                raycastHit.distance,
                $"ARPlane:{plane.trackableId}",
                plane);
            return true;
        }

        static bool TryGetPlacedObjectHit(Ray ray, float maxDistance, out PlacementHitInfo hitInfo)
        {
            hitInfo = default;

            if (!Physics.Raycast(ray, out var raycastHit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            var placedSurface = raycastHit.collider.GetComponentInParent<PlacedObjectSurface>();
            if (placedSurface == null)
                return false;

            hitInfo = new PlacementHitInfo(
                raycastHit.point,
                raycastHit.normal,
                raycastHit.distance,
                $"PlacedObject:{placedSurface.name}");
            return true;
        }

        Pose BuildPlacementPose(PlacementHitInfo hitInfo)
        {
            var position = hitInfo.point;
            var desiredForward = Vector3.forward;

            if (Camera.main != null)
                desiredForward = Camera.main.transform.forward;

            // Keep placed content upright in world space regardless of plane orientation.
            var flattenedForward = Vector3.ProjectOnPlane(desiredForward, Vector3.up);
            if (flattenedForward.sqrMagnitude < 0.0001f)
                flattenedForward = Vector3.forward;

            return new Pose(position, Quaternion.LookRotation(flattenedForward.normalized, Vector3.up));
        }

        async void PlaceObjectAsync(PlacementHitInfo hitInfo, Pose pose)
        {
            m_IsPlacing = true;

            try
            {
                GameObject placementRoot = null;
                GameObject placedObject = null;

                if (hitInfo.plane != null &&
                    m_AnchorManager != null &&
                    m_AnchorManager.enabled &&
                    m_AnchorManager.subsystem != null)
                {
                    if (m_AnchorManager.descriptor != null && m_AnchorManager.descriptor.supportsTrackableAttachments)
                    {
                        var anchor = m_AnchorManager.AttachAnchor(hitInfo.plane, pose);
                        if (anchor != null)
                        {
                            placementRoot = anchor.gameObject;
                            placedObject = SpawnContent(anchor.transform, Pose.identity);
                        }
                    }
                    else
                    {
                        var anchorResult = await m_AnchorManager.TryAddAnchorAsync(pose);
                        if (anchorResult.status.IsSuccess())
                        {
                            placementRoot = anchorResult.value.gameObject;
                            placedObject = SpawnContent(anchorResult.value.transform, Pose.identity);
                        }
                    }
                }

                if (placementRoot == null)
                {
                    placedObject = SpawnContent(null, pose);
                    placementRoot = placedObject;
                }

                if (placementRoot.GetComponent<PlacedObjectRootMarker>() == null)
                    placementRoot.AddComponent<PlacedObjectRootMarker>();

                m_LastPlacementTime = Time.unscaledTime;
                LogDebug($"Placed object at {pose.position}.");
                if (placedObject != null)
                    PlacementCompleted?.Invoke(this, placedObject);
            }
            finally
            {
                m_IsPlacing = false;
            }
        }

        GameObject SpawnContent(Transform parent, Pose pose)
        {
            if (m_PlacePrefab != null)
            {
                var instance = Instantiate(m_PlacePrefab, pose.position, pose.rotation, parent);
                if (parent != null)
                    instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                ConfigureGrabInteraction(instance);
                return instance;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "AR Plane Placement Marker";
            fallback.transform.SetPositionAndRotation(pose.position, pose.rotation);
            fallback.transform.localScale = Vector3.one * 0.15f;
            ConfigureGrabInteraction(fallback);

            if (parent != null)
            {
                fallback.transform.SetParent(parent, false);
                fallback.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            return fallback;
        }

        public void ArmPlacement(GameObject placePrefab)
        {
            m_PlacePrefab = placePrefab;
            m_AttemptPlacement = false;
            m_IsPlacementArmed = placePrefab != null;
            m_WaitForFreshPlacementGesture = placePrefab != null;
        }

        public void DisarmPlacement()
        {
            m_AttemptPlacement = false;
            m_PlacePrefab = null;
            m_IsPlacementArmed = false;
            m_WaitForFreshPlacementGesture = false;
        }

        void ConfigureGrabInteraction(GameObject target)
        {
            if (target == null)
                return;

            EnsureCollider(target);

            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = target.AddComponent<Rigidbody>();

            ConfigureRigidbodyForPlacement(rigidbody);

            var grabInteractable = target.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = target.AddComponent<XRGrabInteractable>();

            grabInteractable.throwOnDetach = false;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
            // Rest in kinematic grab mode so pickup starts from a stable state.
            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            grabInteractable.useDynamicAttach = false;
            grabInteractable.attachEaseInTime = 0f;
            grabInteractable.attachTransform = GetOrCreateGrabAttachTransform(target.transform);

            var closeRangeFilter = target.GetComponent<CloseRangeGrabSelectFilter>();
            if (closeRangeFilter == null)
                closeRangeFilter = target.AddComponent<CloseRangeGrabSelectFilter>();

            closeRangeFilter.maxGrabDistance = m_MaxGrabDistance;

            if (target.GetComponent<PlacedObjectInteractionFeedback>() == null)
                target.AddComponent<PlacedObjectInteractionFeedback>();

            if (target.GetComponent<PlacedObjectSurface>() == null)
                target.AddComponent<PlacedObjectSurface>();

            ConfigurePlacedObjectPhysicsController(target);

            m_SelectFilters.Clear();
            grabInteractable.selectFilters.GetAll(m_SelectFilters);
            if (!m_SelectFilters.Contains(closeRangeFilter))
                grabInteractable.selectFilters.Add(closeRangeFilter);
        }

        void ConfigureRigidbodyForPlacement(Rigidbody rigidbody)
        {
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.linearDamping = 0f;
            rigidbody.angularDamping = 0f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void ConfigurePlacedObjectPhysicsController(GameObject target)
        {
            var physicsController = target.GetComponent<PlacedObjectPhysicsController>();
            if (physicsController != null)
                physicsController.enabled = false;

            LogDebug("Placed object physics disabled. Object will remain kinematic.");
        }

        static void EnsureCollider(GameObject target)
        {
            if (target.GetComponentInChildren<Collider>() != null)
                return;

            target.AddComponent<BoxCollider>();
        }

        Transform GetOrCreateGrabAttachTransform(Transform target)
        {
            var existing = target.Find("Grab Attach");
            if (existing == null)
            {
                var attachObject = new GameObject("Grab Attach");
                existing = attachObject.transform;
                existing.SetParent(target, false);
            }

            var extraGap = Mathf.Max(0f, m_GrabAttachForwardOffset);
            // The placed prefab root is now the authoritative holder/pivot object.
            // Use that local origin directly so grab alignment does not inherit offsets
            // from child mesh bounds or collider bounds.
            existing.localPosition = new Vector3(0f, 0f, -extraGap);
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            return existing;
        }

        void LogDebug(string message)
        {
            if (!m_EnableDebugLogs)
                return;

            Debug.Log($"[XRRayARPlanePlacer] {message}", this);
        }

        bool CanQueuePlacementAttempt()
        {
            if (m_AttemptPlacement || m_IsPlacing)
                return false;

            return Time.unscaledTime >= m_LastPlacementTime + Mathf.Max(0f, m_MinSecondsBetweenPlacements);
        }
    }

}
