using UnityEngine;

namespace HeritageXR
{
    public class ArtworkViewer : MonoBehaviour
    {
        public Camera previewCamera;
        public RenderTexture texture;
        GameObject model;
        Transform pivot;
        Quaternion rotation = Quaternion.identity;
        Vector3 cameraHome, pan;
        float homeFieldOfView, focusDistance, zoom = 1f;
        public bool IsVisible => model != null;

        public void Show(MuseumArtwork artwork)
        {
            Hide();
            // A RenderTexture keeps its last frame when its camera is disabled.
            // Clear that frame before showing another object or revisiting the viewer.
            if (texture != null)
            {
                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                GL.Clear(true, true, previewCamera.backgroundColor);
                RenderTexture.active = previous;
            }
            model = Instantiate(artwork.modelPrefab, transform);
            SetLayer(model.transform, 30);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            pivot = new GameObject("Model rotation center").transform;
            pivot.SetParent(transform, false);
            pivot.position = bounds.center;
            model.transform.SetParent(pivot, true);
            rotation = Quaternion.identity;
            pan = Vector3.zero;
            zoom = 1;
            cameraHome = previewCamera.transform.localPosition;
            homeFieldOfView = previewCamera.fieldOfView;
            focusDistance = Vector3.Distance(previewCamera.transform.position, bounds.center);
            previewCamera.nearClipPlane = .01f;
            previewCamera.gameObject.SetActive(true);
            RefreshPose();
        }
        public void Rotate(Vector2 delta)
        {
            // Rotate about screen axes so the visible surface follows the finger,
            // including after a full vertical turn. Quaternions avoid a pitch lock.
            var up = transform.InverseTransformDirection(previewCamera.transform.up);
            var right = transform.InverseTransformDirection(previewCamera.transform.right);
            rotation = Quaternion.AngleAxis(-delta.x * .45f, up)
                     * Quaternion.AngleAxis(-delta.y * .45f, right) * rotation;
            RefreshPose();
        }
        public void Zoom(float factor)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0) return;
            zoom = Mathf.Clamp(zoom * factor, .4f, 24f);
            RefreshPose();
        }
        public void Pan(Vector2 delta, float viewportSize)
        {
            if (model == null || viewportSize <= 0) return;
            float unitsPerPixel = 2f * focusDistance * Mathf.Tan(previewCamera.fieldOfView * Mathf.Deg2Rad * .5f) / viewportSize;
            var movement = (-previewCamera.transform.right * delta.x + previewCamera.transform.up * delta.y) * unitsPerPixel;
            pan += transform.InverseTransformDirection(movement);
            RefreshPose();
        }
        public void ResetPose() { rotation = Quaternion.identity; pan = Vector3.zero; zoom = 1; RefreshPose(); }
        void RefreshPose()
        {
            if (model == null) return;
            pivot.localRotation = rotation;
            // Optical zoom keeps the camera outside the mesh, instead of growing
            // the mesh into the near plane and clipping its inscriptions.
            previewCamera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(homeFieldOfView * Mathf.Deg2Rad * .5f) / zoom) * Mathf.Rad2Deg;
            previewCamera.transform.localPosition = cameraHome + pan;
        }
        public void Hide()
        {
            if (model != null) { model.SetActive(false); Destroy(model); model = null; }
            if (pivot != null) { Destroy(pivot.gameObject); pivot = null; }
            if (previewCamera != null)
            {
                if (homeFieldOfView > 0) { previewCamera.fieldOfView = homeFieldOfView; previewCamera.transform.localPosition = cameraHome; }
                previewCamera.gameObject.SetActive(false);
            }
        }
        static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform child in t) SetLayer(child, layer); }
    }
}
