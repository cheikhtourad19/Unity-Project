using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeritageXR
{
    [Serializable]
    public class MuseumSubtitleCue
    {
        public float startSeconds, endSeconds;
        public string text;
    }

    [Serializable]
    public class MuseumAudioTrack
    {
        public string id, language, kind, transcript, voiceId, modelId;
        public AudioClip clip;
        public List<MuseumSubtitleCue> subtitles = new();
        public string SubtitleAt(float seconds)
        {
            // Audio time is the only clock: seeking and pauses need no separate timer.
            int low = 0, high = subtitles.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                var cue = subtitles[mid];
                if (seconds < cue.startSeconds) high = mid - 1;
                else if (seconds >= cue.endSeconds) low = mid + 1;
                else return cue.text;
            }
            return null;
        }
    }

    [Serializable]
    public class MuseumArtwork
    {
        public string id, title, summary, description, museumObjectId;
        public string photoCredit, photoLicence, photoSource, modelLicence, modelSource;
        public string[] modelCredits, descriptionSources;
        public Texture2D photograph;
        public GameObject modelPrefab;
        public float targetWidthMeters = .12f;
        public List<MuseumAudioTrack> audio = new();
        public MuseumQuiz quiz;
        public MuseumAudioTrack Track(string language, string kind) => audio.Find(t => t.language == language && t.kind == kind);
    }

    public class MuseumCollection : ScriptableObject
    {
        public List<MuseumArtwork> artworks = new();
        public MuseumArtwork Find(string id) => artworks.Find(a => a.id == id);
    }
}
