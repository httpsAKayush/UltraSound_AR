using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Spawns the camera feed prefab directly in front of the player's view on button press.
    /// No plane detection required — the object spawns at a fixed offset from the camera.
    /// After spawning, the object is fully grabbable and moveable anywhere in the scene,
    /// physics-togglable, and resettable via the existing ObjectPlacementSelectionUI system.
    ///
    /// Setup:
    ///   1. Add this script to any GameObject in your AR1 scene (e.g. the Canvas or a manager object).
    ///   2. Assign the FeedPrefab field (your emptyfeedcube prefab).
    ///   3. Wire a UI Button's OnClick to this script's SpawnFeedScreen() method.
    ///   4. Optionally assign the ObjectPlacementSelectionUI so spawned objects
    ///      are tracked by the reset/revert button.
    /// </summary>
    public class CameraFeedSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("The camera feed prefab to spawn (emptyfeedcube).")]
        public GameObject feedPrefab;

        [Header("Spawn Position")]
        [Tooltip("How far in front of the camera to spawn the screen.")]
        public float spawnDistance = 1.0f;

        [Tooltip("Vertical offset from the camera forward point. Negative = slightly below eye level.")]
        public float verticalOffset = -0.1f;

        [Header("References")]
        [Tooltip("The main camera (XR camera). If null, Camera.main is used.")]
        public Transform cameraTransform;

        [Tooltip("Optional: assign ObjectPlacementSelectionUI so spawned objects " +
                 "are tracked by the revert/reset button.")]
        public ObjectPlacementSelectionUI placementUI;

        void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        /// <summary>
        /// Call this from a UI Button's OnClick event.
        /// Spawns the feed screen in front of the player, fully grabbable.
        /// </summary>
        public void SpawnFeedScreen()
        {
            if (feedPrefab == null)
            {
                Debug.LogWarning("[CameraFeedSpawner] No feed prefab assigned.");
                return;
            }

            if (cameraTransform == null)
            {
                Debug.LogWarning("[CameraFeedSpawner] No camera transform found.");
                return;
            }

            // Calculate spawn position: forward from camera + vertical offset
            var forward        = cameraTransform.forward;
            var spawnPosition  = cameraTransform.position
                                 + forward * spawnDistance
                                 + Vector3.up * verticalOffset;

            // Face the screen toward the player
            var lookDirection  = cameraTransform.position - spawnPosition;
            var spawnRotation  = lookDirection.sqrMagnitude > 0.001f
                                 ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                                 : Quaternion.identity;

            // Spawn
            var instance = Instantiate(feedPrefab, spawnPosition, spawnRotation);

            // Add PlacedObjectRootMarker so the revert button can track and destroy it
            if (instance.GetComponent<PlacedObjectRootMarker>() == null)
                instance.AddComponent<PlacedObjectRootMarker>();

            // Configure grab interaction (same as XRRayARPlanePlacer does)
            ConfigureGrabInteraction(instance);

            Debug.Log($"[CameraFeedSpawner] Feed screen spawned at {spawnPosition}.");
        }

        void ConfigureGrabInteraction(GameObject target)
        {
            // Ensure there's a collider
            if (target.GetComponentInChildren<Collider>() == null)
                target.AddComponent<BoxCollider>();

            // Rigidbody — kinematic so it floats in place until grabbed
            var rb = target.GetComponent<Rigidbody>();
            if (rb == null)
                rb = target.AddComponent<Rigidbody>();

            rb.useGravity              = false;
            rb.isKinematic             = true;
            rb.linearVelocity          = Vector3.zero;
            rb.angularVelocity         = Vector3.zero;
            rb.interpolation           = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode  = CollisionDetectionMode.ContinuousSpeculative;

            // XRGrabInteractable — makes it grabbable with controllers/hands
            var grab = target.GetComponent<XRGrabInteractable>();
            if (grab == null)
                grab = target.AddComponent<XRGrabInteractable>();

            grab.throwOnDetach    = false;
            grab.trackPosition    = true;
            grab.trackRotation    = true;
            grab.movementType     = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = false;
            grab.attachEaseInTime = 0f;

            // Grab attach point — slightly in front of object center
            var attachObj = new GameObject("Grab Attach");
            attachObj.transform.SetParent(target.transform, false);
            attachObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            attachObj.transform.localRotation = Quaternion.identity;
            grab.attachTransform = attachObj.transform;

            // PlacedObjectSurface — needed for ObjectPlacementSelectionUI grab toggling
            if (target.GetComponent<PlacedObjectSurface>() == null)
                target.AddComponent<PlacedObjectSurface>();
        }
    }
}