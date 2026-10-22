# HeritageXR — parcours de soutenance, refonte visuelle

Mohamed Elgheith, étudiant à esprim. 18 pages, organisées comme une visite. Les captures d’interface proviennent du prototype initial et sont identifiées ; la version actuelle se démontre sur iPhone.

## 1. Entrer dans HeritageXR

Ouvrir la présentation sur le splash, puis poser le contexte en une phrase : HeritageXR est un guide muséal qui associe une œuvre, une exploration visuelle et un récit. L’application mobile sera montrée pendant la soutenance. Ce visuel est le véritable fond de lancement du projet, une illustration d’identité et non la photographie d’un site archéologique. Présenter le projet académique et annoncer que le jury va suivre un parcours de visite.

Sources :

- HERITAGEXR_CONTEXT.md
- Assets/HeritageXR/Resources/Branding/README.md

## 2. Commencer par le regard

Laisser quelques secondes au public pour regarder la pierre. Demander ce que la photographie permet de comprendre et ce qui demande une explication. HeritageXR répond à ce besoin : relier ce que l’on observe au contexte culturel et donner le temps d’examiner les détails. Ne pas transformer cette mise en situation en résultat d’une étude utilisateur ; elle illustre le besoin de médiation du projet.

Sources :

- HERITAGEXR_CONTEXT.md
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg

## 3. Montrer l’application

Ces images sont de vraies captures d’une étape antérieure du prototype. Elles montrent la construction du parcours : accueil, fiche et viewer. Les réglages, les langues, les audios et les quiz ont ensuite évolué. Ne pas présenter ces vues comme des captures du build 6. La version actuelle est démontrée sur l’iPhone. L’en-tête, les accès aux fonctions et la séparation des pages ont été retravaillés pour garder une navigation lisible.

Sources :

- Assets/Artifacts/Preview/ — captures historiques, pas de nouveaux essais exécutés
- Assets/HeritageXR/Scripts/MuseumApp.cs
- UX_ARCHITECTURE.md

## 4. Reconnaître une cible

L’image affichée à gauche est la véritable photo de référence de Rosette. Le suivi repose sur AR Foundation et ARKit, pas sur une reconnaissance volumétrique par IA. Le guide à droite est une capture historique. La bibliothèque actuelle contient six cibles, avec une largeur de référence de 12 cm à mesurer à l’impression. À la perte de suivi, le modèle est masqué et la voix mise en pause. Pour la même cible, la reprise dépend de l’état de lecture précédent. Aucun démarrage automatique à la seule reconnaissance.

Sources :

- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Assets/Artifacts/Preview/ — captures historiques, pas de nouveaux essais exécutés
- Assets/HeritageXR/Scripts/MuseumAR.cs
- Printables/targets.html

## 5. Explorer le modèle

Le viewer est indépendant du scanner : il permet d’examiner le modèle hors caméra. Le zoom optique documenté va de 0,4× à 24×. Les gestes ont été corrigés après les retours sur le sens horizontal et les rotations verticales. La photographie agrandie à droite est un détail de l’image de Rosette, pas une fausse capture du viewer. Dans la version actuelle, la narration et les sous-titres accompagnent le modèle ; changer de type audio conserve sa pose. La capture montre une étape historique de l’interface.

Sources :

- Assets/Artifacts/Preview/ — captures historiques, pas de nouveaux essais exécutés
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Assets/HeritageXR/Scripts/ArtworkViewer.cs
- Assets/HeritageXR/Scripts/MuseumApp.ViewerAudio.cs

## 6. Donner une voix à l’œuvre

Lire la phrase affichée, puis proposer de l’entendre pendant la démonstration mobile. Le texte vient du véritable script de narration FR de Ramsès II ; aucun son n’est régénéré pour la présentation. Narration culturelle et audiodescription sont deux pistes distinctes. Les voix existantes ont été produites avec ElevenLabs. Dans le scanner, la source est attachée au modèle en son 3D ; dans le viewer, la voix reste en 2D. Le démarrage est manuel et une seule piste peut être active.

Sources :

- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Statue_of_Ramesses_II_at_the_British_Museum.jpg
- Content/NarrationScripts/bm_ea19/narration.fr.txt
- AssetSources/Audio/bm_ea19/narration.fr.mp3
- Assets/HeritageXR/Scripts/MuseumAudio.cs

## 7. Adapter la visite

Les textes et voix existent en français, arabe et anglais. L’arabe bénéficie d’une police embarquée et d’une lecture RTL dans l’application. Les sous-titres sont alignés sur les enregistrements : pause et recherche suivent la position AudioSource. Les préférences de langue, volume, type de piste, taille du texte et sous-titres sont persistantes. Le projet intègre 36 pistes et 352 segments. Cela constitue une prise en compte de l’accessibilité, sans revendiquer une certification générale.

Sources :

- M0tty · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Granite_ram_of_Amun_with_King_Taharqa_-_British_Museum.jpg
- Assets/HeritageXR/Resources/Localization/MuseumStrings.json
- Content/Subtitles/alignment-report.json
- CREDIT_BUILD_REPORT.json

## 8. Faire participer le jury

Poser la question et laisser quelques secondes au public avant de passer à la réponse. Les quatre propositions sont celles du quiz réel de Rosette ; elles ne sont pas inventées pour l’exposé. Le quiz de l’application fournit une seule bonne réponse parmi quatre, puis une correction expliquée. Les textes et identifiants existent en trois langues.

Sources :

- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Content/catalog.json, bm_ea24.games
- Content/QUIZ.md

## 9. Expliquer la réponse

La bonne réponse est hiéroglyphes, démotique et grec. Le texte de présentation de Rosette évoque un décret en l’honneur de Ptolémée V et l’intérêt de comparer les trois écritures pour le déchiffrement. Ne pas prétendre que ce détail historique est une nouvelle recherche du projet. Dans l’application, une bonne réponse vaut un point ; trois questions sont proposées par œuvre. Les réponses, la correction en attente et le meilleur score sont sauvegardés localement. Un changement de langue ne change pas les réponses déjà enregistrées.

Sources :

- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Content/catalog.json, bm_ea24.texts et games
- Assets/HeritageXR/Scripts/MuseumQuiz.cs

## 10. Montrer la collection

Cette composition présente les six œuvres du catalogue actuel. Chaque photo est une ressource existante du projet, volontairement reprise en grand format. Les œuvres disposent d’une même structure de données et des mêmes entrées d’exploration ; les six éléments initiaux ne sont pas une limite logicielle. Citer les identifiants si le jury le demande : EA24, EA74, EA1779, EA19, EA1 et EA23. Les images sont des photographies de référence, pas six captures du rendu 3D.

Sources :

- Content/catalog.json
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Giant_scarab_in_the_British_Museum.jpg
- M0tty · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Granite_ram_of_Amun_with_King_Taharqa_-_British_Museum.jpg
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Statue_of_Ramesses_II_at_the_British_Museum.jpg
- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Red_granite_figure_of_a_lion_from_Gebel_Barkal.jpg
- akhenatenator · CC0-1.0 · https://www.flickr.com/photos/86012097@N08/10678557766/

## 11. Expliquer l’architecture avec un cas concret

Le schéma décrit le fonctionnement local du prototype. MuseumAR gère le suivi ; l’identifiant relie l’œuvre à son modèle et ses pistes ; MuseumApp présente les pages et MuseumAudio pilote la source partagée. Le quiz et la progression sont des composants locaux. Il n’existe pas de serveur métier ou de curateur déployé. La pile documentée est Unity 6, AR Foundation/ARKit, C#, UI Toolkit et Xcode pour la compilation iOS. Les contenus sont embarqués ; il ne faut pas présenter Addressables ou un backend comme réalisés.

Sources :

- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Giant_scarab_in_the_British_Museum.jpg
- Assets/HeritageXR/Scripts/MuseumAR.cs
- Assets/HeritageXR/Scripts/MuseumApp.cs
- Assets/HeritageXR/Scripts/MuseumCollection.cs
- HERITAGEXR_CONTEXT.md

## 12. Relier les données aux médias

Le catalogue actif utilise schemaVersion 2. Chaque œuvre associe texts, model, targets, audio et games, avec versions et sources. L’identifiant bm_ea23 correspond ici au sarcophage de Hapmen. Des templates et des schémas évitent d’inventer une structure différente à chaque ajout. Les informations restent en JSON ; SQLite n’a pas été nécessaire. Les ajouts sont actuellement importés dans Unity et nécessitent un nouveau build. Le mode curateur et la publication dynamique restent des étapes ultérieures, pas un fonctionnement disponible.

Sources :

- akhenatenator · CC0-1.0 · https://www.flickr.com/photos/86012097@N08/10678557766/
- Content/catalog.json
- Content/catalog.schema.json
- Content/artwork.template.json
- Assets/HeritageXR/Editor/CollectionImport.cs

## 13. Raconter une correction

La phrase reprend un problème effectivement signalé par l’utilisateur : la limite de zoom empêchait de lire les inscriptions. Cette difficulté a conduit à corriger les gestes et à étendre le zoom optique. Les essais ont aussi révélé des problèmes de permission caméra et de transition entre pages. Ils ont été corrigés et des retours utilisateur positifs ont suivi sur plusieurs versions. La capture est historique : elle sert à montrer le viewer sur lequel a porté l’itération, pas à inventer une comparaison avant/après de la version actuelle.

Sources :

- Assets/Artifacts/Preview/ — captures historiques, pas de nouveaux essais exécutés
- Historique de la conversation : retour utilisateur sur le viewer
- FUNCTIONAL_TESTS.md
- HERITAGEXR_CONTEXT.md

## 14. Distinguer preuves et objectifs

Le rapport du build 6 indique Succeeded et zéro erreur pour Unity ainsi qu’une compilation Xcode réussie, sans signature. Il reste deux avertissements Unity non bloquants. Les sous-titres et quiz ont été acceptés globalement par l’utilisateur, sans campagne détaillée par scénario. Le statut de la dernière livraison audio du viewer et branding reste celui de livrée/compilée, en attente de confirmation physique détaillée. Aucun taux de reconnaissance, moyenne de FPS ou pourcentage de satisfaction n’est inventé. La présentation accompagne la preuve mobile et ne la remplace pas.

Sources :

- CREDIT_BUILD_REPORT.json
- QUIZ_BUILD_REPORT.json
- SUBTITLE_BUILD_REPORT.json
- Assets/HeritageXR/Resources/Branding/README.md

## 15. Passer au mobile

Prendre l’iPhone et la cible Rosette. Le scénario proposé dure environ quatre minutes : ouvrir la fiche, scanner la cible, lancer la voix et montrer les sous-titres, puis examiner le modèle hors caméra et répondre au quiz. Montrer le résultat et la progression locale. Terminer par un changement de langue et la section À propos. Si le cadre le permet, montrer la pause à la perte de suivi. Ces actions sont à réaliser par le porteur du projet ; aucun nouvel essai sur appareil n’a été exécuté pour fabriquer cette présentation.

Sources :

- HERITAGEXR_CONTEXT.md
- TEST_VIEWER_AUDIO_IPHONE.md
- TEST_QUIZ_IPHONE.md
- Printables/targets.html

## 16. Présenter les limites

Le mode curateur a été reporté explicitement par l’utilisateur. Son interface CRUD et sa chaîne de publication restent à réaliser ; une fiche JSON extensible ne remplace pas un back-office. Addressables et l’ajout de cibles AR à chaud sont des sujets à vérifier séparément. Puzzle 3D, parcours guidé complet, annotations et optimisations restent ouverts. Les objectifs du contexte, non mesurés ici, sont : reconnaissance au moins 95 %, FPS moyen au moins 30, moins de deux pertes de suivi par dix minutes et au moins 80 % d’avis positifs.

Sources :

- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Red_granite_figure_of_a_lion_from_Gebel_Barkal.jpg
- HERITAGEXR_CONTEXT.md
- AGENTS.md

## 17. Conclure et échanger

Le projet assemble une collection documentée, deux modes d’exploration, des voix multilingues, des sous-titres synchronisés et des quiz avec sauvegarde locale. La démonstration donne à voir l’expérience ; les preuves de compilation et les limites restent distinctes. Inviter le jury à revenir sur une fonctionnalité, une correction ou une décision d’architecture. Ne pas annoncer que toutes les exigences initiales ou les indicateurs ont été validés.

Sources :

- HERITAGEXR_CONTEXT.md
- CREDIT_BUILD_REPORT.json
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Statue_of_Ramesses_II_at_the_British_Museum.jpg
- M0tty · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Granite_ram_of_Amun_with_King_Taharqa_-_British_Museum.jpg
- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Giant_scarab_in_the_British_Museum.jpg

## 18. Créditer les ressources

Les visuels de cette refonte sont exclusivement des ressources existantes : splash, icône, photographies et captures historiques du prototype. Aucune image ni voix supplémentaire n’a été générée. Les modèles sont crédités à Daniel Pett, Jennifer Wexler, Thomas Flynn et Copyright Trustees of the British Museum dans le catalogue. Les photographies conservent leurs auteurs, licences et liens. Les recadrages de la présentation sont des choix de mise en page ; ils ne créent pas de faux rendus de l’application.

Sources :

- Content/catalog.json
- https://github.com/BritishMuseumDH/DH-OculusVR-models
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Rosetta_Stone_at_the_British_Museum.jpg
- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Giant_scarab_in_the_British_Museum.jpg
- M0tty · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Granite_ram_of_Amun_with_King_Taharqa_-_British_Museum.jpg
- BabelStone · CC-BY-SA-3.0 · https://commons.wikimedia.org/wiki/File:Statue_of_Ramesses_II_at_the_British_Museum.jpg
- Szilas · CC-BY-SA-4.0 · https://commons.wikimedia.org/wiki/File:Red_granite_figure_of_a_lion_from_Gebel_Barkal.jpg
- akhenatenator · CC0-1.0 · https://www.flickr.com/photos/86012097@N08/10678557766/
- Assets/Artifacts/Preview/ — captures historiques, pas de nouveaux essais exécutés
- Assets/HeritageXR/Resources/Branding/README.md

