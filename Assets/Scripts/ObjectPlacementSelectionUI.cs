using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Owns the object-selection UI and arms the scene placers for placement.
    /// After a successful placement or an explicit cancel, the UI returns to selection mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectPlacementSelectionUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Placement option buttons. Button [i] will place Prefab [i].")]
        List<Button> m_OptionButtons = new();

        [SerializeField]
        [Tooltip("Placement option prefabs. Prefab [i] is placed by Button [i].")]
        List<GameObject> m_OptionPrefabs = new();

        [SerializeField]
        [Tooltip("Cancel/close button shown while the player is armed to place.")]
        Button m_CloseButton;

        [SerializeField]
        [Tooltip("Optional reset button. It is visible only while the option buttons are visible. Wire its OnClick to ResetPlacedObjects.")]
        Button m_ResetButton;

        [SerializeField]
        [Tooltip("Optional manually assigned physics button. It is visible only while the option buttons are visible.")]
        Button m_PhysicsButton;

        [SerializeField]
        [Tooltip("Placers controlled by this UI. If empty, all scene placers are used.")]
        XRRayARPlanePlacer[] m_Placers;

        [SerializeField]
        [Tooltip("Usually the main camera transform. The UI follows this target.")]
        Transform m_FollowTarget;

        [SerializeField]
        [Tooltip("Local offset from the follow target. Negative Y keeps the panel near the bottom of view.")]
        Vector3 m_ViewOffset = new Vector3(0f, -0.18f, 0.7f);

        [SerializeField]
        [Tooltip("Higher values make the panel catch up faster.")]
        float m_PositionFollowSpeed = 8f;

        [SerializeField]
        [Tooltip("Higher values make the panel rotate toward the player faster.")]
        float m_RotationFollowSpeed = 10f;

        [SerializeField]
        [Tooltip("Extra local rotation applied after the panel is aimed at the player. Use this to correct mirrored canvases.")]
        Vector3 m_LookRotationOffsetEuler = new Vector3(0f, 180f, 0f);

        [SerializeField]
        bool m_EnableDebugLogs = true;

        bool m_IsAwaitingPlacement;
        bool m_HasInitializedFollow;
        bool m_ArePlacedObjectGrabsEnabled = true;
        readonly List<GameObject> m_RuntimePlacementRoots = new();
        readonly List<UnityAction> m_OptionButtonHandlers = new();

        void Awake()
        {
            EnsureOptionListsExist();
            RefreshPlacers();
            WarnAboutOptionConfiguration();

            if (m_FollowTarget == null && Camera.main != null)
                m_FollowTarget = Camera.main.transform;
        }

        void OnEnable()
        {
            SubscribeButtonEvents();
        }

        void Start()
        {
            RebuildPlacerSubscriptions();
            ResetToSelectionState();
        }

        void OnDisable()
        {
            UnsubscribeButtonEvents();
            UnsubscribePlacerEvents();
        }

        void LateUpdate()
        {
            UpdateFollowPose();
        }

        void BeginPlacementForIndex(int optionIndex)
        {
            if (!TryGetOptionPrefab(optionIndex, out var placePrefab))
            {
                Debug.LogWarning(
                    $"[{nameof(ObjectPlacementSelectionUI)}] Option {optionIndex + 1} has no prefab assigned.",
                    this);
                return;
            }

            BeginPlacement(placePrefab, optionIndex);
        }

        void CancelPlacement()
        {
            LogDebug("Placement cancelled by UI.");
            ResetToSelectionState();
        }

        void HandlePlacementCompleted(XRRayARPlanePlacer placer, GameObject placedObject)
        {
            RegisterPlacementRoot(placedObject);
            LogDebug($"Placement completed by {placer.name} with {placedObject.name}.");
            ResetToSelectionState();
        }

        public void ResetPlacedObjects()
        {
            ResetToSelectionState();

            CleanupDestroyedPlacementRoots();
            var placementRootsToDestroy = new List<GameObject>(m_RuntimePlacementRoots);

            if (placementRootsToDestroy.Count == 0)
            {
                var placementRootMarkers = FindObjectsByType<PlacedObjectRootMarker>(FindObjectsSortMode.None);
                for (var i = 0; i < placementRootMarkers.Length; ++i)
                {
                    var placementRootMarker = placementRootMarkers[i];
                    if (placementRootMarker == null)
                        continue;

                    var placementRoot = placementRootMarker.gameObject;
                    if (placementRoot != null && !placementRootsToDestroy.Contains(placementRoot))
                        placementRootsToDestroy.Add(placementRoot);
                }
            }

            for (var i = 0; i < placementRootsToDestroy.Count; ++i)
            {
                var placementRoot = placementRootsToDestroy[i];
                if (placementRoot == null)
                    continue;

                Destroy(placementRoot);
            }

            m_RuntimePlacementRoots.Clear();
            LogDebug($"Reset destroyed {placementRootsToDestroy.Count} placement root(s).");
        }

        void BeginPlacement(GameObject placePrefab, int optionIndex)
        {
            if (placePrefab == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ObjectPlacementSelectionUI)}] Option {optionIndex + 1} has no prefab assigned.",
                    this);
                return;
            }

            RebuildPlacerSubscriptions();
            if (m_Placers == null || m_Placers.Length == 0)
            {
                Debug.LogWarning($"[{nameof(ObjectPlacementSelectionUI)}] No placers were found to arm.", this);
                return;
            }

            for (var i = 0; i < m_Placers.Length; ++i)
            {
                var placer = m_Placers[i];
                if (placer == null)
                    continue;

                placer.ArmPlacement(placePrefab);
            }

            m_IsAwaitingPlacement = true;
            ApplyUiState();
            LogDebug($"Option {optionIndex + 1} selected. Armed {CountValidPlacers()} placer(s).");
        }

        void ResetToSelectionState()
        {
            RebuildPlacerSubscriptions();

            for (var i = 0; i < m_Placers.Length; ++i)
            {
                var placer = m_Placers[i];
                if (placer == null)
                    continue;

                placer.DisarmPlacement();
            }

            m_IsAwaitingPlacement = false;
            ApplyUiState();
        }

        void ApplyUiState()
        {
            for (var i = 0; i < m_OptionButtons.Count; ++i)
                SetButtonVisible(m_OptionButtons[i], !m_IsAwaitingPlacement);

            SetButtonVisible(m_CloseButton, m_IsAwaitingPlacement);
            SetButtonVisible(m_ResetButton, !m_IsAwaitingPlacement);
            SetButtonVisible(m_PhysicsButton, !m_IsAwaitingPlacement);
            SetPlacedObjectGrabInteractionsEnabled(!m_IsAwaitingPlacement);
        }

        void UpdateFollowPose()
        {
            if (m_FollowTarget == null)
            {
                if (Camera.main == null)
                    return;

                m_FollowTarget = Camera.main.transform;
            }

            var desiredPosition = m_FollowTarget.TransformPoint(m_ViewOffset);
            var toTarget = m_FollowTarget.position - desiredPosition;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = -m_FollowTarget.forward;

            var desiredRotation =
                Quaternion.LookRotation(toTarget.normalized, Vector3.up) *
                Quaternion.Euler(m_LookRotationOffsetEuler);

            if (!m_HasInitializedFollow)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                m_HasInitializedFollow = true;
                return;
            }

            var positionLerpFactor = 1f - Mathf.Exp(-m_PositionFollowSpeed * Time.unscaledDeltaTime);
            var rotationLerpFactor = 1f - Mathf.Exp(-m_RotationFollowSpeed * Time.unscaledDeltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerpFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationLerpFactor);
        }

        void SubscribeButtonEvents()
        {
            UnsubscribeButtonEvents();

            for (var i = 0; i < m_OptionButtons.Count; ++i)
            {
                var optionButton = m_OptionButtons[i];
                if (optionButton == null)
                {
                    m_OptionButtonHandlers.Add(null);
                    continue;
                }

                var capturedIndex = i;
                UnityAction clickHandler = () => BeginPlacementForIndex(capturedIndex);
                m_OptionButtonHandlers.Add(clickHandler);
                optionButton.onClick.AddListener(clickHandler);
            }
            m_CloseButton?.onClick.AddListener(CancelPlacement);
            m_PhysicsButton?.onClick.AddListener(EnablePhysicsOnPlacedObjects);
        }

        void UnsubscribeButtonEvents()
        {
            var handlerCount = Mathf.Min(m_OptionButtons.Count, m_OptionButtonHandlers.Count);
            for (var i = 0; i < handlerCount; ++i)
            {
                var optionButton = m_OptionButtons[i];
                var clickHandler = m_OptionButtonHandlers[i];
                if (optionButton == null || clickHandler == null)
                    continue;

                optionButton.onClick.RemoveListener(clickHandler);
            }

            m_OptionButtonHandlers.Clear();
            m_CloseButton?.onClick.RemoveListener(CancelPlacement);
            m_PhysicsButton?.onClick.RemoveListener(EnablePhysicsOnPlacedObjects);
        }

        void SubscribePlacerEvents()
        {
            UnsubscribePlacerEvents();

            if (m_Placers == null)
            {
                LogDebug("Subscribed to 0 placer event(s).");
                return;
            }

            for (var i = 0; i < m_Placers.Length; ++i)
            {
                var placer = m_Placers[i];
                if (placer == null)
                    continue;

                placer.PlacementCompleted += HandlePlacementCompleted;
            }

            LogDebug($"Subscribed to {CountValidPlacers()} placer event(s).");
        }

        void UnsubscribePlacerEvents()
        {
            if (m_Placers == null)
                return;

            for (var i = 0; i < m_Placers.Length; ++i)
            {
                var placer = m_Placers[i];
                if (placer == null)
                    continue;

                placer.PlacementCompleted -= HandlePlacementCompleted;
            }
        }

        void RefreshPlacers()
        {
            m_Placers = FindObjectsByType<XRRayARPlanePlacer>(FindObjectsSortMode.None);
        }

        void RebuildPlacerSubscriptions()
        {
            RefreshPlacers();
            SubscribePlacerEvents();
        }

        static void SetButtonVisible(Button button, bool isVisible)
        {
            if (button == null)
                return;

            button.gameObject.SetActive(isVisible);
        }

        int CountValidPlacers()
        {
            if (m_Placers == null)
                return 0;

            var count = 0;
            for (var i = 0; i < m_Placers.Length; ++i)
            {
                if (m_Placers[i] != null)
                    count++;
            }

            return count;
        }

        void EnsureOptionListsExist()
        {
            if (m_OptionButtons == null)
                m_OptionButtons = new List<Button>();

            if (m_OptionPrefabs == null)
                m_OptionPrefabs = new List<GameObject>();
        }

        bool TryGetOptionPrefab(int optionIndex, out GameObject placePrefab)
        {
            placePrefab = null;
            if (optionIndex < 0 || optionIndex >= m_OptionPrefabs.Count)
                return false;

            placePrefab = m_OptionPrefabs[optionIndex];
            return placePrefab != null;
        }

        void WarnAboutOptionConfiguration()
        {
            if (m_OptionButtons.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ObjectPlacementSelectionUI)}] No option buttons are assigned.", this);
                return;
            }

            if (m_OptionButtons.Count != m_OptionPrefabs.Count)
            {
                Debug.LogWarning(
                    $"[{nameof(ObjectPlacementSelectionUI)}] Option button count ({m_OptionButtons.Count}) " +
                    $"does not match prefab count ({m_OptionPrefabs.Count}). Unmatched buttons will not place anything.",
                    this);
            }
        }

        void SetPlacedObjectGrabInteractionsEnabled(bool isEnabled)
        {
            if (m_ArePlacedObjectGrabsEnabled == isEnabled)
                return;

            m_ArePlacedObjectGrabsEnabled = isEnabled;
            var changedCount = 0;
            var placedSurfaces = FindObjectsByType<PlacedObjectSurface>(FindObjectsSortMode.None);
            for (var i = 0; i < placedSurfaces.Length; ++i)
            {
                var placedSurface = placedSurfaces[i];
                if (placedSurface == null)
                    continue;

                var grabInteractable = placedSurface.GetComponentInParent<XRGrabInteractable>();
                if (grabInteractable == null || grabInteractable.enabled == isEnabled)
                    continue;

                grabInteractable.enabled = isEnabled;
                changedCount++;
            }

            LogDebug($"{(isEnabled ? "Enabled" : "Disabled")} grab interaction on {changedCount} placed object(s).");
        }

        public void EnablePhysicsOnPlacedObjects()
        {
            LogDebug("Physics button clicked.");

            var changedCount = 0;
            var placementRoots = CollectPlacementRoots();
            var shouldEnablePhysics = ShouldEnablePhysics(placementRoots);

            for (var i = 0; i < placementRoots.Count; ++i)
            {
                var placementRoot = placementRoots[i];
                if (placementRoot == null)
                    continue;

                var target = GetPhysicsTarget(placementRoot);
                if (target == null)
                    continue;

                var rigidbody = target.GetComponent<Rigidbody>();
                if (rigidbody == null)
                    rigidbody = target.AddComponent<Rigidbody>();

                var physicsController = target.GetComponent<PlacedObjectPhysicsController>();
                if (physicsController == null)
                    physicsController = target.AddComponent<PlacedObjectPhysicsController>();

                physicsController.SetReleasedPhysicsEnabled(shouldEnablePhysics);
                changedCount++;
            }

            LogDebug($"{(shouldEnablePhysics ? "Enabled" : "Disabled")} physics on {changedCount} placed object(s).");
        }

        bool ShouldEnablePhysics(List<GameObject> placementRoots)
        {
            for (var i = 0; i < placementRoots.Count; ++i)
            {
                var target = GetPhysicsTarget(placementRoots[i]);
                if (target == null)
                    continue;

                var rigidbody = target.GetComponent<Rigidbody>();
                if (rigidbody != null && !rigidbody.isKinematic && rigidbody.useGravity)
                    return false;
            }

            return true;
        }

        List<GameObject> CollectPlacementRoots()
        {
            CleanupDestroyedPlacementRoots();
            var placementRoots = new List<GameObject>(m_RuntimePlacementRoots);
            var placementRootMarkers = FindObjectsByType<PlacedObjectRootMarker>(FindObjectsSortMode.None);
            for (var i = 0; i < placementRootMarkers.Length; ++i)
            {
                var placementRootMarker = placementRootMarkers[i];
                if (placementRootMarker == null)
                    continue;

                var placementRoot = placementRootMarker.gameObject;
                if (placementRoot != null && !placementRoots.Contains(placementRoot))
                    placementRoots.Add(placementRoot);
            }

            return placementRoots;
        }

        static GameObject GetPhysicsTarget(GameObject placementRoot)
        {
            if (placementRoot == null)
                return null;

            var grabInteractable = placementRoot.GetComponentInChildren<XRGrabInteractable>(true);
            if (grabInteractable != null)
                return grabInteractable.gameObject;

            var placedSurface = placementRoot.GetComponentInChildren<PlacedObjectSurface>(true);
            if (placedSurface != null)
                return placedSurface.gameObject;

            return placementRoot;
        }

        void LogDebug(string message)
        {
            if (!m_EnableDebugLogs)
                return;

            Debug.Log($"[{nameof(ObjectPlacementSelectionUI)}] {message}", this);
        }

        void RegisterPlacementRoot(GameObject placedObject)
        {
            if (placedObject == null)
                return;

            var placementRootMarker = placedObject.GetComponentInParent<PlacedObjectRootMarker>();
            var placementRoot =
                placementRootMarker != null
                    ? placementRootMarker.gameObject
                    : placedObject;

            if (placementRoot == null || m_RuntimePlacementRoots.Contains(placementRoot))
                return;

            m_RuntimePlacementRoots.Add(placementRoot);
        }

        void CleanupDestroyedPlacementRoots()
        {
            for (var index = m_RuntimePlacementRoots.Count - 1; index >= 0; --index)
            {
                if (m_RuntimePlacementRoots[index] == null)
                    m_RuntimePlacementRoots.RemoveAt(index);
            }
        }
    }
}
