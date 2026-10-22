using UnityEngine;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityVolumeRendering
{
    /// <summary>
    /// Automatically positions and scales the skull volume directly in front of the HoloLens / Main Camera at eye level.
    /// Uses horizontal gaze projection to prevent the skull from appearing low on the floor.
    /// Supports Key 'P' (reset position), Up/Down arrows for height adjustment, and UI buttons.
    /// </summary>
    public class VolumeAutoPositioner : MonoBehaviour
    {
        [SerializeField]
        private float distanceInFrontOfCamera = 0.85f; // 85 cm in front for optimal HoloLens viewing

        [SerializeField]
        private float verticalOffset = 0.05f; // +5 cm above camera center for exact eye-level placement

        [SerializeField]
        private float targetHologramSize = 0.35f; // 35 cm bounding box size for AR viewing

        [SerializeField]
        private bool autoPositionOnStart = true;

        [Header("Position Locking")]
        [SerializeField]
        private bool isPositionLocked = false;
        public bool IsPositionLocked => isPositionLocked;


        private void Start()
        {
            // ── HoloLens: Force transparent background (no blue/white skybox) ──
            Camera cam = GetMainCamera();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Fully transparent black
            }

            if (autoPositionOnStart)
            {
                PositionInFrontOfCamera();
            }
        }

        private void Update()
        {
            Camera cam = GetMainCamera();
            if (cam != null)
            {
                // Force safe near clip plane to prevent near-geometry stretching inside volume
                if (cam.nearClipPlane > 0.08f)
                {
                    cam.nearClipPlane = 0.05f;
                }
            }

            // Keyboard shortcuts
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.pKey.wasPressedThisFrame)
                {
                    PositionInFrontOfCamera();
                }
                if (Keyboard.current.kKey.wasPressedThisFrame)
                {
                    TogglePositionLock();
                }
                if (Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    RaiseHeight(0.1f);
                }
                if (Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    LowerHeight(0.1f);
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.P))
            {
                PositionInFrontOfCamera();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                TogglePositionLock();
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                RaiseHeight(0.1f);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                LowerHeight(0.1f);
            }
#endif
        }

        /// <summary>
        /// Toggles locking the skull position so hand gestures cannot move or stick to it.
        /// </summary>
        public void TogglePositionLock()
        {
            SetPositionLocked(!isPositionLocked);
        }

        public void SetPositionLocked(bool locked)
        {
            isPositionLocked = locked;
            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
            {
                grab.enabled = !locked;
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Debug.Log(locked ? "🔒 TrajectAR: Skull position LOCKED in world space. Hand gestures will not move the skull."
                             : "🔓 TrajectAR: Skull position UNLOCKED. You can now pinch-grab and move the skull.");
        }

        public void PositionInFrontOfCamera()
        {
            Camera cam = GetMainCamera();
            if (cam == null) return;

            cam.nearClipPlane = 0.05f;

            // Normalize volume scale to comfortable AR hologram size (35cm)
            EnsureComfortableScale();

            // Project camera forward onto horizontal plane (XZ) so head pitch doesn't push target down to floor
            Vector3 forwardHorizontal = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forwardHorizontal.sqrMagnitude < 0.01f)
            {
                forwardHorizontal = cam.transform.forward;
            }

            // Calculate position directly in front at eye level
            Vector3 targetPosition = cam.transform.position 
                + (forwardHorizontal * distanceInFrontOfCamera) 
                + (Vector3.up * verticalOffset);

            transform.position = targetPosition;

            // Face camera upright
            Vector3 lookDir = cam.transform.position - transform.position;
            lookDir.y = 0; // Keep upright
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
            }


            Debug.Log($"Volume skull positioned at EYE LEVEL ({distanceInFrontOfCamera}m in front, +{verticalOffset}m height).");
        }

        public void RaiseHeight(float delta = 0.1f)
        {
            verticalOffset += delta;
            PositionInFrontOfCamera();
        }

        public void LowerHeight(float delta = 0.1f)
        {
            verticalOffset -= delta;
            PositionInFrontOfCamera();
        }

        public void SetVerticalOffset(float offset)
        {
            verticalOffset = offset;
            PositionInFrontOfCamera();
        }

        private void EnsureComfortableScale()
        {
            // If scale is default 1.0 or too large, scale it down to targetHologramSize (0.35f) for AR
            float currentMaxScale = Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            if (currentMaxScale > 0.5f)
            {
                transform.localScale = Vector3.one * targetHologramSize;
            }
        }

        private Camera GetMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindObjectOfType<Camera>();
            }
            return cam;
        }
    }
}


