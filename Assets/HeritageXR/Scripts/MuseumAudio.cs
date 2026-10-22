using UnityEngine;

namespace HeritageXR
{
    // One source for the visit, shared by the model viewer and the AR player.
    // No automatic playback: tracking recovery resumes only an interrupted clip.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MuseumAudio : MonoBehaviour
    {
        AudioSource source;
        Transform anchor;
        bool intendedPlaying, trackingPaused, suspended, started, completed;
        float resumeTime;
        public MuseumArtwork Artwork { get; private set; }
        public MuseumAudioTrack Track { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public bool WantsPlayback => intendedPlaying;
        public bool TrackingPaused => trackingPaused && intendedPlaying;
        public bool Available => Track?.clip != null;
        public float Duration => Available ? Track.clip.length : 0;
        public float Position => completed ? Duration : source != null && Available ? source.time : 0;
        public bool HasPlaybackPosition => Available && !completed && (started || Position > 0);
        public string Subtitle => HasPlaybackPosition ? Track.SubtitleAt(Position) : null;
        public float Volume
        {
            get => PlayerPrefs.GetFloat("heritage.audioVolume", .85f);
            set
            {
                EnsureSource(); source.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("heritage.audioVolume", source.volume); PlayerPrefs.Save();
            }
        }
        void Awake() => EnsureSource();
        void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false;
            source.volume = Volume; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = .5f; source.maxDistance = 5f;
            source.spatialBlend = 0;
        }
        public void Select(MuseumArtwork artwork, string language, string kind)
        {
            EnsureSource();
            var track = artwork?.Track(language, kind);
            if (Artwork == artwork && Track == track) return;
            Stop(); Artwork = artwork; Track = track; source.clip = track?.clip;
        }
        public void SetSpatial(bool spatial, Transform artworkAnchor = null)
        {
            EnsureSource(); source.spatialBlend = spatial ? 1 : 0;
            anchor = spatial ? artworkAnchor : null;
            if (!spatial) { trackingPaused = false; return; }
            if (anchor == null)
            {
                if (!trackingPaused) { resumeTime = Position; source.Pause(); }
                trackingPaused = true;
            }
            else if (trackingPaused)
            {
                trackingPaused = false;
                if (intendedPlaying && !suspended)
                {
                    source.time = resumeTime;
                    if (started) source.UnPause(); else { source.Play(); started = true; }
                }
            }
        }
        public void Toggle()
        {
            EnsureSource(); if (!Available || suspended) return;
            if (intendedPlaying) { intendedPlaying = false; source.Pause(); return; }
            intendedPlaying = true;
            if (trackingPaused) return;
            if (completed || source.time >= Duration - .05f) { source.time = 0; started = false; completed = false; }
            if (started) source.UnPause(); else { source.Play(); started = true; }
        }
        public void Restart()
        {
            Seek(0);
            if (intendedPlaying && !trackingPaused && !suspended) { source.Play(); started = true; }
        }
        public void Seek(float seconds)
        {
            EnsureSource(); if (!Available) return;
            completed = false;
            source.time = Mathf.Clamp(seconds, 0, Mathf.Max(0, Duration - .02f));
            resumeTime = source.time;
        }
        public void Stop()
        {
            EnsureSource(); source.Stop();
            intendedPlaying = false; trackingPaused = false; resumeTime = 0;
            started = false; completed = false;
            anchor = null; source.spatialBlend = 0;
        }
        void LateUpdate()
        {
            if (source == null) return;
            if (source.spatialBlend > 0 && anchor == null) SetSpatial(true);
            if (anchor != null) source.transform.position = anchor.position + Vector3.up * .1f;
            if (intendedPlaying && started && !trackingPaused && !suspended && source.clip.loadState == AudioDataLoadState.Loaded && !source.isPlaying)
            {
                intendedPlaying = false;
                started = false; completed = true;
            }
        }
        void OnApplicationPause(bool paused)
        {
            suspended = paused;
            if (paused && source != null) { intendedPlaying = false; source.Pause(); }
        }
        void OnDisable() { if (source != null) Stop(); }
    }
}
