using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Live On-Screen Runtime Control Panel for Play Mode.
    /// Includes the complete TrajectAR Surgical Navigation HUD, 1D Hounsfield Density Profile Graph,
    /// 3D Skull Cut Plane (Craniotomy), and dedicated Medivis 2D CT Multi-Planar Slices window.
    /// </summary>
    public class VolumeControlUI : MonoBehaviour
    {
        [SerializeField]
        private bool showUI = true;

        private VolumeRenderedObject volObj;
        private TrajectoryPlanner trajPlanner;
        private CranialClippingController clippingController;
        private MedivisSliceViewer medivisViewer;

        private Rect windowRect = new Rect(20, 20, 450, 700);
        private Rect trajWindowRect = new Rect(490, 20, 460, 680);
        private Rect medivisWindowRect = new Rect(970, 20, 440, 460);

        private Vector2 mainScrollPos = Vector2.zero;

        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle smallButtonStyle;
        private GUIStyle toggleStyle;
        private GUIStyle headerStyle;
        private GUIStyle badgeStyle;

        private Texture2D graphTexture;
        private Color[] graphPixels;
        private const int GRAPH_W = 200;
        private const int GRAPH_H = 60;

        private void Start()
        {
            volObj = GetComponent<VolumeRenderedObject>();
            if (volObj == null)
            {
                volObj = FindObjectOfType<VolumeRenderedObject>();
            }

            if (volObj != null)
            {
                trajPlanner = volObj.GetComponent<TrajectoryPlanner>();
                clippingController = volObj.GetComponent<CranialClippingController>();
                medivisViewer = volObj.GetComponent<MedivisSliceViewer>();
            }

            InitGraphTexture();
        }

        private void InitGraphTexture()
        {
            graphTexture = new Texture2D(GRAPH_W, GRAPH_H, TextureFormat.RGBA32, false);
            graphTexture.filterMode = FilterMode.Point;
            graphPixels = new Color[GRAPH_W * GRAPH_H];
        }

        private void OnGUI()
        {
            if (!showUI || volObj == null) return;

            if (trajPlanner == null && volObj != null) trajPlanner = volObj.GetComponent<TrajectoryPlanner>();
            if (clippingController == null && volObj != null) clippingController = volObj.GetComponent<CranialClippingController>();
            if (medivisViewer == null && volObj != null) medivisViewer = volObj.GetComponent<MedivisSliceViewer>();

            if (trajPlanner != null && Event.current != null)
            {
                Vector2 mousePos = Event.current.mousePosition;
                bool overMain = windowRect.Contains(mousePos);
                bool overTraj = trajPlanner.isEnabled && trajWindowRect.Contains(mousePos);
                bool overMedivis = trajPlanner.isEnabled && medivisWindowRect.Contains(mousePos);
                trajPlanner.isMouseOverUI = overMain || overTraj || overMedivis;
            }

            SetupStyles();

            windowRect.height = Mathf.Clamp(Screen.height - 40, 520, 780);

            // 1. Main Volume Controls Window
            windowRect = GUI.Window(999, windowRect, DrawVolumeControlWindow, "TrajectAR Volume Controls");

            // 2. Trajectory Planning Telemetry Window
            if (trajPlanner != null && trajPlanner.isEnabled)
            {
                trajWindowRect = GUI.Window(1000, trajWindowRect, DrawTrajectoryWindow, "🎯 TrajectAR Trajectory Planner");

                // 3. Dedicated Medivis 2D CT Orthogonal Slices & Bullseye Window
                medivisWindowRect.x = Mathf.Clamp(medivisWindowRect.x, trajWindowRect.x + trajWindowRect.width + 10, Screen.width - 450);
                medivisWindowRect = GUI.Window(1001, medivisWindowRect, DrawMedivisWindow, "🩺 Medivis 2D CT Slices (MPR)");
            }
        }

        private void SetupStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.3f, 0.85f, 1f) }
                };

                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 38
                };

                smallButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 34
                };

                toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };

                badgeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private void DrawVolumeControlWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 1000, 30));
            Color oldBg = GUI.backgroundColor;

            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos, GUILayout.Width(windowRect.width - 20), GUILayout.Height(windowRect.height - 40));

            GUILayout.Space(4);

            // 1. Visibility Window Sliders
            Vector2 visWindow = volObj.GetVisibilityWindow();
            GUILayout.Label($"Skin/Bone Peel (Min Density): {visWindow.x:F2}", labelStyle);
            float newMin = GUILayout.HorizontalSlider(visWindow.x, 0.0f, 1.0f, GUILayout.Height(22));

            GUILayout.Space(4);
            GUILayout.Label($"Max Density Threshold: {visWindow.y:F2}", labelStyle);
            float newMax = GUILayout.HorizontalSlider(visWindow.y, 0.0f, 1.0f, GUILayout.Height(22));

            if (newMin != visWindow.x || newMax != visWindow.y)
            {
                volObj.SetVisibilityWindow(newMin, newMax);
            }

            GUILayout.Space(10);
            GUILayout.Label("--- Render Modes ---", headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("DVR (Volume)", buttonStyle)) volObj.SetRenderMode(RenderMode.DirectVolumeRendering);
            if (GUILayout.Button("MIP (X-Ray)", buttonStyle)) volObj.SetRenderMode(RenderMode.MaximumIntensityProjectipon);
            if (GUILayout.Button("SURF (3D Bone)", buttonStyle)) volObj.SetRenderMode(RenderMode.IsosurfaceRendering);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("--- Quick Presets ---", headerStyle);

            // ── RESET BUTTON: Always get back to the clean white skull ──
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1.0f);
            if (GUILayout.Button("🔄 Reset to Starting Skull", buttonStyle))
            {
                // 1. Clean white bone transfer function
                volObj.SetBonePreset();
                // 2. Turn OFF cut plane if active
                if (clippingController != null && clippingController.isClippingEnabled)
                    clippingController.ToggleClipping();
                // 3. Hide trajectory
                if (trajPlanner != null)
                    trajPlanner.ClearTrajectory();
                // 4. Destroy any in-scene slicing planes
                SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
                foreach (var p in planes) Destroy(p.gameObject);
                Debug.Log("Reset to starting clean skull.");
            }
            GUI.backgroundColor = oldBg;

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🦴 White Bone (B)", buttonStyle)) volObj.SetBonePreset();
            if (GUILayout.Button("Full Volume (F)", buttonStyle)) volObj.SetVisibilityWindow(0.0f, 1.0f);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.55f);
            if (GUILayout.Button("🧠 Reveal Brain & Open Skull", buttonStyle))
            {
                volObj.SetBrainInteriorPreset();
                if (clippingController != null && !clippingController.isClippingEnabled)
                {
                    clippingController.ToggleClipping();
                }
            }
            GUI.backgroundColor = oldBg;

            GUILayout.Space(4);
            if (GUILayout.Button("📄 Spawn 3D In-Scene CT Slice Plane", buttonStyle))
            {
                volObj.CreateSlicingPlane();
                Debug.Log("Spawned In-Scene 3D Slicing Plane inside volume.");
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Eye Level (P)", buttonStyle))
            {
                VolumeAutoPositioner positioner = volObj.GetComponent<VolumeAutoPositioner>();
                if (positioner != null) positioner.PositionInFrontOfCamera();
            }
            if (GUILayout.Button("⬆️ Raise", buttonStyle))
            {
                VolumeAutoPositioner positioner = volObj.GetComponent<VolumeAutoPositioner>();
                if (positioner != null) positioner.RaiseHeight(0.1f);
            }
            if (GUILayout.Button("⬇️ Lower", buttonStyle))
            {
                VolumeAutoPositioner positioner = volObj.GetComponent<VolumeAutoPositioner>();
                if (positioner != null) positioner.LowerHeight(0.1f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            VolumeAutoPositioner autoPos = volObj.GetComponent<VolumeAutoPositioner>();
            bool isPosLocked = autoPos != null && autoPos.IsPositionLocked;
            GUI.backgroundColor = isPosLocked ? new Color(1.0f, 0.45f, 0.45f) : new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button(isPosLocked ? "🔒 Skull Position: LOCKED (K to Unlock)" : "🔓 Skull Position: UNLOCKED (K to Lock)", buttonStyle))
            {
                if (autoPos != null) autoPos.TogglePositionLock();
            }
            GUI.backgroundColor = oldBg;

            GUILayout.Space(10);
            bool trajActive = (trajPlanner != null && trajPlanner.isEnabled);
            GUI.backgroundColor = trajActive ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button(trajActive ? "✅ 3D Surgical Trajectory (T): ACTIVE" : "❌ 3D Surgical Trajectory (T): OFF", buttonStyle))
            {
                if (trajPlanner != null) trajPlanner.ToggleTrajectory();
            }
            GUI.backgroundColor = oldBg;

            // ── 3D Skull Cut Plane & Adjusters (Always Visible) ──
            GUILayout.Space(10);
            GUILayout.Label("--- 3D Skull Cut Plane (Craniotomy) ---", headerStyle);
            bool cutActive = (clippingController != null && clippingController.isClippingEnabled);
            GUI.backgroundColor = cutActive ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.7f, 0.3f, 0.3f);
            if (GUILayout.Button(cutActive ? "🪚 Skull Cut Plane (C): ACTIVE" : "🪚 Skull Cut Plane (C): OFF", buttonStyle))
            {
                if (clippingController != null)
                {
                    clippingController.ToggleClipping();
                }
            }
            GUI.backgroundColor = oldBg;

            if (clippingController != null)
            {
                GUILayout.Space(4);
                string cutStatus = cutActive ? "CUT ACTIVE" : "INACTIVE (Click button above to cut)";
                GUILayout.Label($"🎛️ Cut Height Adjuster: {clippingController.clipHeight:F2} [{cutStatus}]", labelStyle);
                float newH = GUILayout.HorizontalSlider(clippingController.clipHeight, -0.45f, 0.45f, GUILayout.Height(22));
                if (newH != clippingController.clipHeight)
                {
                    clippingController.SetClipHeight(newH);
                }

                GUILayout.Space(2);
                GUILayout.Label("Clipping Orientation:", labelStyle);
                GUILayout.BeginHorizontal();

                bool isAxial = clippingController.currentOrientation == CranialClippingController.ClipOrientation.AxialHorizontal;
                GUI.backgroundColor = isAxial ? Color.cyan : oldBg;
                if (GUILayout.Button("Axial (Top)", smallButtonStyle)) clippingController.SetOrientation(CranialClippingController.ClipOrientation.AxialHorizontal);

                bool isSagittal = clippingController.currentOrientation == CranialClippingController.ClipOrientation.SagittalMidline;
                GUI.backgroundColor = isSagittal ? Color.cyan : oldBg;
                if (GUILayout.Button("Sagittal (Side)", smallButtonStyle)) clippingController.SetOrientation(CranialClippingController.ClipOrientation.SagittalMidline);

                bool isCoronal = clippingController.currentOrientation == CranialClippingController.ClipOrientation.CoronalFrontal;
                GUI.backgroundColor = isCoronal ? Color.cyan : oldBg;
                if (GUILayout.Button("Coronal (Front)", smallButtonStyle)) clippingController.SetOrientation(CranialClippingController.ClipOrientation.CoronalFrontal);

                GUI.backgroundColor = oldBg;
                GUILayout.EndHorizontal();
            }

            // ── 3D Specular Surface Lighting Button ──
            GUILayout.Space(12);
            GUILayout.Label("--- 3D Surface Lighting ---", headerStyle);
            bool lighting = volObj.GetLightingEnabled();
            GUI.backgroundColor = lighting ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
            if (GUILayout.Button(lighting ? "💡 3D Specular Surface Lighting (L): ON" : "💡 3D Specular Surface Lighting (L): OFF", buttonStyle))
            {
                volObj.SetLightingEnabled(!lighting);
                Debug.Log($"3D Surface Lighting switched: {(!lighting ? "ON" : "OFF")}");
            }
            GUI.backgroundColor = oldBg;

            GUILayout.Space(10);
            GUILayout.EndScrollView();
        }

        private void DrawTrajectoryWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 1000, 30));
            Color oldBg = GUI.backgroundColor;

            GUILayout.Space(6);

            // 1. Interactive Placement & Raycasting Mode
            GUILayout.Label("--- 1. Interactive Trajectory Placement ---", headerStyle);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = trajPlanner.isPlacingEntry ? Color.yellow : oldBg;
            if (GUILayout.Button(trajPlanner.isPlacingEntry ? "📍 CLICK SKULL TO DROP" : "📍 Set Entry (Click Skull)", smallButtonStyle))
            {
                trajPlanner.StartPlacingEntry();
            }
            GUI.backgroundColor = trajPlanner.isPlacingTarget ? Color.cyan : oldBg;
            if (GUILayout.Button(trajPlanner.isPlacingTarget ? "🎯 CLICK TO DROP" : "🎯 Set Target (Click)", smallButtonStyle))
            {
                trajPlanner.StartPlacingTarget();
            }
            GUI.backgroundColor = oldBg;
            GUILayout.EndHorizontal();

            if (trajPlanner.isPlacingEntry)
            {
                GUI.backgroundColor = new Color(1f, 0.45f, 0.0f);
                GUILayout.Box("👉 CLICK ANYWHERE ON THE 3D SKULL TO SET ENTRY BURR HOLE", badgeStyle, GUILayout.Height(28));
                GUI.backgroundColor = oldBg;
            }
            else if (trajPlanner.isPlacingTarget)
            {
                GUI.backgroundColor = new Color(0.0f, 0.85f, 0.95f);
                GUILayout.Box("👉 CLICK ANYWHERE TO PLACE INTERNAL VENTRICLE TARGET", badgeStyle, GUILayout.Height(28));
                GUI.backgroundColor = oldBg;
            }

            GUILayout.Space(6);

            // 2. Approach Template Presets
            GUILayout.Label("--- 2. Clinical Approach Templates ---", headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kocher's Point", smallButtonStyle)) trajPlanner.SetApproach(TrajectoryApproach.KochersFrontal);
            if (GUILayout.Button("Dandy's Point", smallButtonStyle)) trajPlanner.SetApproach(TrajectoryApproach.OccipitalDandy);
            if (GUILayout.Button("Steep / Hazard", smallButtonStyle)) trajPlanner.SetApproach(TrajectoryApproach.SuboptimalSteep);
            GUILayout.EndHorizontal();

            if (trajPlanner.hasTrajectoryBeenPlaced)
            {
                GUILayout.Space(4);
                if (GUILayout.Button("🗑️ Clear / Hide Trajectory", smallButtonStyle))
                {
                    trajPlanner.ClearTrajectory();
                }

                GUILayout.Space(6);

                // 3. Fine-Tuning Coordinate Sliders
                GUILayout.Label("--- 3. Entry Point (Burr Hole on Skull) ---", headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Entry X: {trajPlanner.localEntryPoint.x:F2}", labelStyle, GUILayout.Width(100));
                float newEx = GUILayout.HorizontalSlider(trajPlanner.localEntryPoint.x, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Entry Y: {trajPlanner.localEntryPoint.y:F2}", labelStyle, GUILayout.Width(100));
                float newEy = GUILayout.HorizontalSlider(trajPlanner.localEntryPoint.y, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Entry Z: {trajPlanner.localEntryPoint.z:F2}", labelStyle, GUILayout.Width(100));
                float newEz = GUILayout.HorizontalSlider(trajPlanner.localEntryPoint.z, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                GUILayout.Space(4);
                GUILayout.Label("--- 4. Target Point (Internal Ventricle Target) ---", headerStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Target X: {trajPlanner.localTargetPoint.x:F2}", labelStyle, GUILayout.Width(100));
                float newTx = GUILayout.HorizontalSlider(trajPlanner.localTargetPoint.x, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Target Y: {trajPlanner.localTargetPoint.y:F2}", labelStyle, GUILayout.Width(100));
                float newTy = GUILayout.HorizontalSlider(trajPlanner.localTargetPoint.y, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Target Z: {trajPlanner.localTargetPoint.z:F2}", labelStyle, GUILayout.Width(100));
                float newTz = GUILayout.HorizontalSlider(trajPlanner.localTargetPoint.z, -0.45f, 0.45f);
                GUILayout.EndHorizontal();

                if (newEx != trajPlanner.localEntryPoint.x || newEy != trajPlanner.localEntryPoint.y || newEz != trajPlanner.localEntryPoint.z ||
                    newTx != trajPlanner.localTargetPoint.x || newTy != trajPlanner.localTargetPoint.y || newTz != trajPlanner.localTargetPoint.z)
                {
                    trajPlanner.localEntryPoint = new Vector3(newEx, newEy, newEz);
                    trajPlanner.localTargetPoint = new Vector3(newTx, newTy, newTz);
                    trajPlanner.currentApproach = TrajectoryApproach.CustomUserDefined;
                    trajPlanner.RecalculateTrajectoryMetrics();
                }

                GUILayout.Space(8);

                // 4. Safety Badge
                Color badgeCol = (trajPlanner.compositeRiskScore >= 80f) ? new Color(0.1f, 0.6f, 0.2f) :
                                 (trajPlanner.compositeRiskScore >= 55f) ? new Color(0.7f, 0.6f, 0.1f) :
                                                                          new Color(0.8f, 0.2f, 0.2f);
                GUI.backgroundColor = badgeCol;
                GUILayout.Box($"SAFETY RATING: {trajPlanner.safetyGrade} ({trajPlanner.compositeRiskScore:F0}%)", badgeStyle, GUILayout.Height(30));
                GUI.backgroundColor = oldBg;

                GUILayout.Space(6);

                // 5. Clinical Telemetry Metrics
                GUILayout.Label($"• Total Trajectory Length:  {trajPlanner.totalDistanceMm:F1} mm", labelStyle);
                GUILayout.Label($"• Cranial Bone Thickness:    {trajPlanner.skullBoneThicknessMm:F1} mm  (Drill Stopper)", labelStyle);
                GUILayout.Label($"• Brain Penetration Depth:  {trajPlanner.brainPenetrationDepthMm:F1} mm", labelStyle);
                GUILayout.Label($"• Cranial Entry Angle:       {trajPlanner.entryAngleDegrees:F1}° (Surface Normal)", labelStyle);
                GUILayout.Label($"• Target Status:             {(trajPlanner.targetReached ? "✅ VENTRICLE REACHED" : "⚠️ IN SOFT TISSUE")}", labelStyle);

                GUILayout.Space(8);

                // 6. Live Virtual Needle Simulation
                GUILayout.Label("--- Virtual Needle Insertion Simulator ---", headerStyle);
                GUILayout.Label($"Advance: {(trajPlanner.needleProgress * 100f):F0}%  |  Remaining: {(trajPlanner.totalDistanceMm * (1f - trajPlanner.needleProgress)):F1} mm", labelStyle);
                trajPlanner.needleProgress = GUILayout.HorizontalSlider(trajPlanner.needleProgress, 0.0f, 1.0f, GUILayout.Height(22));

                GUILayout.Space(3);
                GUI.backgroundColor = new Color(0.15f, 0.25f, 0.45f);
                GUILayout.Box($"TISSUE: {trajPlanner.currentNeedleTissue}", badgeStyle, GUILayout.Height(26));
                GUI.backgroundColor = oldBg;

                GUILayout.Space(8);

                // 7. 1D Density Profile Graph
                GUILayout.Label("--- 1D Density Profile (Entry ➔ Target) ---", headerStyle);
                UpdateGraphTexture();
                Rect graphRect = GUILayoutUtility.GetRect(GRAPH_W * 2f, GRAPH_H * 1.5f);
                GUI.DrawTexture(graphRect, graphTexture, ScaleMode.StretchToFill);

                GUILayout.Label("Red: Bone (>400 HU)  |  Yellow: Brain  |  Cyan: Ventricle CSF", labelStyle);
            }
            else
            {
                GUILayout.Space(20);
                GUI.backgroundColor = new Color(0.1f, 0.35f, 0.5f);
                GUILayout.Box("ℹ️ TRAJECTORY STANDBY\nClick 'Kocher's Point' or 'Set Entry' to plot trajectory path.", badgeStyle, GUILayout.Height(55));
                GUI.backgroundColor = oldBg;
            }
        }

        private void DrawMedivisWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 1000, 30));

            GUILayout.Space(6);

            if (medivisViewer != null && volObj != null && volObj.dataset != null && trajPlanner != null)
            {
                medivisViewer.UpdateMedivisViews(volObj.dataset, trajPlanner.localEntryPoint, trajPlanner.localTargetPoint, trajPlanner.needleProgress);

                GUILayout.BeginHorizontal();

                // 1. Axial 2D Slice Box (Tip Z)
                GUILayout.BeginVertical(GUILayout.Width(135));
                GUILayout.Label("Tip Z (Axial CT)", labelStyle);
                if (medivisViewer.axialTexture != null)
                {
                    Rect axRect = GUILayoutUtility.GetRect(130, 130);
                    GUI.DrawTexture(axRect, medivisViewer.axialTexture, ScaleMode.StretchToFill);
                }
                GUILayout.Label("Cyan: Path\nGreen: Ventricle", labelStyle);
                GUILayout.EndVertical();

                // 2. Sagittal 2D Slice Box (Tip X)
                GUILayout.BeginVertical(GUILayout.Width(135));
                GUILayout.Label("Tip X (Sagittal CT)", labelStyle);
                if (medivisViewer.sagittalTexture != null)
                {
                    Rect sagRect = GUILayoutUtility.GetRect(130, 130);
                    GUI.DrawTexture(sagRect, medivisViewer.sagittalTexture, ScaleMode.StretchToFill);
                }
                GUILayout.Label("Profile via\nKocher's path", labelStyle);
                GUILayout.EndVertical();

                // 3. Medivis Green Bullseye Guidance Reticle
                GUILayout.BeginVertical(GUILayout.Width(130));
                GUILayout.Label("Bullseye Reticle", labelStyle);
                if (medivisViewer.bullseyeTexture != null)
                {
                    Rect retRect = GUILayoutUtility.GetRect(96, 96);
                    GUI.DrawTexture(retRect, medivisViewer.bullseyeTexture, ScaleMode.ScaleToFit);
                }
                float remainingMm = trajPlanner.totalDistanceMm * (1f - trajPlanner.needleProgress);
                GUILayout.Label(remainingMm < 3f ? "🟢 ON TARGET" : "🟡 ALIGNING", labelStyle);
                GUILayout.Label($"Offset: {remainingMm:F1} mm", labelStyle);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }
        }

        private void UpdateGraphTexture()
        {
            if (graphTexture == null || trajPlanner == null) return;

            Color bgCol = new Color(0.08f, 0.08f, 0.14f, 0.95f);
            for (int i = 0; i < graphPixels.Length; i++)
            {
                graphPixels[i] = bgCol;
            }

            int sampleCount = TrajectoryPlanner.PROFILE_SAMPLES;
            for (int x = 0; x < GRAPH_W; x++)
            {
                float t = (float)x / (GRAPH_W - 1);
                int sampleIdx = Mathf.Clamp((int)(t * (sampleCount - 1)), 0, sampleCount - 1);
                float normDensity = trajPlanner.densityProfile[sampleIdx];

                int peakY = Mathf.Clamp((int)(normDensity * (GRAPH_H - 8)) + 3, 3, GRAPH_H - 2);

                Color barCol = (normDensity >= 0.28f) ? new Color(1f, 0.25f, 0.25f) :
                               (normDensity >= 0.06f) ? new Color(1f, 0.9f, 0.2f) :
                                                        new Color(0.1f, 0.9f, 1f);

                for (int y = 0; y <= peakY; y++)
                {
                    graphPixels[x + y * GRAPH_W] = barCol;
                }
            }

            int needleX = Mathf.Clamp((int)(trajPlanner.needleProgress * (GRAPH_W - 1)), 0, GRAPH_W - 1);
            for (int y = 0; y < GRAPH_H; y++)
            {
                graphPixels[needleX + y * GRAPH_W] = Color.white;
            }

            graphTexture.SetPixels(graphPixels);
            graphTexture.Apply();
        }

        private void OnDestroy()
        {
            if (graphTexture != null)
            {
                Destroy(graphTexture);
            }
        }
    }
}
