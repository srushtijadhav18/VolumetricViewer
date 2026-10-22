using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Interactive 3D Cranial Trepanation / Skull Clipping Plane Controller.
    /// Slices open the 3D CT skull volume in real-time on the GPU, exposing internal brain anatomy
    /// and allowing visual inspection of deep trajectory paths (inspired by Medivis Navigate Cranial).
    /// </summary>
    public class CranialClippingController : MonoBehaviour
    {
        [Header("State")]
        public bool isClippingEnabled = false;

        [Range(-0.45f, 0.45f)]
        public float clipHeight = 0.22f; // Initial cut above eye level exposing brain cavity

        public enum ClipOrientation
        {
            AxialHorizontal, // Standard craniotomy (slices off top of skull)
            SagittalMidline, // Midline side-profile cut
            CoronalFrontal   // Frontal face-to-back cut
        }

        public ClipOrientation currentOrientation = ClipOrientation.AxialHorizontal;

        private VolumeRenderedObject volObj;
        private GameObject planeObj;
        private CrossSectionPlane crossSectionPlane;

        private void Start()
        {
            volObj = GetComponent<VolumeRenderedObject>();
            if (volObj == null)
            {
                volObj = FindObjectOfType<VolumeRenderedObject>();
            }
        }

        public void ToggleClipping()
        {
            SetClippingEnabled(!isClippingEnabled);
        }

        public void SetClippingEnabled(bool enable)
        {
            isClippingEnabled = enable;

            if (isClippingEnabled)
            {
                EnsurePlaneInstance();
                if (planeObj != null) planeObj.SetActive(true);
                UpdatePlaneTransform();
            }
            else
            {
                if (planeObj != null)
                {
                    planeObj.SetActive(false);
                }
            }
        }

        public void SetClipHeight(float height)
        {
            clipHeight = Mathf.Clamp(height, -0.45f, 0.45f);
            if (isClippingEnabled)
            {
                UpdatePlaneTransform();
            }
        }

        public void SetOrientation(ClipOrientation orientation)
        {
            currentOrientation = orientation;
            if (isClippingEnabled)
            {
                UpdatePlaneTransform();
            }
        }

        private void EnsurePlaneInstance()
        {
            if (volObj == null)
            {
                volObj = GetComponent<VolumeRenderedObject>();
                if (volObj == null) volObj = FindObjectOfType<VolumeRenderedObject>();
            }

            if (planeObj == null && volObj != null)
            {
                GameObject prefab = Resources.Load<GameObject>("CrossSectionPlane");
                if (prefab != null)
                {
                    planeObj = GameObject.Instantiate(prefab);
                    planeObj.name = "TrajectAR_CranialCutPlane";
                    planeObj.transform.SetParent(volObj.transform);

                    crossSectionPlane = planeObj.GetComponent<CrossSectionPlane>();
                    if (crossSectionPlane != null)
                    {
                        crossSectionPlane.SetTargetObject(volObj);
                    }

                    // Hide the visible quad mesh so only the clipping effect is visible
                    MeshRenderer quadRend = planeObj.GetComponent<MeshRenderer>();
                    if (quadRend != null)
                    {
                        quadRend.enabled = false;
                    }
                }
            }
        }

        private void UpdatePlaneTransform()
        {
            if (planeObj == null || volObj == null) return;

            Vector3 localPos = Vector3.zero;
            Quaternion localRot = Quaternion.identity;

            switch (currentOrientation)
            {
                case ClipOrientation.AxialHorizontal:
                    // Cuts off top of skull along Y axis
                    localPos = new Vector3(0f, clipHeight, 0f);
                    localRot = Quaternion.Euler(270f, 0f, 0f);
                    break;

                case ClipOrientation.SagittalMidline:
                    // Cuts along sagittal plane (X axis)
                    localPos = new Vector3(clipHeight, 0f, 0f);
                    localRot = Quaternion.Euler(0f, 90f, 0f);
                    break;

                case ClipOrientation.CoronalFrontal:
                    // Cuts along coronal plane (Z axis)
                    localPos = new Vector3(0f, 0f, clipHeight);
                    localRot = Quaternion.Euler(0f, 0f, 0f);
                    break;
            }

            planeObj.transform.localPosition = localPos;
            planeObj.transform.localRotation = localRot;
            planeObj.transform.localScale = Vector3.one * 1.5f;
        }

        private void OnDestroy()
        {
            if (planeObj != null)
            {
                Destroy(planeObj);
            }
        }
    }
}
