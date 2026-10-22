using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HeritageXR
{
    public enum ScanState { Idle, Permission, Starting, Searching, Found, Lost, Denied, Restricted, Unavailable, Failed }

    public class MuseumAR : MonoBehaviour
    {
        public MuseumCollection collection;
        public ARSession session;
        public GameObject cameraRig;
        public ARTrackedImageManager imageManager;
        public Camera fallbackCamera;
        public AudioListener fallbackListener, arListener;
        public event Action<ScanState, MuseumArtwork> Changed;
        public ScanState State { get; private set; } = ScanState.Idle;
        public MuseumArtwork CurrentArtwork { get; private set; }
        public Transform CurrentAnchor { get; private set; }
        readonly Dictionary<TrackableId, GameObject> models = new();
        Coroutine startup;
        bool scanning;
        string requestedArtwork;

        void Awake()
        {
            imageManager.trackablesChanged.AddListener(OnImagesChanged);
            StopScanning();
        }

        void OnDestroy() { if (imageManager != null) imageManager.trackablesChanged.RemoveListener(OnImagesChanged); }
        void SetState(ScanState state, MuseumArtwork artwork = null, Transform anchor = null)
        {
            State = state;
            CurrentArtwork = artwork;
            CurrentAnchor = anchor;
            Changed?.Invoke(state, artwork);
        }

        public void StartScanning(string artworkId = null)
        {
            StopScanning();
            requestedArtwork = artworkId;
            scanning = true;
            startup = StartCoroutine(StartSession());
        }

        IEnumerator StartSession()
        {
#if !UNITY_IOS || UNITY_EDITOR
            SetState(ScanState.Unavailable);
            yield break;
#else
            SetState(ScanState.Permission);
            Debug.Log($"[HeritageXR] Camera authorization before request: {CameraPermission.Status}");
            if (CameraPermission.Status == CameraAuthorization.NotDetermined)
            {
                CameraPermission.Request();
                // A pending system dialog is not a denial. Wait for the user's
                // answer without a timeout that would mislabel an unanswered prompt.
                while (scanning && CameraPermission.Status == CameraAuthorization.NotDetermined)
                    yield return null;
            }
            if (!scanning) yield break;
            Debug.Log($"[HeritageXR] Camera authorization after request: {CameraPermission.Status}");
            if (CameraPermission.Status != CameraAuthorization.Authorized)
            {
                SetState(CameraPermission.Status == CameraAuthorization.Restricted ? ScanState.Restricted : ScanState.Denied);
                yield break;
            }
            SetState(ScanState.Starting);
            yield return ARSession.CheckAvailability();
            if (!scanning) yield break;
            if (ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported)
            {
                SetState(ScanState.Unavailable);
                yield break;
            }
            cameraRig.SetActive(true);
            fallbackCamera.enabled = false;
            if (fallbackListener != null) fallbackListener.enabled = false;
            if (arListener != null) arListener.enabled = true;
            session.enabled = true;
            session.GetComponent<ARInputManager>().enabled = true;
            imageManager.enabled = true;
            float startedAt = Time.realtimeSinceStartup;
            while (scanning && ARSession.state != ARSessionState.SessionTracking)
            {
                if (Time.realtimeSinceStartup - startedAt > 25f)
                {
                    StopSessionObjects();
                    SetState(ScanState.Failed);
                    yield break;
                }
                yield return null;
            }
            if (scanning && CurrentArtwork == null) SetState(ScanState.Searching);
#endif
        }

        void OnApplicationFocus(bool focused)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!focused || !scanning) return;
            // Returning from Settings resumes the current scan after access is granted.
            if ((State == ScanState.Denied || State == ScanState.Restricted) && CameraPermission.Status == CameraAuthorization.Authorized)
                StartScanning(requestedArtwork);
            else if ((State == ScanState.Searching || State == ScanState.Found || State == ScanState.Lost) && CameraPermission.Status != CameraAuthorization.Authorized)
            {
                StopSessionObjects();
                SetState(CameraPermission.Status == CameraAuthorization.Restricted ? ScanState.Restricted : ScanState.Denied);
            }
#endif
        }

        public void StopScanning()
        {
            scanning = false;
            if (startup != null) StopCoroutine(startup);
            startup = null;
            StopSessionObjects();
            SetState(ScanState.Idle);
        }

        void StopSessionObjects()
        {
            imageManager.enabled = false;
            session.enabled = false;
            session.GetComponent<ARInputManager>().enabled = false;
            cameraRig.SetActive(false);
            fallbackCamera.enabled = true;
            if (arListener != null) arListener.enabled = false;
            if (fallbackListener != null) fallbackListener.enabled = true;
            foreach (var model in models.Values) if (model != null) Destroy(model);
            models.Clear();
        }

        void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            if (!scanning) return;
            foreach (var image in changes.added) UpdateImage(image);
            foreach (var image in changes.updated) UpdateImage(image);
            foreach (var removed in changes.removed)
            {
                if (models.Remove(removed.Key, out var model)) Destroy(model);
            }
            MuseumArtwork visible = null;
            Transform visibleAnchor = null;
            foreach (var image in imageManager.trackables)
            {
                if (image.trackingState == TrackingState.Tracking &&
                    (requestedArtwork == null || image.referenceImage.name == requestedArtwork))
                {
                    visible = collection.Find(image.referenceImage.name);
                    if (visible != null)
                    {
                        if (models.TryGetValue(image.trackableId, out var instance)) visibleAnchor = instance.transform;
                        break;
                    }
                }
            }
            if (visible != null) SetState(ScanState.Found, visible, visibleAnchor);
            else if (State == ScanState.Found || State == ScanState.Lost) SetState(ScanState.Lost);
        }

        void UpdateImage(ARTrackedImage image)
        {
            var artwork = collection.Find(image.referenceImage.name);
            if (artwork == null || (requestedArtwork != null && requestedArtwork != artwork.id)) return;
            bool visible = image.trackingState == TrackingState.Tracking;
            if (!models.TryGetValue(image.trackableId, out var model) && visible)
            {
                model = Instantiate(artwork.modelPrefab, image.transform);
                model.transform.localPosition = new Vector3(0, .012f, 0);
                model.transform.localRotation = Quaternion.identity;
                models.Add(image.trackableId, model);
            }
            if (model != null) model.SetActive(visible);
        }
    }
}
