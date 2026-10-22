using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HeritageXR
{
    public enum MuseumPage { Home, Collection, Detail, ScanGuide, Scanner, Viewer, Preferences, Credits, QuizIntro = 9, QuizQuestion, QuizFeedback, QuizResult, QuizProgress }

    [RequireComponent(typeof(UIDocument))]
    public partial class MuseumApp : MonoBehaviour
    {
        public MuseumCollection collection;
        public MuseumAR ar;
        public ArtworkViewer viewer;
        public MuseumAudio player;
        public StyleSheet theme;
        public MuseumPage CurrentPage { get; private set; }
        public string SelectedArtworkId => selected?.id;
        public VisualElement Root => root;
        MuseumArtwork selected;
        MuseumLocalization localization;
        VisualElement root, shell, pageHost, body, nav, curtain;
        Label headerSubtitle, scanHeading, scanHint, scanBadge;
        Label audioTime, audioTranscript, audioStatus;
        VisualElement subtitlePanel;
        Label subtitleText;
        Button subtitleToggle;
        Button audioPlay, scanPlay;
        Slider audioSeek;
        bool seeking;
        float nextAudioRefresh;
        string AudioKind => PlayerPrefs.GetInt("heritage.audioDescription", 0) == 1 ? "audio_description" : "narration";
        bool SubtitlesEnabled => PlayerPrefs.GetInt("heritage.subtitles", 1) == 1;
        Button backButton, settingsButton, foundButton, scanSettingsButton, scanRetryButton;
        MuseumIcon backIcon, settingsIcon;
        readonly Dictionary<MuseumPage, (Button button, Label label, MuseumIcon icon)> destinations = new();
        readonly Stack<(MuseumPage, string)> history = new();
        readonly Dictionary<(MuseumPage, string), Vector2> scrollPositions = new();
        (MuseumPage, string) ScrollKey => (CurrentPage, CurrentPage == MuseumPage.Detail || CurrentPage == MuseumPage.Credits || CurrentPage == MuseumPage.ScanGuide || CurrentPage == MuseumPage.Viewer || IsQuizPage(CurrentPage) ? selected?.id : null);
        readonly Dictionary<int, Vector2> pointers = new();
        ScrollView scroll;
        Coroutine reveal;
        int pageVersion;
        float pinchDistance;
        Vector2 pinchCenter;
        bool largeText, reduceMotion;
        int lastWidth, lastHeight;
        Rect lastSafeArea;
        static readonly Color Ink = new Color(.13f, .22f, .20f);

        string T(string key) => localization.Get(key);
        MuseumLocalization.ArtworkText Copy(MuseumArtwork artwork) => localization.Artwork(artwork);

        void OnEnable()
        {
            Application.targetFrameRate = 60;
            localization = new MuseumLocalization();
            quizProgress = new MuseumQuizProgress();
            if (player == null) player = GetComponent<MuseumAudio>();
            if (player == null) player = gameObject.AddComponent<MuseumAudio>();
            MuseumFonts.Initialize();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (theme != null && !root.styleSheets.Contains(theme)) root.styleSheets.Add(theme);
            root.AddToClassList("app");
            var paper = new Color(246f / 255f, 243f / 255f, 238f / 255f);
            ar.fallbackCamera.backgroundColor = paper;
            viewer.previewCamera.backgroundColor = paper;
            largeText = PlayerPrefs.GetInt("heritage.largeText", 0) == 1;
            reduceMotion = PlayerPrefs.GetInt("heritage.reduceMotion", 0) == 1;
            root.EnableInClassList("large-text", largeText);
            root.EnableInClassList("reduce-motion", reduceMotion);
            shell = new VisualElement { name = "safe-area" }; shell.AddToClassList("shell"); root.Add(shell);
            CreateHeader();
            pageHost = new VisualElement { name = "page-host" }; pageHost.AddToClassList("page-host"); shell.Add(pageHost);
            curtain = new VisualElement { name = "page-curtain", pickingMode = PickingMode.Ignore }; curtain.AddToClassList("page-curtain"); pageHost.Add(curtain);
            CreateNavigation();
            history.Clear(); selected = null;
            ar.Changed += OnScanChanged;
            Show(MuseumPage.Home);
            ShowStartupSplash();
        }
        void OnDisable()
        {
            pageVersion++;
            if (reveal != null) StopCoroutine(reveal);
            ar.Changed -= OnScanChanged;
            ar.StopScanning();
            viewer.Hide();
            player?.Stop();
            startupSplash?.RemoveFromHierarchy();
            startupSplash = null;
            pointers.Clear();
        }
        void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            if (Time.unscaledTime >= nextAudioRefresh) { nextAudioRefresh = Time.unscaledTime + .05f; RefreshAudioControls(); }
        }
        void SaveScroll() { if (scroll != null) scrollPositions[ScrollKey] = scroll.scrollOffset; }
        public void Navigate(MuseumPage page, string artworkId = null)
        {
            if (page == CurrentPage && (artworkId == null || artworkId == selected?.id)) return;
            SaveScroll();
            history.Push((CurrentPage, selected?.id));
            if (artworkId != null) selected = collection.Find(artworkId);
            Show(page);
        }
        public void Back()
        {
            SaveScroll();
            if (history.Count == 0) { SwitchTab(MuseumPage.Home); return; }
            var entry = history.Pop();
            selected = collection.Find(entry.Item2);
            Show(entry.Item1);
        }
        void SwitchTab(MuseumPage page)
        {
            if (CurrentPage == page && selected == null) return;
            SaveScroll(); history.Clear(); selected = null; Show(page);
        }
        void ChangeLanguage(string language)
        {
            if (localization.Language == language) return;
            SaveScroll(); localization.SetLanguage(language); Show(CurrentPage);
        }
        void Show(MuseumPage page)
        {
            if ((page == MuseumPage.Detail || page == MuseumPage.Viewer || page == MuseumPage.Credits || IsQuizPage(page)) && selected == null)
                page = MuseumPage.Collection;
            if (IsQuizPage(page) && selected.quiz == null) page = MuseumPage.Detail;
            page = ResolveQuizPage(page);
            int version = ++pageVersion;
            if (reveal != null) StopCoroutine(reveal);
            curtain.style.display = DisplayStyle.Flex;
            curtain.style.opacity = 1;
            pageHost.pickingMode = PickingMode.Ignore;
            var previous = CurrentPage;
            CurrentPage = page;
            player.Stop();
            audioPlay = null; scanPlay = null; audioSeek = null; audioTime = null; audioTranscript = null; audioStatus = null; seeking = false;
            subtitlePanel = null; subtitleText = null; subtitleToggle = null;
            viewerAudioChoices.Clear();
            if (previous == MuseumPage.Scanner) ar.StopScanning();
            if (viewer.IsVisible) viewer.Hide();
            pointers.Clear(); pinchDistance = 0;
            root.EnableInClassList("camera-page", page == MuseumPage.Scanner);
            ApplyLanguage(); UpdateHeader(); UpdateNavigation();
            // Construct off-panel and replace just the page. The header and tabs
            // keep their geometry, font caches and input registrations.
            var oldBody = body;
            body = new VisualElement { name = "page-body" }; body.AddToClassList("body");
            body.SetEnabled(false);
            scroll = null;
            switch (page)
            {
                case MuseumPage.Home: Home(); break;
                case MuseumPage.Collection: Collection(); break;
                case MuseumPage.Detail: Detail(); break;
                case MuseumPage.ScanGuide: ScanGuide(); break;
                case MuseumPage.Scanner: Scanner(); break;
                case MuseumPage.Viewer: Viewer(); break;
                case MuseumPage.Preferences: Preferences(); break;
                case MuseumPage.Credits: Credits(); break;
                case MuseumPage.QuizIntro: QuizIntro(); break;
                case MuseumPage.QuizQuestion: QuizQuestion(); break;
                case MuseumPage.QuizFeedback: QuizFeedback(); break;
                case MuseumPage.QuizResult: QuizResult(); break;
                case MuseumPage.QuizProgress: QuizProgress(); break;
            }
            oldBody?.RemoveFromHierarchy();
            pageHost.Insert(0, body);
            ApplySafeArea();
            var pageScroll = scroll;
            var position = scrollPositions.TryGetValue(ScrollKey, out var saved) ? saved : Vector2.zero;
            reveal = StartCoroutine(RevealPage(version, body, pageScroll, position));
        }
        IEnumerator RevealPage(int version, VisualElement page, ScrollView pageScroll, Vector2 position)
        {
            // Capture the actual page, rather than the mutable global `scroll`.
            // No delayed callback from an old screen can move the next screen.
            yield return null;
            if (version != pageVersion) yield break;
            if (pageScroll != null) pageScroll.scrollOffset = position;
            // Let UI layout and the first camera render settle behind an opaque
            // curtain. Never crossfade the old page or reuse its 3D image.
            yield return null;
            if (version != pageVersion) yield break;
            pageHost.pickingMode = PickingMode.Position;
            page.SetEnabled(true);
            if (!reduceMotion)
            {
                float elapsed = 0;
                while (elapsed < .12f)
                {
                    if (version != pageVersion) yield break;
                    elapsed += Time.unscaledDeltaTime;
                    curtain.style.opacity = 1f - Mathf.SmoothStep(0, 1, elapsed / .12f);
                    yield return null;
                }
            }
            if (version == pageVersion) { curtain.style.display = DisplayStyle.None; reveal = null; }
        }
        void ApplyLanguage()
        {
            root.languageDirection = localization.IsArabic ? LanguageDirection.RTL : LanguageDirection.LTR;
            root.EnableInClassList("rtl", localization.IsArabic);
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(localization.IsArabic ? MuseumFonts.Arabic : MuseumFonts.Body);
        }
        void ApplySafeArea()
        {
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafeArea = Screen.safeArea;
            if (shell == null || Screen.width == 0) return;
            float scale = 430f / Screen.width;
            shell.style.paddingTop = Mathf.Max(12, (Screen.height - Screen.safeArea.yMax) * scale);
            shell.style.paddingBottom = Mathf.Max(12, Screen.safeArea.yMin * scale);
            shell.style.paddingLeft = Screen.safeArea.xMin * scale;
            shell.style.paddingRight = (Screen.width - Screen.safeArea.xMax) * scale;
        }
        Label Text(string value, string css = "paragraph")
        {
            var label = new Label(value); label.AddToClassList(css);
            if (!localization.IsArabic && (css == "hero-title" || css == "page-title" || css == "photo-title" || css == "viewer-title"))
                label.style.unityFontDefinition = FontDefinition.FromSDFFont(MuseumFonts.Display);
            return label;
        }
        Label Words(string key, string css = "paragraph") => Text(T(key), css);
        Button Action(string title, Action clicked, string css = "primary", string name = null)
        {
            var button = new Button(clicked) { text = title, name = name ?? title, tooltip = title };
            button.AddToClassList(css); return button;
        }
        ScrollView Content()
        {
            scroll = new ScrollView(ScrollViewMode.Vertical) { name = "page-scroll" };
            scroll.AddToClassList("content"); scroll.contentContainer.AddToClassList("page-content");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            body.Add(scroll); return scroll;
        }
        void CreateHeader()
        {
            var header = new VisualElement { name = "app-header" }; header.AddToClassList("header");
            backButton = Action("", Back, "icon-button", "back");
            backIcon = new MuseumIcon(MuseumIconKind.Back); backButton.Add(backIcon); header.Add(backButton);
            var titles = new VisualElement(); titles.AddToClassList("header-copy");
            var brand = new Label("Heritage XR"); brand.AddToClassList("brand");
            brand.languageDirection = LanguageDirection.LTR; brand.style.unityFontDefinition = FontDefinition.FromSDFFont(MuseumFonts.Body);
            titles.Add(brand); headerSubtitle = new Label(); headerSubtitle.AddToClassList("header-subtitle"); titles.Add(headerSubtitle); header.Add(titles);
            settingsButton = Action("", () => Navigate(MuseumPage.Preferences), "icon-button", "preferences");
            settingsIcon = new MuseumIcon(MuseumIconKind.Settings); settingsButton.Add(settingsIcon); header.Add(settingsButton);
            shell.Add(header);
        }
        void UpdateHeader()
        {
            bool canBack = history.Count > 0;
            backButton.style.visibility = canBack ? Visibility.Visible : Visibility.Hidden;
            backButton.SetEnabled(canBack); backButton.tooltip = T("back");
            backIcon.Mirrored = localization.IsArabic;
            settingsButton.tooltip = T("preferences");
            settingsButton.EnableInClassList("active", CurrentPage == MuseumPage.Preferences);
            var color = CurrentPage == MuseumPage.Scanner ? Color.white : Ink;
            backIcon.Color = color; settingsIcon.Color = color;
            headerSubtitle.text = T(CurrentPage switch {
                MuseumPage.Home => "home", MuseumPage.Collection => "collection", MuseumPage.Detail => "artwork", MuseumPage.ScanGuide => "guide",
                MuseumPage.Scanner => "scan", MuseumPage.Viewer => "viewer", MuseumPage.Preferences => "preferences",
                MuseumPage.QuizIntro or MuseumPage.QuizQuestion or MuseumPage.QuizFeedback or MuseumPage.QuizResult => "quiz.title", MuseumPage.QuizProgress => "quiz.progress.title", _ => "credits"
            });
        }
        void CreateNavigation()
        {
            destinations.Clear();
            nav = new VisualElement { name = "bottom-navigation" }; nav.AddToClassList("navigation");
            AddDestination(MuseumPage.Home, MuseumIconKind.Discover, "discover");
            AddDestination(MuseumPage.Collection, MuseumIconKind.Collection, "collection");
            AddDestination(MuseumPage.ScanGuide, MuseumIconKind.Scan, "scanner");
            shell.Add(nav);
        }
        void AddDestination(MuseumPage page, MuseumIconKind kind, string name)
        {
            var button = Action("", () => SwitchTab(page), "nav-item", "nav-" + name);
            var icon = new MuseumIcon(kind); button.Add(icon);
            var label = new Label(); label.AddToClassList("nav-label"); button.Add(label); nav.Add(button);
            destinations.Add(page, (button, label, icon));
        }
        void UpdateNavigation()
        {
            nav.style.display = CurrentPage == MuseumPage.Home || CurrentPage == MuseumPage.Collection || CurrentPage == MuseumPage.ScanGuide ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var entry in destinations)
            {
                string label = T(entry.Key == MuseumPage.Home ? "home" : entry.Key == MuseumPage.Collection ? "collection" : "scan");
                entry.Value.label.text = label; entry.Value.button.tooltip = label;
                entry.Value.button.EnableInClassList("selected", CurrentPage == entry.Key);
                entry.Value.icon.Color = CurrentPage == entry.Key ? Ink : new Color(.43f, .48f, .45f);
            }
        }
        VisualElement Photo(MuseumArtwork artwork, string css)
        {
            var photo = new VisualElement(); photo.AddToClassList(css);
            photo.style.backgroundImage = new StyleBackground(artwork.photograph); return photo;
        }
        void Home()
        {
            var content = Content();
            content.Add(Words("home.eyebrow", "eyebrow")); content.Add(Words("home.title", "hero-title")); content.Add(Words("home.intro", "intro"));
            var featured = new Button(() => Navigate(MuseumPage.Detail, collection.artworks[0].id)) { name = "featured-artwork", tooltip = Copy(collection.artworks[0]).title };
            featured.AddToClassList("featured-card");
            var image = Photo(collection.artworks[0], "hero-photo");
            var caption = new VisualElement(); caption.AddToClassList("photo-caption");
            caption.Add(Words("home.featured", "photo-kicker")); caption.Add(Words("home.caption", "photo-title")); image.Add(caption); featured.Add(image); content.Add(featured);
            content.Add(Action(T("home.explore"), () => SwitchTab(MuseumPage.Collection), "primary", "explore"));
            content.Add(Action(T("quiz.progress.open"), () => Navigate(MuseumPage.QuizProgress), "secondary", "quiz-progress"));
            content.Add(Text(string.Format(T("home.footer"), ArtworkCount()), "footnote"));
        }
        void Collection()
        {
            var content = Content();
            content.Add(Words("egypt", "eyebrow")); content.Add(Text(string.Format(T("collection.title"), ArtworkCount()), "page-title")); content.Add(Words("collection.intro", "intro"));
            foreach (var artwork in collection.artworks)
            {
                var copy = Copy(artwork);
                var card = new Button(() => Navigate(MuseumPage.Detail, artwork.id)) { name = "artwork-" + artwork.id, tooltip = copy.title };
                card.AddToClassList("artwork-card"); card.Add(Photo(artwork, "card-photo"));
                var words = new VisualElement(); words.AddToClassList("card-copy");
                var id = Text(artwork.museumObjectId, "eyebrow"); id.languageDirection = LanguageDirection.LTR; words.Add(id);
                words.Add(Text(copy.title, "card-title")); words.Add(Text(copy.summary, "card-summary"));
                words.Add(Words("collection.open", "card-link")); card.Add(words); content.Add(card);
            }
        }
        void Detail()
        {
            var content = Content(); var copy = Copy(selected);
            content.Add(Photo(selected, "detail-photo")); content.Add(Words("egypt", "eyebrow"));
            content.Add(Text(copy.title, "page-title")); content.Add(Text(copy.summary, "intro"));
            content.Add(Action(T("detail.ar"), () => Navigate(MuseumPage.ScanGuide), "primary", "see-ar"));
            content.Add(Action(T("detail.3d"), () => Navigate(MuseumPage.Viewer), "secondary", "see-3d"));
            if (selected.quiz != null) content.Add(Action(T("quiz.open"), () => Navigate(MuseumPage.QuizIntro), "secondary", "quiz-artwork"));
            content.Add(Words("detail.story", "section-title")); content.Add(Text(copy.description));
            content.Add(Action(T("credits"), () => Navigate(MuseumPage.Credits), "text-link", "credits"));
        }
        void ScanGuide()
        {
            var content = Content();
            content.Add(Words("guide.eyebrow", "eyebrow")); content.Add(Words("guide.title", "page-title"));
            content.Add(Words(selected == null ? "guide.intro" : "guide.selected", "intro")); content.Add(Photo(selected ?? collection.artworks[0], "guide-photo"));
            Step(content, "01", "guide.step1"); Step(content, "02", "guide.step2"); Step(content, "03", "guide.step3");
            content.Add(Words("guide.privacy", "footnote"));
            var actions = new VisualElement(); actions.AddToClassList("guide-actions");
            actions.Add(Action(T("guide.start"), () => Navigate(MuseumPage.Scanner), "primary", "start-scan")); body.Add(actions);
        }
        void Step(VisualElement parent, string number, string key)
        {
            var row = new VisualElement(); row.AddToClassList("step");
            row.Add(Text(number, "step-number"));
            var copy = new VisualElement(); copy.AddToClassList("step-copy"); copy.Add(Words(key, "step-title")); copy.Add(Words(key + ".detail", "small"));
            row.Add(copy); parent.Add(row);
        }
        void Scanner()
        {
            var status = new VisualElement(); status.AddToClassList("scan-status-row");
            scanBadge = Words("scan.badge", "scan-badge"); status.Add(scanBadge); body.Add(status);
            var focus = new VisualElement { pickingMode = PickingMode.Ignore }; focus.AddToClassList("scan-focus");
            for (int i = 0; i < 4; i++) { var corner = new VisualElement(); corner.AddToClassList("corner"); corner.AddToClassList("corner-" + i); focus.Add(corner); }
            body.Add(focus);
            CreateSubtitles(body, true);
            var prompt = new ScrollView(ScrollViewMode.Vertical); prompt.AddToClassList("scan-prompt");
            prompt.contentContainer.AddToClassList("scan-prompt-content");
            prompt.verticalScrollerVisibility = ScrollerVisibility.Hidden; prompt.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scanHeading = Words("scan.default.title", "scan-heading"); scanHint = Words("scan.default.hint", "scan-hint"); prompt.Add(scanHeading); prompt.Add(scanHint);
            var controls = new VisualElement(); controls.AddToClassList("scan-audio-controls");
            scanPlay = Action(T("audio.play"), () => { player.Toggle(); RefreshAudioControls(); }, "scan-action", "scan-audio"); controls.Add(scanPlay);
            controls.Add(SubtitleControl()); prompt.Add(controls);
            audioStatus = Text("", "scan-hint"); prompt.Add(audioStatus);
            foundButton = Action(T("scan.details"), () => { if (ar.CurrentArtwork != null) Navigate(MuseumPage.Detail, ar.CurrentArtwork.id); }, "scan-action", "found-artwork"); prompt.Add(foundButton);
            scanSettingsButton = Action(T("scan.settings"), CameraPermission.OpenSettings, "scan-action", "camera-settings"); prompt.Add(scanSettingsButton);
            scanRetryButton = Action(T("scan.retry"), () => ar.StartScanning(selected?.id), "scan-action", "retry-scan"); prompt.Add(scanRetryButton);
            prompt.Add(Action(T("scan.collection"), () => SwitchTab(MuseumPage.Collection), "scan-link", "scan-collection")); body.Add(prompt);
            ar.StartScanning(selected?.id); OnScanChanged(ar.State, ar.CurrentArtwork);
        }
        void OnScanChanged(ScanState state, MuseumArtwork artwork)
        {
            if (CurrentPage != MuseumPage.Scanner || scanHeading == null) return;
            string key = state switch {
                ScanState.Permission => "permission", ScanState.Starting => "starting", ScanState.Searching => "searching", ScanState.Found => "found",
                ScanState.Lost => "lost", ScanState.Denied => "denied", ScanState.Restricted => "restricted", ScanState.Unavailable => "unavailable", ScanState.Failed => "failed", _ => "default"
            };
            scanHeading.text = state == ScanState.Found && artwork != null ? Copy(artwork).title : T("scan." + key + ".title");
            if (state == ScanState.Searching && selected != null) scanHeading.text = T("scan.searching.selected");
            scanHint.text = (state == ScanState.Searching && selected != null ? Copy(selected).title + "\n" : "") + T("scan." + key + ".hint");
            scanHint.style.display = state == ScanState.Found ? DisplayStyle.None : DisplayStyle.Flex;
            scanBadge.text = T(state == ScanState.Found ? "scan.found.badge" : "scan.badge");
            foundButton.style.display = state == ScanState.Found ? DisplayStyle.Flex : DisplayStyle.None;
            scanSettingsButton.style.display = state == ScanState.Denied ? DisplayStyle.Flex : DisplayStyle.None;
            scanRetryButton.style.display = state == ScanState.Failed ? DisplayStyle.Flex : DisplayStyle.None;
            if (state == ScanState.Found && artwork != null)
            {
                player.Select(artwork, localization.Language, AudioKind);
                player.SetSpatial(true, ar.CurrentAnchor);
            }
            else if (state == ScanState.Lost) player.SetSpatial(true);
            else if (state != ScanState.Searching) player.Stop();
            scanPlay.style.display = state == ScanState.Found || state == ScanState.Lost ? DisplayStyle.Flex : DisplayStyle.None;
            scanPlay.SetEnabled(state == ScanState.Found && player.Available);
            subtitleToggle.style.display = state == ScanState.Found || state == ScanState.Lost ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshAudioControls();
        }
        void Viewer()
        {
            body.Add(Text(Copy(selected).title, "viewer-title"));
            viewer.Show(selected);
            var image = new Image { image = viewer.texture, name = "model-view", scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("model-view"); body.Add(image);
            image.RegisterCallback<PointerDownEvent>(e =>
            {
                if (pointers.Count >= 2) return;
                pointers[e.pointerId] = e.position; image.CapturePointer(e.pointerId);
                if (pointers.Count == 2)
                {
                    var positions = new List<Vector2>(pointers.Values); pinchDistance = Vector2.Distance(positions[0], positions[1]); pinchCenter = (positions[0] + positions[1]) * .5f;
                }
                else pinchDistance = 0;
                e.StopPropagation();
            });
            image.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!pointers.TryGetValue(e.pointerId, out var previous)) return;
                pointers[e.pointerId] = e.position;
                if (pointers.Count == 1) viewer.Rotate((Vector2)e.position - previous);
                else
                {
                    var positions = new List<Vector2>(pointers.Values); float distance = Vector2.Distance(positions[0], positions[1]); var center = (positions[0] + positions[1]) * .5f;
                    if (pinchDistance > 0) viewer.Zoom(distance / pinchDistance);
                    viewer.Pan(center - pinchCenter, Mathf.Min(image.contentRect.width, image.contentRect.height)); pinchDistance = distance; pinchCenter = center;
                }
                e.StopPropagation();
            });
            image.RegisterCallback<PointerUpEvent>(e => { pointers.Remove(e.pointerId); image.ReleasePointer(e.pointerId); pinchDistance = 0; });
            image.RegisterCallback<PointerCancelEvent>(e => { pointers.Remove(e.pointerId); image.ReleasePointer(e.pointerId); pinchDistance = 0; });
            image.RegisterCallback<PointerCaptureOutEvent>(e => { pointers.Remove(e.pointerId); pinchDistance = 0; });
            image.RegisterCallback<WheelEvent>(e => { viewer.Zoom(Mathf.Exp(-e.delta.y * .04f)); e.StopPropagation(); });
            body.Add(Words("viewer.hint", "viewer-hint"));
            var controls = new VisualElement(); controls.AddToClassList("viewer-controls");
            var minus = Action("−", () => viewer.Zoom(.8f), "zoom", "zoom-out"); minus.tooltip = T("viewer.zoom.out"); controls.Add(minus);
            controls.Add(Action(T("viewer.reset"), viewer.ResetPose, "secondary", "reset-view"));
            var plus = Action("+", () => viewer.Zoom(1.25f), "zoom", "zoom-in"); plus.tooltip = T("viewer.zoom.in"); controls.Add(plus); body.Add(controls);
            ViewerAudio();
        }
        void Preferences()
        {
            var content = Content();
            content.Add(Words("preferences.eyebrow", "eyebrow")); content.Add(Words("preferences.title", "page-title")); content.Add(Words("preferences.intro", "intro"));
            content.Add(Words("preferences.language", "section-title")); content.Add(Words("preferences.language.hint", "small"));
            var languages = new VisualElement(); languages.AddToClassList("settings-card");
            LanguageChoice(languages, "fr", "Français", "FR"); LanguageChoice(languages, "ar", "العربية", "AR"); LanguageChoice(languages, "en", "English", "EN"); content.Add(languages);
            content.Add(Words("preferences.comfort", "section-title"));
            var comfort = new VisualElement(); comfort.AddToClassList("settings-card");
            Preference(comfort, "preferences.large", "large-text-toggle", largeText, value => { largeText = value; root.EnableInClassList("large-text", value); PlayerPrefs.SetInt("heritage.largeText", value ? 1 : 0); PlayerPrefs.Save(); });
            Preference(comfort, "preferences.motion", "reduce-motion-toggle", reduceMotion, value => { reduceMotion = value; root.EnableInClassList("reduce-motion", value); PlayerPrefs.SetInt("heritage.reduceMotion", value ? 1 : 0); PlayerPrefs.Save(); });
            content.Add(comfort);
            content.Add(Words("preferences.audio", "section-title"));
            var audio = new VisualElement(); audio.AddToClassList("settings-card");
            Preference(audio, "preferences.audiodescription", "audio-description-toggle", AudioKind == "audio_description", value => { PlayerPrefs.SetInt("heritage.audioDescription", value ? 1 : 0); PlayerPrefs.Save(); });
            Preference(audio, "preferences.transcript", "transcript-toggle", PlayerPrefs.GetInt("heritage.transcript", 1) == 1, value => { PlayerPrefs.SetInt("heritage.transcript", value ? 1 : 0); PlayerPrefs.Save(); });
            Preference(audio, "preferences.subtitles", "subtitles-toggle", SubtitlesEnabled, value => { PlayerPrefs.SetInt("heritage.subtitles", value ? 1 : 0); PlayerPrefs.Save(); });
            var volume = new Slider(T("audio.volume"), 0, 1) { value = player.Volume, name = "audio-volume" }; volume.AddToClassList("volume-slider");
            volume.RegisterValueChangedCallback(e => player.Volume = e.newValue); audio.Add(volume);
            content.Add(audio); content.Add(Words("preferences.privacy", "preferences-note"));
            AboutSection(content);
        }
        void LanguageChoice(VisualElement parent, string language, string nativeName, string code)
        {
            var choice = Action("", () => ChangeLanguage(language), "language-choice", "language-" + language);
            bool active = localization.Language == language; choice.EnableInClassList("chosen", active);
            var badge = Text(code, "language-code"); badge.languageDirection = LanguageDirection.LTR; badge.style.unityFontDefinition = FontDefinition.FromSDFFont(MuseumFonts.Body); choice.Add(badge);
            var name = Text(nativeName, "language-name"); name.languageDirection = language == "ar" ? LanguageDirection.RTL : LanguageDirection.LTR;
            name.style.unityFontDefinition = FontDefinition.FromSDFFont(language == "ar" ? MuseumFonts.Arabic : MuseumFonts.Body); choice.Add(name);
            var check = new MuseumIcon(MuseumIconKind.Check); check.style.visibility = active ? Visibility.Visible : Visibility.Hidden; choice.Add(check);
            choice.tooltip = nativeName + (active ? " · " + T("selected") : ""); parent.Add(choice);
        }
        void Preference(VisualElement parent, string key, string name, bool value, Action<bool> changed)
        {
            var row = new VisualElement(); row.AddToClassList("setting-row");
            var copy = new VisualElement(); copy.AddToClassList("setting-copy"); copy.Add(Words(key, "setting-title")); copy.Add(Words(key + ".hint", "setting-description")); row.Add(copy);
            var toggle = new Toggle { name = name, value = value, tooltip = T(key) }; toggle.AddToClassList("setting-toggle");
            toggle.RegisterValueChangedCallback(e => changed(e.newValue)); row.Add(toggle); parent.Add(row);
        }
        void Credits()
        {
            var content = Content(); content.Add(Text(Copy(selected).title, "page-title"));
            content.Add(Words("credits.photo", "section-title")); content.Add(Provenance(selected.photoCredit + " · " + selected.photoLicence));
            content.Add(Action(T("credits.photo.source"), () => Application.OpenURL(selected.photoSource), "text-link"));
            content.Add(Words("credits.model", "section-title")); content.Add(Provenance(string.Join(", ", selected.modelCredits) + "\n" + selected.modelLicence));
            content.Add(Action(T("credits.model.source"), () => Application.OpenURL(selected.modelSource), "text-link"));
            content.Add(Words("credits.audio", "section-title")); content.Add(Words("credits.audio.note", "small"));
            content.Add(Words("credits.description", "section-title")); content.Add(Words("credits.description.note", "small"));
            for (int i = 0; i < selected.descriptionSources.Length; i++)
            {
                string source = selected.descriptionSources[i];
                content.Add(Action(string.Format(T("credits.source"), i + 1), () => Application.OpenURL(source), "text-link"));
            }
        }
        string ArtworkCount()
        {
            string value = collection.artworks.Count.ToString();
            if (!localization.IsArabic) return value;
            const string digits = "٠١٢٣٤٥٦٧٨٩";
            var characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++) if (characters[i] >= '0' && characters[i] <= '9') characters[i] = digits[characters[i] - '0'];
            return new string(characters);
        }
        static string Clock(float seconds) => $"{Mathf.FloorToInt(seconds / 60):00}:{Mathf.FloorToInt(seconds) % 60:00}";
        void CreateSubtitles(VisualElement parent, bool camera)
        {
            subtitlePanel = new VisualElement { name = "subtitle-panel", pickingMode = PickingMode.Ignore };
            subtitlePanel.AddToClassList("subtitle-panel");
            if (camera) subtitlePanel.AddToClassList("ar-subtitles");
            subtitleText = Text("", "subtitle-text"); subtitleText.name = "subtitle-text";
            subtitlePanel.Add(subtitleText); subtitlePanel.style.display = DisplayStyle.None; parent.Add(subtitlePanel);
        }
        Button SubtitleControl()
        {
            subtitleToggle = Action("", () =>
            {
                PlayerPrefs.SetInt("heritage.subtitles", SubtitlesEnabled ? 0 : 1); PlayerPrefs.Save(); RefreshAudioControls();
            }, "subtitle-control", "subtitle-control");
            return subtitleToggle;
        }
        void RefreshAudioControls()
        {
            if (player == null || (CurrentPage != MuseumPage.Viewer && CurrentPage != MuseumPage.Scanner)) return;
            if (subtitleToggle != null)
            {
                subtitleToggle.text = T(SubtitlesEnabled ? "subtitles.on" : "subtitles.off");
                subtitleToggle.tooltip = T(SubtitlesEnabled ? "subtitles.hide" : "subtitles.show");
                subtitleToggle.EnableInClassList("chosen", SubtitlesEnabled);
                subtitleToggle.SetEnabled(player.Track != null && player.Track.subtitles.Count > 0);
            }
            if (subtitlePanel != null)
            {
                string cue = SubtitlesEnabled && (CurrentPage != MuseumPage.Scanner || ar.State == ScanState.Found) ? player.Subtitle : null;
                if (subtitleText.text != (cue ?? "")) subtitleText.text = cue ?? "";
                bool active = SubtitlesEnabled && player.HasPlaybackPosition && (CurrentPage != MuseumPage.Scanner || ar.State == ScanState.Found);
                subtitlePanel.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                // Retain the slot across short speech gaps; the camera frame never jumps per cue.
                subtitlePanel.style.visibility = string.IsNullOrEmpty(cue) ? Visibility.Hidden : Visibility.Visible;
            }
            var button = CurrentPage == MuseumPage.Viewer ? audioPlay : scanPlay;
            if (button != null)
            {
                button.text = T(player.WantsPlayback ? "audio.pause" : "audio.play");
                if (CurrentPage == MuseumPage.Viewer) button.SetEnabled(player.Available);
                else button.SetEnabled(player.Available && (ar.State == ScanState.Found || ar.State == ScanState.Lost && player.WantsPlayback));
            }
            if (audioTime != null) audioTime.text = Clock(player.Position) + " / " + Clock(player.Duration);
            if (audioSeek != null)
            {
                // Reset the previous track's slider value before shrinking its range.
                // A range clamp can otherwise seek the newly selected clip accidentally.
                if (!seeking) audioSeek.SetValueWithoutNotify(player.Position);
                audioSeek.highValue = Mathf.Max(.1f, player.Duration);
                audioSeek.SetEnabled(player.Available);
            }
            if (audioStatus != null)
            {
                audioStatus.text = player.TrackingPaused ? T("audio.tracking.paused") : player.Available ? T(AudioKind == "audio_description" ? "audio.description" : "audio.narration") : CurrentPage == MuseumPage.Viewer ? T("audio.unavailable") : "";
                audioStatus.style.display = string.IsNullOrEmpty(audioStatus.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        Label Provenance(string value)
        {
            var label = Text(value); label.languageDirection = LanguageDirection.LTR;
            label.style.unityFontDefinition = FontDefinition.FromSDFFont(MuseumFonts.Body); return label;
        }
    }
}
