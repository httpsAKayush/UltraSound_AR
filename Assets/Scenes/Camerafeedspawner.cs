using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaXR.LofiStudy.ARFoundation
{
    public class CameraFeedSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        public GameObject feedPrefab;

        [Header("Spawn Position")]
        public float spawnDistance  = 1.5f;
        public float verticalOffset = 0f;

        [Header("References")]
        public Transform cameraTransform;
        public ObjectPlacementSelectionUI placementUI;
        public MicrusControlPanel controlPanel;

        [Header("Placers — assign both NearFarInteractor placers here")]
        public XRRayARPlanePlacer[] placers;   // drag both placers in Inspector

        void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

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

            // Spawn position — forward from camera
            var forward       = cameraTransform.forward;
            forward.y         = 0;
            forward.Normalize();

            var spawnPosition = cameraTransform.position
                                + forward * spawnDistance
                                + Vector3.up * verticalOffset;

            // Face screen toward player
            var lookDir       = spawnPosition - cameraTransform.position;
            lookDir.y         = 0;
            var spawnRotation = lookDir.sqrMagnitude > 0.001f
                                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                                : Quaternion.identity;

            // Spawn feed prefab
            var instance = Instantiate(feedPrefab, spawnPosition, spawnRotation);

            if (instance.GetComponent<PlacedObjectRootMarker>() == null)
                instance.AddComponent<PlacedObjectRootMarker>();

            ConfigureGrabInteraction(instance);

            if (controlPanel != null)
                controlPanel.BuildControlPanels(instance);

            // ── KEY CHANGE ───────────────────────────────────────────────
            // Disarm all placers so Trigger is no longer consumed by
            // AR plane placement — it becomes free for UI button clicks.
            DisarmAllPlacers();
            // ─────────────────────────────────────────────────────────────

            Debug.Log("[CameraFeedSpawner] Feed screen spawned. Placers disarmed — trigger now controls UI.");
        }

        void DisarmAllPlacers()
        {
            if (placers == null) return;
            foreach (var placer in placers)
            {
                if (placer != null)
                    placer.DisarmPlacement();
            }
        }

        void ConfigureGrabInteraction(GameObject target)
        {
            // Shrink collider to screen area only — don't cover side panels
            var existingCol = target.GetComponentInChildren<BoxCollider>();
            if (existingCol != null)
            {
                existingCol.center = new Vector3(0f, 0.5f, 0f);
                existingCol.size   = new Vector3(1.8f, 1.1f, 0.05f);
            }
            else
            {
                var col    = target.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size   = new Vector3(1.8f, 1.1f, 0.05f);
            }

            var rb = target.GetComponent<Rigidbody>();
            if (rb == null) rb = target.AddComponent<Rigidbody>();
            rb.useGravity             = false;
            rb.isKinematic            = true;
            rb.linearVelocity         = Vector3.zero;
            rb.angularVelocity        = Vector3.zero;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = target.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = target.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach    = false;
            grab.trackPosition    = true;
            grab.trackRotation    = false;
            grab.trackScale       = false; // ← ADD THIS — prevents scale change on grab
            grab.movementType     = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = false;
            grab.attachEaseInTime = 0f;

            var attachObj = new GameObject("GrabAttach");
            attachObj.transform.SetParent(target.transform, false);
            attachObj.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            grab.attachTransform = attachObj.transform;

            if (target.GetComponent<PlacedObjectSurface>() == null)
                target.AddComponent<PlacedObjectSurface>();
        }
    }
}