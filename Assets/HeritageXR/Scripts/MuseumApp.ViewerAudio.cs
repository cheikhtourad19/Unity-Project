using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeritageXR
{
    public partial class MuseumApp
    {
        readonly List<(Button button, string kind)> viewerAudioChoices = new();

        void ViewerAudio()
        {
            player.Select(selected, localization.Language, AudioKind);
            // Outside AR, a stable 2D voice accompanies the RenderTexture model.
            // The preview camera lives in a separate render space; it is not an AR listener.
            player.SetSpatial(false);
            var panel = new VisualElement { name = "viewer-audio" }; panel.AddToClassList("viewer-audio");
            CreateSubtitles(panel, false);
            var controls = new VisualElement(); controls.AddToClassList("viewer-audio-controls");
            audioPlay = Action(T("audio.play"), () => { player.Toggle(); RefreshAudioControls(); }, "primary", "viewer-audio-play");
            controls.Add(audioPlay); controls.Add(SubtitleControl()); panel.Add(controls);

            var options = new Foldout { text = T("viewer.audio.options"), value = false, name = "viewer-audio-options" };
            options.AddToClassList("viewer-audio-options");
            var details = new ScrollView(ScrollViewMode.Vertical); details.AddToClassList("viewer-audio-details"); options.Add(details);
            var choices = new VisualElement(); choices.AddToClassList("audio-choices");
            ViewerAudioChoice(choices, "narration", "audio.narration"); ViewerAudioChoice(choices, "audio_description", "audio.description"); details.Add(choices);
            audioStatus = Text("", "small"); details.Add(audioStatus);
            audioTime = Text("", "audio-time"); audioTime.languageDirection = LanguageDirection.LTR; details.Add(audioTime);
            audioSeek = new Slider(0, Mathf.Max(.1f, player.Duration)) { name = "viewer-audio-seek", tooltip = T("audio.seek") };
            audioSeek.AddToClassList("audio-seek"); audioSeek.languageDirection = LanguageDirection.LTR;
            audioSeek.RegisterValueChangedCallback(e => player.Seek(e.newValue));
            audioSeek.RegisterCallback<PointerDownEvent>(_ => seeking = true, TrickleDown.TrickleDown);
            audioSeek.RegisterCallback<PointerUpEvent>(_ => seeking = false, TrickleDown.TrickleDown);
            audioSeek.RegisterCallback<PointerCancelEvent>(_ => seeking = false, TrickleDown.TrickleDown);
            audioSeek.RegisterCallback<PointerCaptureOutEvent>(_ => seeking = false, TrickleDown.TrickleDown); details.Add(audioSeek);
            details.Add(Action(T("audio.restart"), () => { player.Restart(); RefreshAudioControls(); }, "secondary", "viewer-audio-restart"));
            if (PlayerPrefs.GetInt("heritage.transcript", 1) == 1)
            {
                details.Add(Words("audio.transcript", "section-title"));
                audioTranscript = Text(player.Track?.transcript ?? ""); audioTranscript.AddToClassList("audio-transcript"); details.Add(audioTranscript);
            }
            panel.Add(options); body.Add(panel); RefreshAudioControls();
        }
        void ViewerAudioChoice(VisualElement parent, string kind, string key)
        {
            var choice = Action(T(key), () =>
            {
                if (AudioKind == kind) return;
                PlayerPrefs.SetInt("heritage.audioDescription", kind == "audio_description" ? 1 : 0); PlayerPrefs.Save();
                player.Select(selected, localization.Language, AudioKind); player.SetSpatial(false);
                foreach (var entry in viewerAudioChoices) entry.button.EnableInClassList("chosen", entry.kind == AudioKind);
                if (audioTranscript != null) audioTranscript.text = player.Track?.transcript ?? "";
                seeking = false; RefreshAudioControls();
            }, "audio-choice", "viewer-audio-kind-" + kind);
            choice.EnableInClassList("chosen", AudioKind == kind);
            viewerAudioChoices.Add((choice, kind)); parent.Add(choice);
        }
    }
}
