using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Medivis-style Multi-Planar Reconstruction (MPR) 2D CT Slice & Bullseye Generator.
    /// Extracts real-time 2D Axial and Sagittal CT cross-section slices intersecting the surgical trajectory,
    /// and renders the Medivis surgical bullseye guidance reticle (inspired by Medivis Navigate Cranial).
    /// </summary>
    public class MedivisSliceViewer : MonoBehaviour
    {
        public const int SLICE_RES = 128;
        public const int RETICLE_RES = 96;

        public Texture2D axialTexture { get; private set; }
        public Texture2D sagittalTexture { get; private set; }
        public Texture2D bullseyeTexture { get; private set; }

        private Color[] axialPixels;
        private Color[] sagittalPixels;
        private Color[] bullseyePixels;

        private void Awake()
        {
            axialTexture = new Texture2D(SLICE_RES, SLICE_RES, TextureFormat.RGB24, false);
            axialTexture.filterMode = FilterMode.Bilinear;
            axialPixels = new Color[SLICE_RES * SLICE_RES];

            sagittalTexture = new Texture2D(SLICE_RES, SLICE_RES, TextureFormat.RGB24, false);
            sagittalTexture.filterMode = FilterMode.Bilinear;
            sagittalPixels = new Color[SLICE_RES * SLICE_RES];

            bullseyeTexture = new Texture2D(RETICLE_RES, RETICLE_RES, TextureFormat.RGBA32, false);
            bullseyeTexture.filterMode = FilterMode.Point;
            bullseyePixels = new Color[RETICLE_RES * RETICLE_RES];
        }

        /// <summary>
        /// Generates live 2D CT slices and surgical reticle matching the Medivis Navigate Cranial layout.
        /// </summary>
        public void UpdateMedivisViews(VolumeDataset dataset, Vector3 localEntry, Vector3 localTarget, float needleProgress)
        {
            if (dataset == null || axialTexture == null) return;

            Vector3 currentNeedle = Vector3.Lerp(localEntry, localTarget, needleProgress);

            // Convert to UVW coordinates [0, 1]
            Vector3 targetUVW = localTarget + Vector3.one * 0.5f;
            Vector3 entryUVW = localEntry + Vector3.one * 0.5f;
            Vector3 needleUVW = currentNeedle + Vector3.one * 0.5f;

            int targetX = Mathf.Clamp((int)(targetUVW.x * dataset.dimX), 0, dataset.dimX - 1);
            int targetY = Mathf.Clamp((int)(targetUVW.y * dataset.dimY), 0, dataset.dimY - 1);
            int targetZ = Mathf.Clamp((int)(targetUVW.z * dataset.dimZ), 0, dataset.dimZ - 1);

            int needleY = Mathf.Clamp((int)(needleUVW.y * dataset.dimY), 0, dataset.dimY - 1);
            int sliceY = Mathf.Clamp((int)Mathf.Lerp(targetY, needleY, 0.5f), 0, dataset.dimY - 1);

            // 1. Generate Axial 2D Slice (Horizontal plane XZ at slice height)
            for (int z = 0; z < SLICE_RES; z++)
            {
                int dataZ = Mathf.Clamp((int)((float)z / SLICE_RES * dataset.dimZ), 0, dataset.dimZ - 1);
                for (int x = 0; x < SLICE_RES; x++)
                {
                    int dataX = Mathf.Clamp((int)((float)x / SLICE_RES * dataset.dimX), 0, dataset.dimX - 1);
                    float raw = dataset.GetData(dataX, sliceY, dataZ);
                    float norm = Mathf.InverseLerp(dataset.GetMinDataValue(), dataset.GetMaxDataValue(), raw);

                    // Clinical Brain CT Windowing: Enhance brain soft tissue & skull bone contrast
                    float windowed = ApplyBrainCTWindow(norm);
                    axialPixels[x + z * SLICE_RES] = new Color(windowed, windowed, windowed);
                }
            }

            // Draw Trajectory line & Target circle on Axial Slice
            int axEx = Mathf.Clamp((int)(entryUVW.x * SLICE_RES), 0, SLICE_RES - 1);
            int axEz = Mathf.Clamp((int)(entryUVW.z * SLICE_RES), 0, SLICE_RES - 1);
            int axTx = Mathf.Clamp((int)(targetUVW.x * SLICE_RES), 0, SLICE_RES - 1);
            int axTz = Mathf.Clamp((int)(targetUVW.z * SLICE_RES), 0, SLICE_RES - 1);

            DrawBresenhamLine(axialPixels, SLICE_RES, axEx, axEz, axTx, axTz, new Color(0.2f, 0.95f, 1f)); // Cyan Line
            DrawCircleMarker(axialPixels, SLICE_RES, axTx, axTz, 5, new Color(0.1f, 1f, 0.4f));             // Green Target

            axialTexture.SetPixels(axialPixels);
            axialTexture.Apply();

            // 2. Generate Sagittal 2D Slice (Side profile YZ at target lateral position)
            int sliceX = Mathf.Clamp(targetX, 0, dataset.dimX - 1);
            for (int y = 0; y < SLICE_RES; y++)
            {
                int dataY = Mathf.Clamp((int)((float)y / SLICE_RES * dataset.dimY), 0, dataset.dimY - 1);
                for (int z = 0; z < SLICE_RES; z++)
                {
                    int dataZ = Mathf.Clamp((int)((float)z / SLICE_RES * dataset.dimZ), 0, dataset.dimZ - 1);
                    float raw = dataset.GetData(sliceX, dataY, dataZ);
                    float norm = Mathf.InverseLerp(dataset.GetMinDataValue(), dataset.GetMaxDataValue(), raw);

                    float windowed = ApplyBrainCTWindow(norm);
                    sagittalPixels[z + y * SLICE_RES] = new Color(windowed, windowed, windowed);
                }
            }

            int sagEz = Mathf.Clamp((int)(entryUVW.z * SLICE_RES), 0, SLICE_RES - 1);
            int sagEy = Mathf.Clamp((int)(entryUVW.y * SLICE_RES), 0, SLICE_RES - 1);
            int sagTz = Mathf.Clamp((int)(targetUVW.z * SLICE_RES), 0, SLICE_RES - 1);
            int sagTy = Mathf.Clamp((int)(targetUVW.y * SLICE_RES), 0, SLICE_RES - 1);

            DrawBresenhamLine(sagittalPixels, SLICE_RES, sagEz, sagEy, sagTz, sagTy, new Color(0.2f, 0.95f, 1f));
            DrawCircleMarker(sagittalPixels, SLICE_RES, sagTz, sagTy, 5, new Color(0.1f, 1f, 0.4f));

            sagittalTexture.SetPixels(sagittalPixels);
            sagittalTexture.Apply();

            // 3. Render Medivis Surgical Bullseye Reticle
            UpdateBullseyeReticle(needleProgress);
        }

        private float ApplyBrainCTWindow(float norm)
        {
            // Contrast stretch: Brain tissue (~0.05 to 0.25) -> Expanded grayscale, Bone (>0.28) -> Pure white
            if (norm > 0.28f) return 1.0f; // Bone
            if (norm < 0.04f) return 0.05f; // Air / Ventricle CSF
            return Mathf.Clamp01((norm - 0.04f) / 0.24f * 0.75f + 0.15f); // Brain parenchyma
        }

        private void UpdateBullseyeReticle(float needleProgress)
        {
            Color darkBg = new Color(0.04f, 0.08f, 0.06f, 0.95f);
            Color greenRing = new Color(0.15f, 0.9f, 0.35f, 0.95f);
            Color crosshairCol = new Color(0.2f, 0.7f, 0.35f, 0.6f);

            for (int i = 0; i < bullseyePixels.Length; i++)
            {
                bullseyePixels[i] = darkBg;
            }

            int center = RETICLE_RES / 2;

            // Draw concentric target circles
            int r1 = 38;
            int r2 = 24;
            int r3 = 10;

            for (int y = 0; y < RETICLE_RES; y++)
            {
                for (int x = 0; x < RETICLE_RES; x++)
                {
                    int dx = x - center;
                    int dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (Mathf.Abs(dist - r1) < 1.2f || Mathf.Abs(dist - r2) < 1.2f || Mathf.Abs(dist - r3) < 1.2f)
                    {
                        bullseyePixels[x + y * RETICLE_RES] = greenRing;
                    }
                    else if ((x == center && Mathf.Abs(dy) < r1) || (y == center && Mathf.Abs(dx) < r1))
                    {
                        bullseyePixels[x + y * RETICLE_RES] = crosshairCol;
                    }
                }
            }

            // Draw needle alignment dot
            // When progress approaches 1.0, dot centers perfectly on (0,0)
            float deviationFactor = (1f - needleProgress) * 16f;
            int dotX = Mathf.Clamp(center + (int)(Mathf.Sin(Time.time * 2f) * deviationFactor * 0.3f), 4, RETICLE_RES - 5);
            int dotY = Mathf.Clamp(center + (int)(Mathf.Cos(Time.time * 2f) * deviationFactor * 0.3f), 4, RETICLE_RES - 5);

            Color dotCol = (needleProgress >= 0.9f) ? Color.green : Color.yellow;
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    bullseyePixels[(dotX + dx) + (dotY + dy) * RETICLE_RES] = dotCol;
                }
            }

            bullseyeTexture.SetPixels(bullseyePixels);
            bullseyeTexture.Apply();
        }

        private void DrawBresenhamLine(Color[] pixels, int res, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 >= 0 && x0 < res && y0 >= 0 && y0 < res)
                {
                    pixels[x0 + y0 * res] = color;
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawCircleMarker(Color[] pixels, int res, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (Mathf.Abs(d - radius) < 1.2f || d <= 1.5f)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < res && py >= 0 && py < res)
                        {
                            pixels[px + py * res] = color;
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (axialTexture != null) Destroy(axialTexture);
            if (sagittalTexture != null) Destroy(sagittalTexture);
            if (bullseyeTexture != null) Destroy(bullseyeTexture);
        }
    }
}
