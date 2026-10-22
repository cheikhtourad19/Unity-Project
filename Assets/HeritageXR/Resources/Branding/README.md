# HeritageXR — identité visuelle

Création : 9 octobre 2026, outil intégré `image_gen.imagegen`, compétence imagegen. Dimensions des originaux : icône 1254 × 1254 px, fond 941 × 1672 px, PNG opaques. Deux générations distinctes, aucune clé API ou génération ElevenLabs utilisée pour ces visuels. Les originaux sélectionnés sont copiés dans ce dossier ; ces copies sont les assets du projet.

- `AppIcon.png` : icône opaque, temple/stèle doré sur vert profond, quatre repères AR. Les slots d’icônes iOS sont configurés par `BrandingBuildPreparation.Configure()` ; Unity génère les dimensions exportées.
- `SplashBackground.png` : fond portrait du lancement natif iOS et du splash UI. Le texte académique est rendu séparément pour conserver une bonne lisibilité.

Crédit fourni par l’utilisateur, orthographe conservée : « Projet académique réalisé par Mohamed Elgheith, étudiant à esprim. » Il figure dans les storyboards natifs et l’overlay de lancement ; les réglages possèdent une section À propos localisée. L’illustration est une identité graphique, sans revendication de reconstitution historique exacte.

Le splash UI passe automatiquement à l’accueil après 2,5 secondes ou par Entrer. Le splash Unity générique est désactivé. Aucun nouveau GameObject de scène ajouté.

## Prompt icône

```text
Use case: logo-brand. Generate one polished production iOS app icon for HeritageXR, an Egyptian museum augmented-reality guide. Square 1024x1024, full bleed opaque background, no rounded outer corners (iOS applies the mask). A single bold stylized Egyptian stone stele silhouette containing a minimal temple doorway, framed by four subtle AR viewfinder corner marks, warm muted gold on deep forest green #203a32. Refined museum identity, balanced geometry, slight tactile stone relief, readable at 60px, centered with 18% safe margins. No lettering, no mockup, no border, no watermark, no tiny details.
```

## Prompt fond de splash

```text
Use case: stylized-concept. Asset: portrait mobile splash-screen background for HeritageXR, museum augmented-reality guide to Ancient Egypt. Production artwork, tall portrait 9:16, full bleed. Deep forest green #203a32 with elegant subtle stone texture, softly illuminated golden Egyptian temple doorway and stone stele composition centered in the middle third, warm muted gold and sandstone. Refined restrained museum aesthetic consistent with a gold temple/stele app icon. Plenty of calm dark negative space at top 25% and bottom 25% for app title and academic developer credit that will be rendered separately by the application. No text, no letters, no people, no phone mockup, no watermark. Strong clean silhouettes and quiet atmosphere, not a busy collage.
```

## Références d’intégration

Configuration Unity via [PlayerSettings.SetPlatformIcons](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.SetPlatformIcons.html), icônes issues de GetPlatformIcons et textures locales. Les ressources/storyboards de lancement existants de l’export sont remplacés en post-traitement ; la compilation Xcode vérifie leur validité de build. Les essais de rendu sur iPhone restent à effectuer par l’utilisateur.

