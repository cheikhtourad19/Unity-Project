# Polices embarquées

Noto Sans Regular pour les commandes et le texte courant ; Noto Serif Regular pour les grands titres latins ; Noto Sans Arabic Regular pour l’arabe.

Fichiers officiels issus du [dépôt Noto Fonts](https://github.com/notofonts/noto-fonts), répertoire `hinted/ttf`. Licence SIL Open Font License conservée dans `OFL.txt`. Fichiers `.ttf` embarqués dans Resources : aucune récupération réseau au lancement.

Des FontAsset dynamiques TextCore sont créés une fois par session ; le rendu utilise Advanced Text Generator et `languageDirection` pour la composition arabe. [Documentation Unity sur le générateur avancé](https://docs.unity.com/en-us/engine/6000.0/manual/uitoolkits/uielements/uie-work-with-text/uie-advanced-text-generator).
