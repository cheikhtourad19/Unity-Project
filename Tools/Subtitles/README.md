# HeritageXR — préparation des sous-titres

Les scripts et MP3 existants servent de source. Aucun audio n’est régénéré. L’alignement multilingue local utilise MMS_FA et CTC, selon la documentation officielle TorchAudio : https://docs.pytorch.org/audio/2.8/tutorials/forced_alignment_for_multilingual_data_tutorial.html. TorchAudio est fixé à 2.8 car l’API d’alignement est retirée après cette version. La romanisation uroman sert uniquement au calcul ; le texte original français/arabe/anglais est conservé dans les fichiers livrés.

Créer un environnement Python 3.12 isolé, installer `requirements.txt`, puis lancer depuis la racine :

```sh
TORCH_HOME=/private/tmp/heritage-subtitle-models /private/tmp/heritage-subtitle-env/bin/python Tools/Subtitles/align.py --device mps
/private/tmp/heritage-subtitle-env/bin/python Tools/Subtitles/promote.py
```

Utiliser `--device cpu` si MPS n’est pas disponible, et `--only <trackId>` pour une piste. Le modèle public est téléchargé dans le cache temporaire ; il n’est pas distribué avec l’application Unity. Les dépendances ne sont pas ajoutées au runtime mobile. Le calcul local ne consomme pas de crédits ElevenLabs.

`align.py` traite l’acoustique par fenêtres de 12 s avec 1 s de contexte, puis aligne les mots sur l’émission complète. Les intervalles ne proviennent pas d’un partage uniforme de la durée. Les temps ont une résolution d’environ 20 ms. Les fichiers de cache ne sont réutilisés que pour les mêmes empreintes de script et d’audio.

Chaque fichier `Content/Subtitles/<id>/<type>.<lang>.json` contient : identifiant de piste, empreintes audio/script, durée, méthode, segments d’affichage, mots alignés et scores acoustiques. Les scores ne sont pas des probabilités de correction linguistique. La qualité calculée signale les mots à examiner ; elle ne remplace pas l’écoute utilisateur. La segmentation préserve le texte, conserve la ponctuation, prépare deux lignes courtes et évite les chevauchements. Une marge d’affichage après les mots peut aller jusqu’à la prochaine réplique, sans dépasser l’audio.

`promote.py` vérifie les références, empreintes, bornes et conservation du texte pour toutes les pistes avant de modifier le catalogue commun. Les sous-titres restent `draft`, la relecture utilisateur reste ouverte et `readyForPublication` reste faux. `CollectionImport.Sync()` vérifie à nouveau ces données et les copie dans les pistes sérialisées du ScriptableObject. Les JSON de préparation ne sont pas chargés à chaque frame sur iPhone.

Pour une correction manuelle après écoute, ajuster `segments` dans le fichier de la piste, conserver l’identité/les empreintes, puis relancer la promotion et l’import CLI. Modifier une voix ou un script impose de refaire l’alignement. Ne pas garder un minutage correspondant à un ancien son.
