using UnityEngine;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityVolumeRendering
{
    /// <summary>
    /// Presentation shortcuts for live demonstration to professor / sir.
    /// Supports Unity's New Input System package cleanly.
    /// </summary>
    public class DemoKeyboardShortcuts : MonoBehaviour
    {
        private VolumeRenderedObject volObj;

        private void Start()
        {
            volObj = GetComponent<VolumeRenderedObject>();
            if (volObj == null)
            {
                volObj = FindObjectOfType<VolumeRenderedObject>();
            }
        }

        private void Update()
        {
            if (volObj == null) return;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                {
                    volObj.SetRenderMode(RenderMode.DirectVolumeRendering);
                    Debug.Log("Mode Switched: Direct Volume Rendering (DVR)");
                }

                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                {
                    volObj.SetRenderMode(RenderMode.MaximumIntensityProjectipon);
                    Debug.Log("Mode Switched: Maximum Intensity Projection (MIP)");
                }

                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                {
                    volObj.SetRenderMode(RenderMode.IsosurfaceRendering);
                    Debug.Log("Mode Switched: Isosurface Surface Rendering (SURF)");
                }

                if (Keyboard.current.bKey.wasPressedThisFrame)
                {
                    volObj.SetBonePreset();
                    Debug.Log("Preset Applied: Restored Clean White Bone Structure");
                }

                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    volObj.SetVisibilityWindow(0.0f, 1.0f);
                    Debug.Log("Preset Applied: Full Volume (Skin & Tissue)");
                }

                if (Keyboard.current.lKey.wasPressedThisFrame)
                {
                    bool current = volObj.GetLightingEnabled();
                    volObj.SetLightingEnabled(!current);
                    Debug.Log("Lighting Toggled: " + (!current ? "ON" : "OFF"));
                }

                if (Keyboard.current.tKey.wasPressedThisFrame)
                {
                    TrajectoryPlanner planner = volObj.GetComponent<TrajectoryPlanner>();
                    if (planner != null)
                    {
                        planner.ToggleTrajectory();
                        Debug.Log("3D Trajectory Planning Toggled: " + (planner.isEnabled ? "ON" : "OFF"));
                    }
                }
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    CranialClippingController clipper = volObj.GetComponent<CranialClippingController>();
                    if (clipper != null)
                    {
                        clipper.ToggleClipping();
                        Debug.Log("Cranial Cut Plane Toggled: " + (clipper.isClippingEnabled ? "ON" : "OFF"));
                    }
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                volObj.SetRenderMode(RenderMode.DirectVolumeRendering);
                Debug.Log("Mode Switched: Direct Volume Rendering (DVR)");
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                volObj.SetRenderMode(RenderMode.MaximumIntensityProjectipon);
                Debug.Log("Mode Switched: Maximum Intensity Projection (MIP)");
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                volObj.SetRenderMode(RenderMode.IsosurfaceRendering);
                Debug.Log("Mode Switched: Isosurface Surface Rendering (SURF)");
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                volObj.SetBonePreset();
                Debug.Log("Preset Applied: Restored Clean White Bone Structure");
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                volObj.SetVisibilityWindow(0.0f, 1.0f);
                Debug.Log("Preset Applied: Full Volume (Skin & Tissue)");
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                bool current = volObj.GetLightingEnabled();
                volObj.SetLightingEnabled(!current);
                Debug.Log("Lighting Toggled: " + (!current ? "ON" : "OFF"));
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                TrajectoryPlanner planner = volObj.GetComponent<TrajectoryPlanner>();
                if (planner != null)
                {
                    planner.ToggleTrajectory();
                    Debug.Log("3D Trajectory Planning Toggled: " + (planner.isEnabled ? "ON" : "OFF"));
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                CranialClippingController clipper = volObj.GetComponent<CranialClippingController>();
                if (clipper != null)
                {
                    clipper.ToggleClipping();
                    Debug.Log("Cranial Cut Plane Toggled: " + (clipper.isClippingEnabled ? "ON" : "OFF"));
                }
            }
#endif
        }
    }
}
