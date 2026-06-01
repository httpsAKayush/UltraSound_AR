using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Fires one temporary physics projectile per activate press using the interactor ray direction.
    /// Intended for controller trigger or hand activate/pinch on a left-side Near-Far or Ray interactor.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaycastProjectileShooter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Interactor that provides both the activate state and the ray direction.")]
        XRBaseInputInteractor m_InputInteractor;

        [SerializeField]
        [Tooltip("Projectile prefab to spawn. Expected to contain a collider and usually a rigidbody.")]
        GameObject m_ProjectilePrefab;

        [SerializeField]
        [Tooltip("Projectile speed in meters per second.")]
        float m_LaunchSpeed = 8f;

        [SerializeField]
        [Tooltip("How long the projectile lives before it is destroyed.")]
        float m_ProjectileLifetimeSeconds = 7f;

        [SerializeField]
        [Tooltip("Small forward offset from the ray origin so the projectile does not spawn inside the hand/controller.")]
        float m_SpawnForwardOffset = 0.06f;

        [SerializeField]
        [Tooltip("Ignore projectile firing while the interactor is pointing at UI.")]
        bool m_BlockWhilePointerOverUI = true;

        [SerializeField]
        [Tooltip("Also fire on Select when Activate is not emitted. Enable this for hand pinch input.")]
        bool m_AllowSelectFallbackForActivate = true;

        [SerializeField]
        [Tooltip("Minimum time between projectile shots. Prevents a held pinch from firing repeatedly.")]
        float m_MinSecondsBetweenShots = 0.3f;

        [SerializeField]
        bool m_EnableDebugLogs = true;

        float m_LastShotTime = float.NegativeInfinity;

        void Awake()
        {
            if (m_InputInteractor == null)
                m_InputInteractor = GetComponent<XRBaseInputInteractor>();
        }

        void Update()
        {
            if (m_InputInteractor == null || m_ProjectilePrefab == null)
                return;

            var activateStarted = m_InputInteractor.logicalActivateState.wasPerformedThisFrame;
            var selectStarted =
                m_AllowSelectFallbackForActivate &&
                !activateStarted &&
                m_InputInteractor.logicalSelectState.wasPerformedThisFrame;

            if (!activateStarted && !selectStarted)
                return;

            if (Time.unscaledTime - m_LastShotTime < Mathf.Max(0.01f, m_MinSecondsBetweenShots))
            {
                LogDebug("Projectile fire ignored because the shot cooldown is still active.");
                return;
            }

            if (m_BlockWhilePointerOverUI && IsPointerOverUI())
            {
                LogDebug("Projectile fire cancelled because pointer is over UI.");
                return;
            }

            m_LastShotTime = Time.unscaledTime;
            LogDebug(activateStarted ? "Projectile fired from Activate input." : "Projectile fired from Select fallback input.");
            FireProjectile();
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

        void FireProjectile()
        {
            if (m_InputInteractor is not IXRRayProvider rayProvider)
            {
                Debug.LogWarning($"[{nameof(RaycastProjectileShooter)}] Input interactor does not implement IXRRayProvider.", this);
                return;
            }

            var rayOrigin = rayProvider.GetOrCreateRayOrigin();
            if (rayOrigin == null)
            {
                Debug.LogWarning($"[{nameof(RaycastProjectileShooter)}] Ray origin is missing.", this);
                return;
            }

            var direction = rayProvider.rayEndPoint - rayOrigin.position;
            if (direction.sqrMagnitude < 0.0001f)
                direction = rayOrigin.forward;

            direction.Normalize();

            var spawnPosition = rayOrigin.position + direction * Mathf.Max(0f, m_SpawnForwardOffset);
            var projectile = Instantiate(m_ProjectilePrefab, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            var rigidbody = projectile.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = projectile.AddComponent<Rigidbody>();

            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.linearVelocity = direction * m_LaunchSpeed;
            rigidbody.angularVelocity = Vector3.zero;

            Destroy(projectile, Mathf.Max(0.1f, m_ProjectileLifetimeSeconds));
            LogDebug($"Fired projectile {projectile.name} at speed {m_LaunchSpeed}.");
        }

        void LogDebug(string message)
        {
            if (!m_EnableDebugLogs)
                return;

            Debug.Log($"[{nameof(RaycastProjectileShooter)}] {message}", this);
        }
    }
}
