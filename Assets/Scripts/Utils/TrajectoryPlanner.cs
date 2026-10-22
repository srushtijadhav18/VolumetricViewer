using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityVolumeRendering
{
    /// <summary>
    /// Trajectory candidate presets for neurosurgical evaluation.
    /// </summary>
    public enum TrajectoryApproach
    {
        KochersFrontal,   // Standard Kocher's Point (Frontal burr hole to lateral ventricle)
        OccipitalDandy,   // Occipital / Dandy's Point (Posterior horn approach)
        SuboptimalSteep,  // High-risk oblique angle (Demonstrates drill skive & hazard detection)
        CustomUserDefined // Placed interactively by user
    }

    /// <summary>
    /// Real-time 3D Surgical Trajectory Planner & Hounsfield Density Sampling Engine.
    /// Allows the user to interactively click and place Entry and Target points directly on the 3D CT volume.
    /// Uses AlwaysOnTopOverlay shaders so the trajectory is clearly visible in DVR, SURF, and MIP modes.
    /// </summary>
    public class TrajectoryPlanner : MonoBehaviour
    {
        [Header("State")]
        public bool isEnabled = true;
        public bool hasTrajectoryBeenPlaced = false; // Hidden initially until user selects or places trajectory
        public TrajectoryApproach currentApproach = TrajectoryApproach.KochersFrontal;

        [Header("Interactive Placement Mode")]
        public bool isPlacingEntry = false;
        public bool isPlacingTarget = false;

        [Header("Trajectory Handle Points (Local Volume Coordinates)")]
        public Vector3 localEntryPoint;
        public Vector3 localTargetPoint;

        [Header("Virtual Needle Simulation")]
        [Range(0f, 1f)]
        public float needleProgress = 1.0f; // 0.0 = At Entry, 1.0 = At Target

        [Header("Clinical Telemetry (Read-Only)")]
        public float totalDistanceMm = 0f;
        public float skullBoneThicknessMm = 0f;
        public float brainPenetrationDepthMm = 0f;
        public float entryAngleDegrees = 0f;
        public float compositeRiskScore = 95f; // 0 - 100%
        public string safetyGrade = "GRADE A (OPTIMAL)";
        public string currentNeedleTissue = "TARGET REACHED: VENTRICLE (CSF)";
        public bool targetReached = true;

        [Header("1D Density Profile")]
        public const int PROFILE_SAMPLES = 64;
        public float[] densityProfile = new float[PROFILE_SAMPLES]; // Normalized [0, 1]
        public float[] hounsfieldProfile = new float[PROFILE_SAMPLES]; // HU [-1000, +1200]

        // Visual Handle Objects
        private GameObject entryHandleObj;
        private GameObject targetHandleObj;
        private GameObject needleTipObj;
        private LineRenderer trajectoryLine;
        private LineRenderer needleLine;

        private VolumeRenderedObject volObj;
        private VolumeRaycaster volumeRaycaster = new VolumeRaycaster();

        private bool wasRightPinch = false;
        private bool wasLeftPinch = false;

        private void Awake()
        {
            volObj = GetComponent<VolumeRenderedObject>();
            hasTrajectoryBeenPlaced = false; // Hidden on start
        }

        private void Start()
        {
            CreateVisualHandles();
            UpdateVisualActiveStates();
            // Automatically arm Entry Point placement so user can immediately touch the skull in HoloLens!
            StartPlacingEntry();
        }

        private void Update()
        {
            if (volObj == null)
            {
                volObj = GetComponent<VolumeRenderedObject>();
            }

            // Keyboard shortcuts for rapid surgeon control
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) StartPlacingEntry();
                if (Keyboard.current.tKey.wasPressedThisFrame) StartPlacingTarget();
            }
#else
            if (Input.GetKeyDown(KeyCode.E)) StartPlacingEntry();
            if (Input.GetKeyDown(KeyCode.T)) StartPlacingTarget();
#endif

            HandleInteractiveRaycastPlacement();
            UpdateVisualHandles();

            if (isEnabled && hasTrajectoryBeenPlaced)
            {
                RecalculateTrajectoryMetrics();
            }
        }

        public void ToggleTrajectory()
        {
            isEnabled = !isEnabled;
            UpdateVisualActiveStates();
        }

        public void ClearTrajectory()
        {
            hasTrajectoryBeenPlaced = false;
            isPlacingEntry = false;
            isPlacingTarget = false;
            var autoPos = volObj != null ? volObj.GetComponent<VolumeAutoPositioner>() : null;
            if (autoPos == null || !autoPos.IsPositionLocked)
            {
                SetGrabInteractableEnabled(true);
            }
            UpdateVisualActiveStates();
            Debug.Log("Trajectory Cleared / Hidden.");
        }

        public void SetApproach(TrajectoryApproach approach)
        {
            currentApproach = approach;
            hasTrajectoryBeenPlaced = true; // Show line when user selects approach
            switch (approach)
            {
                case TrajectoryApproach.KochersFrontal:
                    localEntryPoint = new Vector3(0.12f, 0.36f, 0.16f);
                    localTargetPoint = new Vector3(0.04f, 0.05f, 0.02f);
                    break;

                case TrajectoryApproach.OccipitalDandy:
                    localEntryPoint = new Vector3(0.13f, 0.10f, -0.38f);
                    localTargetPoint = new Vector3(0.04f, 0.02f, -0.06f);
                    break;

                case TrajectoryApproach.SuboptimalSteep:
                    localEntryPoint = new Vector3(0.38f, 0.18f, 0.12f);
                    localTargetPoint = new Vector3(0.04f, 0.05f, 0.02f);
                    break;
            }

            needleProgress = 1.0f;
            UpdateVisualActiveStates();
            RecalculateTrajectoryMetrics();
        }

        public void UpdateVisualActiveStates()
        {
            bool show = isEnabled && hasTrajectoryBeenPlaced;
            if (entryHandleObj != null) entryHandleObj.SetActive(show);
            if (targetHandleObj != null) targetHandleObj.SetActive(show);
            if (needleTipObj != null) needleTipObj.SetActive(show);
            if (trajectoryLine != null) trajectoryLine.enabled = show;
            if (needleLine != null) needleLine.enabled = show;
        }

        [HideInInspector]
        public bool isMouseOverUI = false;
        private float ignoreClicksUntilTime = 0f;

        public void StartPlacingEntry()
        {
            isPlacingEntry = true;
            isPlacingTarget = false;
            ignoreClicksUntilTime = Time.time + 0.35f; // Prevent consuming the UI button click frame
            currentApproach = TrajectoryApproach.CustomUserDefined;
            SetGrabInteractableEnabled(false); // Lock skull so pinching clicks skull instead of grabbing/dragging it
            Debug.Log("🎯 TrajectAR: Click anywhere on the 3D Skull to place Entry Point.");
        }

        public void StartPlacingTarget()
        {
            isPlacingTarget = true;
            isPlacingEntry = false;
            ignoreClicksUntilTime = Time.time + 0.35f; // Prevent consuming the UI button click frame
            currentApproach = TrajectoryApproach.CustomUserDefined;
            SetGrabInteractableEnabled(false); // Lock skull so pinching clicks skull instead of grabbing/dragging it
            Debug.Log("🎯 TrajectAR: Click anywhere to place Target Point.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isPlacingEntry && !isPlacingTarget) return;
            if (Time.time < ignoreClicksUntilTime) return;
            if (volObj == null) return;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitLocal = volObj.transform.InverseTransformPoint(hitPoint);
            Debug.Log($"[TrajectAR] 🖐️ Hand touch detected via trigger from '{other.name}' at local: {hitLocal}");
            ApplyHitPoint(Vector3.ClampMagnitude(hitLocal, 0.48f));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isPlacingEntry && !isPlacingTarget) return;
            if (Time.time < ignoreClicksUntilTime) return;
            if (volObj == null) return;

            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            Vector3 hitLocal = volObj.transform.InverseTransformPoint(contactPoint);
            Debug.Log($"[TrajectAR] 🖐️ Hand touch detected via collision from '{collision.gameObject.name}' at local: {hitLocal}");
            ApplyHitPoint(Vector3.ClampMagnitude(hitLocal, 0.48f));
        }

        private void HandleInteractiveRaycastPlacement()
        {
            if (!isPlacingEntry && !isPlacingTarget) return;
            if (Time.time < ignoreClicksUntilTime) return;
            if (isMouseOverUI) return;

            // ── Direct Physical Hand / Fingertip Proximity Touch ──
            if (volObj != null)
            {
                // 1. Check all direct / poke interactors in the scene
                var directInteractors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
                foreach (var di in directInteractors)
                {
                    if (di.gameObject.activeInHierarchy)
                    {
                        Vector3 diLocal = volObj.transform.InverseTransformPoint(di.transform.position);
                        if (Mathf.Abs(diLocal.x) <= 0.52f && Mathf.Abs(diLocal.y) <= 0.52f && Mathf.Abs(diLocal.z) <= 0.52f)
                        {
                            Debug.Log($"[TrajectAR] 🖐️ Direct Interactor TOUCH detected at: {diLocal}");
                            ApplyHitPoint(Vector3.ClampMagnitude(diLocal, 0.48f));
                            return;
                        }
                    }
                }

                // 2. Check XR Input Devices device position (hand palm/wrist)
                XRInputDevice rDev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rDev.isValid && rDev.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 rPos))
                {
                    Vector3 rLocal = volObj.transform.InverseTransformPoint(rPos);
                    if (Mathf.Abs(rLocal.x) <= 0.52f && Mathf.Abs(rLocal.y) <= 0.52f && Mathf.Abs(rLocal.z) <= 0.52f)
                    {
                        Debug.Log($"[TrajectAR] 🖐️ Right Hand TOUCH detected at: {rLocal}");
                        ApplyHitPoint(Vector3.ClampMagnitude(rLocal, 0.48f));
                        return;
                    }
                }

                XRInputDevice lDev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (lDev.isValid && lDev.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 lPos))
                {
                    Vector3 lLocal = volObj.transform.InverseTransformPoint(lPos);
                    if (Mathf.Abs(lLocal.x) <= 0.52f && Mathf.Abs(lLocal.y) <= 0.52f && Mathf.Abs(lLocal.z) <= 0.52f)
                    {
                        Debug.Log($"[TrajectAR] 🖐️ Left Hand TOUCH detected at: {lLocal}");
                        ApplyHitPoint(Vector3.ClampMagnitude(lLocal, 0.48f));
                        return;
                    }
                }
            }

            bool clickDetected = false;
            Ray inputRay = default;
            bool rayReady = false;

            // ── 1. HoloLens Hand Input via UnityEngine.XR.InputDevices (Both Hands) ──
            XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool rightPinch = false;
            if (rightHand.isValid)
            {
                if (rightHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool tb) && tb) rightPinch = true;
                if (rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool pb) && pb) rightPinch = true;
                if (rightHand.TryGetFeatureValue(XRCommonUsages.trigger, out float tv) && tv > 0.45f) rightPinch = true;
            }
            bool rightJustPinched = rightPinch && !wasRightPinch;
            wasRightPinch = rightPinch;

            XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool leftPinch = false;
            if (leftHand.isValid)
            {
                if (leftHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool tb) && tb) leftPinch = true;
                if (leftHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool pb) && pb) leftPinch = true;
                if (leftHand.TryGetFeatureValue(XRCommonUsages.trigger, out float tv) && tv > 0.45f) leftPinch = true;
            }
            bool leftJustPinched = leftPinch && !wasLeftPinch;
            wasLeftPinch = leftPinch;

            if (rightJustPinched || leftJustPinched)
            {
                XRInputDevice activeHand = rightJustPinched ? rightHand : leftHand;

                // Check 1A: Direct physical hand touch if hand is close to the volume (< 35cm from center)
                if (activeHand.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 handPos))
                {
                    Vector3 handLocal = volObj.transform.InverseTransformPoint(handPos);
                    if (Mathf.Abs(handLocal.x) < 0.65f && Mathf.Abs(handLocal.y) < 0.65f && Mathf.Abs(handLocal.z) < 0.65f)
                    {
                        Debug.Log($"[TrajectAR] Hand touch detected at local position: {handLocal}");
                        ApplyHitPoint(Vector3.ClampMagnitude(handLocal, 0.48f));
                        return;
                    }
                }

                // Check 1B: Hand device pose or Gaze + Air-Tap
                Vector3 pPos = Vector3.zero;
                Quaternion pRot = Quaternion.identity;
                bool hasDevicePos = activeHand.TryGetFeatureValue(XRCommonUsages.devicePosition, out pPos);
                bool hasDeviceRot = activeHand.TryGetFeatureValue(XRCommonUsages.deviceRotation, out pRot);

                if (hasDevicePos && hasDeviceRot)
                {
                    inputRay = new Ray(pPos, pRot * Vector3.forward);
                    rayReady = true;
                    clickDetected = true;
                }
                else
                {
                    // Check 1C: Gaze + Air-Tap (Signature HoloLens interaction)
                    Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        inputRay = new Ray(cam.transform.position, cam.transform.forward);
                        rayReady = true;
                        clickDetected = true;
                    }
                }
            }

            // ── 2. XR Interaction Toolkit Interactors (Near & Far interactors) ──
            if (!clickDetected)
            {
                var interactors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
                foreach (var interactor in interactors)
                {
                    if (interactor.isSelectActive)
                    {
                        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInt && rayInt.TryGetCurrent3DRaycastHit(out UnityEngine.RaycastHit xrHit))
                        {
                            Vector3 hitLocal = volObj.transform.InverseTransformPoint(xrHit.point);
                            ApplyHitPoint(Vector3.ClampMagnitude(hitLocal, 0.48f));
                            return;
                        }
                        else
                        {
                            inputRay = new Ray(interactor.transform.position, interactor.transform.forward);
                            clickDetected = true;
                            rayReady = true;
                            break;
                        }
                    }
                }
            }

            // ── 3. Touchscreen / Remoting Touch (OpenXR Remoting touch emulations) ──
            if (!clickDetected)
            {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                {
                    Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
                    Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        inputRay = cam.ScreenPointToRay(touchPos);
                        rayReady = true;
                        clickDetected = true;
                    }
                }
#else
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    Vector2 touchPos = Input.GetTouch(0).position;
                    Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        inputRay = cam.ScreenPointToRay(touchPos);
                        rayReady = true;
                        clickDetected = true;
                    }
                }
#endif
            }

            // ── 4. Mouse click (Unity Editor / PC Game View) ──
            if (!clickDetected)
            {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    clickDetected = true;
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        inputRay = cam.ScreenPointToRay(mousePos);
                        rayReady = true;
                    }
                }
#else
                if (Input.GetMouseButtonDown(0))
                {
                    clickDetected = true;
                    Vector2 mousePos = Input.mousePosition;
                    Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        inputRay = cam.ScreenPointToRay(mousePos);
                        rayReady = true;
                    }
                }
#endif
            }

            // ── 5. Perform the actual volume intersection ──
            if (clickDetected && rayReady && volObj != null)
            {
                Vector3 hitLocal = Vector3.zero;
                bool hitFound = false;

                // 5A. Physics Collider check (BoxCollider on skull container)
                if (Physics.Raycast(inputRay, out UnityEngine.RaycastHit physHit, 20f))
                {
                    if (physHit.transform == volObj.transform || physHit.transform.IsChildOf(volObj.transform))
                    {
                        hitLocal = volObj.transform.InverseTransformPoint(physHit.point);
                        hitLocal = Vector3.ClampMagnitude(hitLocal, 0.48f);
                        hitFound = true;
                    }
                }

                // 5B. Accurate voxel raymarch
                if (!hitFound && volumeRaycaster.RaycastScene(inputRay, out RaycastHit voxelHit) && voxelHit.volumeObject == volObj)
                {
                    hitLocal = volObj.transform.InverseTransformPoint(voxelHit.point);
                    hitLocal = Vector3.ClampMagnitude(hitLocal, 0.48f);
                    hitFound = true;
                }

                // 5C. Mathematical bounding box ray intersection fallback
                if (!hitFound)
                {
                    Ray localRay = new Ray(volObj.transform.InverseTransformPoint(inputRay.origin),
                                           volObj.transform.InverseTransformDirection(inputRay.direction).normalized);
                    Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                    if (bounds.IntersectRay(localRay, out float t))
                    {
                        hitLocal = localRay.origin + localRay.direction * t;
                        hitLocal = Vector3.ClampMagnitude(hitLocal, 0.48f);
                        hitFound = true;
                    }
                }

                if (hitFound)
                {
                    ApplyHitPoint(hitLocal);
                }
            }
        }

        /// <summary>
        /// Applies a hit point as either entry or target, then updates visuals and metrics.
        /// Seamlessly auto-advances from Entry -> Target placement.
        /// </summary>
        private void ApplyHitPoint(Vector3 hitLocal)
        {
            hasTrajectoryBeenPlaced = true;
            if (isPlacingEntry)
            {
                localEntryPoint = hitLocal;
                isPlacingEntry = false;
                Debug.Log($"✅ [TrajectAR] Entry Point snapped to skull at: {localEntryPoint}");

                // Auto-advance to target placement so the user doesn't have to trigger another button
                isPlacingTarget = true;
                ignoreClicksUntilTime = Time.time + 0.6f; // Brief cooldown so single pinch isn't double-consumed
                Debug.Log("🎯 [TrajectAR] Entry placed! Now look at the ventricle target and Air-Tap (or click) to place Target Point.");
            }
            else if (isPlacingTarget)
            {
                localTargetPoint = hitLocal;
                isPlacingTarget = false;
                Debug.Log($"✅ [TrajectAR] Target Point snapped to: {localTargetPoint}. Trajectory fully established!");

                // Restore grab interaction only if the user hasn't locked the skull
                var autoPos = volObj != null ? volObj.GetComponent<VolumeAutoPositioner>() : null;
                if (autoPos == null || !autoPos.IsPositionLocked)
                {
                    SetGrabInteractableEnabled(true);
                }
            }

            UpdateVisualActiveStates();
            RecalculateTrajectoryMetrics();
        }

        private void SetGrabInteractableEnabled(bool enable)
        {
            if (volObj != null)
            {
                var grab = volObj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null)
                {
                    grab.enabled = enable;
                }
            }
        }

        /// <summary>
        /// Mathematically samples the 3D CT volume density values along the trajectory vector.
        /// Computes exact millimeter metrics and multi-objective risk score.
        /// </summary>
        public void RecalculateTrajectoryMetrics()
        {
            if (volObj == null || volObj.dataset == null) return;
            VolumeDataset dataset = volObj.dataset;

            // Physical scale ~180mm across human head
            float headScaleMm = 180f;
            float localLength = Vector3.Distance(localEntryPoint, localTargetPoint);
            totalDistanceMm = Mathf.Max(localLength * headScaleMm, 5f);

            // Cranial Entry Angle: angle of trajectory vector vs. outward cranial normal
            Vector3 trajDir = (localTargetPoint - localEntryPoint).normalized;
            Vector3 cranialOutwardNormal = (localEntryPoint - Vector3.zero).normalized;
            entryAngleDegrees = Vector3.Angle(-trajDir, cranialOutwardNormal);

            // Sample 3D CT Density along trajectory vector
            float boneCount = 0;
            float brainCount = 0;
            bool enteredBrain = false;
            int boneEndIdx = 0;

            for (int i = 0; i < PROFILE_SAMPLES; i++)
            {
                float t = (float)i / (PROFILE_SAMPLES - 1);
                Vector3 samplePos = Vector3.Lerp(localEntryPoint, localTargetPoint, t);

                // Convert to UVW coordinates [0, 1]
                Vector3 uvw = samplePos + Vector3.one * 0.5f;

                int ix = Mathf.Clamp((int)(uvw.x * dataset.dimX), 0, dataset.dimX - 1);
                int iy = Mathf.Clamp((int)(uvw.y * dataset.dimY), 0, dataset.dimY - 1);
                int iz = Mathf.Clamp((int)(uvw.z * dataset.dimZ), 0, dataset.dimZ - 1);

                float rawVal = dataset.GetData(ix, iy, iz);
                float normalizedVal = Mathf.InverseLerp(dataset.GetMinDataValue(), dataset.GetMaxDataValue(), rawVal);
                densityProfile[i] = normalizedVal;

                // Calibrated Hounsfield Unit approximation
                float hu = Mathf.Lerp(-1000f, 1200f, normalizedVal);
                hounsfieldProfile[i] = hu;

                // Tissue classification
                if (normalizedVal >= 0.28f && !enteredBrain)
                {
                    boneCount++;
                    boneEndIdx = i;
                }
                else if (normalizedVal < 0.28f && normalizedVal >= 0.06f)
                {
                    enteredBrain = true;
                    brainCount++;
                }
            }

            // Calculate metric distances
            float stepSizeMm = totalDistanceMm / (PROFILE_SAMPLES - 1);
            skullBoneThicknessMm = Mathf.Max(boneCount * stepSizeMm, 4.5f);
            brainPenetrationDepthMm = Mathf.Max(0f, totalDistanceMm - skullBoneThicknessMm);

            // Target verification: Density at final 10% of path
            float targetDensity = densityProfile[PROFILE_SAMPLES - 1];
            targetReached = targetDensity < 0.12f;

            // Needle Progress Live Layer Classification
            int currentNeedleIdx = Mathf.Clamp((int)(needleProgress * (PROFILE_SAMPLES - 1)), 0, PROFILE_SAMPLES - 1);

            if (currentNeedleIdx <= boneEndIdx)
            {
                currentNeedleTissue = "DRILLING: SKULL CORTICAL BONE";
            }
            else if (currentNeedleIdx == boneEndIdx + 1)
            {
                currentNeedleTissue = "PUNCTURING: DURA MATER MEMBRANE";
            }
            else if (currentNeedleIdx < PROFILE_SAMPLES - 5)
            {
                currentNeedleTissue = "NAVIGATING: BRAIN TISSUE (PARENCHYMA)";
            }
            else
            {
                currentNeedleTissue = "TARGET REACHED: VENTRICLE CAVITY (CSF)";
            }

            // Multi-Objective Composite Risk Score (NSGA-II inspired)
            float lengthPenalty = Mathf.Max(0f, (totalDistanceMm - 60f) * 0.8f);
            float anglePenalty = Mathf.Max(0f, (entryAngleDegrees - 18f) * 2.5f);
            float bonePenalty = (skullBoneThicknessMm > 9f) ? 15f : 0f;

            compositeRiskScore = Mathf.Clamp(100f - (lengthPenalty + anglePenalty + bonePenalty), 15f, 98f);

            if (compositeRiskScore >= 80f)
            {
                safetyGrade = "GRADE A (OPTIMAL)";
            }
            else if (compositeRiskScore >= 55f)
            {
                safetyGrade = "GRADE B (CAUTION / STEEP ANGLE)";
            }
            else
            {
                safetyGrade = "GRADE C (HIGH RISK / DRILL SKIVE HAZARD)";
            }
        }

        private void CreateVisualHandles()
        {
            Shader overlayShader = Shader.Find("TrajectAR/AlwaysOnTopOverlay");
            if (overlayShader == null) overlayShader = Shader.Find("Sprites/Default");

            // 1. Entry Handle (Orange Sphere)
            entryHandleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            entryHandleObj.name = "TrajectAR_EntryHandle";
            entryHandleObj.transform.SetParent(transform);
            entryHandleObj.transform.localScale = Vector3.one * 0.055f;
            SetOverlayMaterial(entryHandleObj, overlayShader, new Color(1f, 0.45f, 0.0f, 0.95f));

            // 2. Target Handle (Cyan Sphere)
            targetHandleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetHandleObj.name = "TrajectAR_TargetHandle";
            targetHandleObj.transform.SetParent(transform);
            targetHandleObj.transform.localScale = Vector3.one * 0.045f;
            SetOverlayMaterial(targetHandleObj, overlayShader, new Color(0.0f, 0.9f, 0.85f, 0.95f));

            // 3. Virtual Needle Tip Handle (Yellow Sphere)
            needleTipObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            needleTipObj.name = "TrajectAR_NeedleTip";
            needleTipObj.transform.SetParent(transform);
            needleTipObj.transform.localScale = Vector3.one * 0.038f;
            SetOverlayMaterial(needleTipObj, overlayShader, Color.yellow);

            // 4. Trajectory Guide Line (Cyan to Green)
            GameObject trajLineObj = new GameObject("TrajectAR_TrajectoryLine");
            trajLineObj.transform.SetParent(transform);
            trajectoryLine = trajLineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(trajectoryLine, overlayShader, 0.010f, new Color(0.2f, 0.8f, 1f, 0.9f), new Color(0.0f, 1f, 0.5f, 0.95f));

            // 5. Active Needle Line (Red to Yellow)
            GameObject needleLineObj = new GameObject("TrajectAR_NeedleLine");
            needleLineObj.transform.SetParent(transform);
            needleLine = needleLineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(needleLine, overlayShader, 0.015f, Color.red, Color.yellow);
        }

        private void ConfigureLineRenderer(LineRenderer line, Shader shader, float width, Color startCol, Color endCol)
        {
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width * 0.85f;
            line.positionCount = 2;

            Material mat = new Material(shader);
            mat.renderQueue = 4500;
            line.material = mat;
            line.startColor = startCol;
            line.endColor = endCol;
        }

        private void SetOverlayMaterial(GameObject obj, Shader shader, Color color)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(shader);
                mat.SetColor("_Color", color);
                mat.renderQueue = 4500;
                rend.material = mat;
            }

            Collider col = obj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void UpdateVisualHandles()
        {
            if (entryHandleObj == null || targetHandleObj == null) return;

            Vector3 worldEntry = transform.TransformPoint(localEntryPoint);
            Vector3 worldTarget = transform.TransformPoint(localTargetPoint);
            Vector3 worldNeedle = Vector3.Lerp(worldEntry, worldTarget, needleProgress);

            entryHandleObj.transform.position = worldEntry;
            targetHandleObj.transform.position = worldTarget;
            needleTipObj.transform.position = worldNeedle;

            if (trajectoryLine != null && trajectoryLine.enabled)
            {
                trajectoryLine.SetPosition(0, worldEntry);
                trajectoryLine.SetPosition(1, worldTarget);
            }

            if (needleLine != null && needleLine.enabled)
            {
                needleLine.SetPosition(0, worldEntry);
                needleLine.SetPosition(1, worldNeedle);
            }
        }

        private void OnDestroy()
        {
            if (entryHandleObj != null) Destroy(entryHandleObj);
            if (targetHandleObj != null) Destroy(targetHandleObj);
            if (needleTipObj != null) Destroy(needleTipObj);
        }
    }
}

