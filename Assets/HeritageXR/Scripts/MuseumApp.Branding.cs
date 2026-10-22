using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeritageXR
{
    public partial class MuseumApp
    {
        static bool startupShown;
        VisualElement startupSplash;
        const string DeveloperName = "Mohamed Elgheith";

        void AboutSection(VisualElement content)
        {
            content.Add(Words("about.title", "section-title"));
            var card = new VisualElement { name = "about-section" }; card.AddToClassList("about-card");
            var icon = new Image { image = Resources.Load<Texture2D>("Branding/AppIcon"), scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("about-icon"); card.Add(icon);
            card.Add(Provenance("HeritageXR")); card.Add(Words("about.academic", "about-project"));
            card.Add(Words("about.by", "small")); card.Add(Provenance(DeveloperName));
            card.Add(Words("about.affiliation", "paragraph")); card.Add(Words("about.description", "small"));
            card.Add(Text(Format("about.version", Application.version), "preferences-note")); content.Add(card);
        }
        void ShowStartupSplash()
        {
            if (startupShown) return;
            startupShown = true;
            startupSplash = new VisualElement { name = "startup-splash" }; startupSplash.AddToClassList("startup-splash");
            var art = new Image { image = Resources.Load<Texture2D>("Branding/SplashBackground"), scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            art.AddToClassList("splash-art"); startupSplash.Add(art);
            var copy = new VisualElement(); copy.AddToClassList("splash-copy");
            var title = Provenance("HeritageXR"); title.AddToClassList("splash-title"); copy.Add(title);
            copy.Add(Words("splash.subtitle", "splash-subtitle"));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; copy.Add(spacer);
            // Exact user-supplied credit remains French on launch, regardless of UI language.
            var credit = Provenance("Projet académique réalisé par " + DeveloperName + ", étudiant à esprim."); credit.AddToClassList("splash-credit"); copy.Add(credit);
            var enter = Action(T("splash.enter"), DismissStartupSplash, "splash-enter", "splash-enter"); copy.Add(enter);
            startupSplash.Add(copy); root.Add(startupSplash);
            StartCoroutine(DismissSplashAfterDelay());
        }
        IEnumerator DismissSplashAfterDelay()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            DismissStartupSplash();
        }
        void DismissStartupSplash()
        {
            startupSplash?.RemoveFromHierarchy(); startupSplash = null;
        }
    }
}
