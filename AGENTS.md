# Presenter (prononcé à l'anglaise, genre "prézenteure")

Un logiciel (natif, donc pas de web en gros) de création de présentations (pour les oraux) axé technique (blocs de code, fenêtres, etc.) mais quand même utilisable pour les prez avec les gens lambda, avec une subtilité : les animations et le flow de chaque slide est géré par du code : on aurait un onglet à droite avec le code du flow de la slide (chaque slide a sa fonction) et une vm qui exécute le code qui gère la slide.

## Consignes
Utilisation du skill `gregouz-code-style` pour l'ensemble de la codebase.
Le code source sera situé dans src dans une solution sln avec un seul projet "Presenter"
Tes tests seront dans le dossier "tests" et ne seront pas inclus dans le repo final.
Tu feras un script `build-macos.sh` qui générera un .app sans notarization dans un dossier `build`.

## Stack technique
C#, dotnet 10, Avalonia pour l'UI, Jint pour l'interpréteur JavaScript qui servira à gérer le workflow d'une slide.

## VM JavaScript
- Toutes les fonctions exposées à l'user sur l'interface devront être courtes, en anglais, claires et compréhensibles.
- Une "partie", qui pourra être rejouée comme une étape de la slide dans la présentation, sera déclarée avec une méthode part qui prend en paramètre un lambda async décrivant la partie. Quand le présentateur passe sur l'étape, le contenu de la fonction est exécuté en asynchrone.
- Le script de chaque slide sera composé ainsi : une fonction beforePresent() qui sera les étapes exécutées lors de l'affichage de la slide, et afterPresent qui sera appelé à la fin de la slide, toutes deux asynchrones.
- fadeIn et fadeOut pourront controller un fondu dynamique

## Interface
- Tu te baseras sur les maquettes primitives fournies (mockups/*.png) pour créer l'app. 
- La fenêtre principale devra avoir ExtendClientArea à vrai (donc avec une barre de titre personnalisée) comme la maquette, supportant macOS et Windows.
- L'éditeur de code (AvaloniaEdit) devra avoir une coloration syntaxique et les features intelligentes pour le JS avec les méthodes proposées en assistance comme un IDE classique
- CTRL+S permettra de sauvegarder, mais tu mettras aussi dans la barre de titre un bouton "sauvegarder"
- Il y aura sur toute l'appli une gestion undo redo avec CTRL+Z et CTRL+Y (ou CTRL+SHIFT+Z)
- Le mode présentation prendra tout l'écran en fullscreen
- Pour toutes les icônes utilise le pack Magesticons (outline) depuis Iconify
- L'éditeur d'éléments pour les slides aura des commandes de redimensionnement, de translation et de rotation. 
- Un bouton permettra de permuter entre les commandes classiques avec les poignées pour modifier la taille et la position des objets, et une poignée générale (à la Unity) avec translation, rotation et scale.
- Réplique toutes les fonctions basiques d'un éditeur de présentation : templates, titres et sous-titres, images, rubriques, listes, formattage Markdown partout.
- Tu retrouveras les thèmes de présentation dans themes/xx avec :
  - `title.png` : slide de titre
  - `slide.png` : slide intermédiaire
  - `end.png` : slide de fin
- Tu pourras créer tes propres thèmes et les étoffer avec de nouveaux types de slides équivalents.
- Pour les textes, tu permettras de changer les polices d'écritures et les styles de texte. Gras, italique. Arial, JetBrains Mono, etc. Fais en sorte de pouvoir utiliser les polices de Google Fonts.
- Il faut qu'on puisse rajouter facilement des thèmes par la suite. Tu me feras un tuto simple de comment faire pour rajouter un thème dans docs/themes/README.md
- Dans les thèmes, fais-toi plaisir : tu peux ajouter des formes animées en arrière-plan, à la seule condition que ça doit être des animations lentes, qui ne doivent pas distraire les auditeurs.

## Readme
Pour le readme, reste simple et concis. N'inclus pas chaque changement que tu fais : tu présentes, et c'est tout. Inclus la prononciation aussi (et même en phonétique).