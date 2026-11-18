# Créer un thème

1. Dans Presenter, ouvrez **Fichier → Ouvrir le dossier des thèmes**. En développement, vous pouvez aussi ajouter le thème dans le dossier `themes/` du projet.
2. Créez un dossier avec un nom court, par exemple `ocean`. Déposez-y trois images PNG au format 16:9, idéalement en 1920 × 1080.

   | Fichier | Modèle proposé |
   | --- | --- |
   | `title.png` | Titre |
   | `slide.png` | Contenu |
   | `end.png` | Conclusion |

3. Relancez Presenter et choisissez le thème dans la colonne de gauche. Le bouton **+** propose les modèles de diapos. Pour partager une présentation qui utilise un thème personnalisé, joignez le dossier du thème.

Prévoyez des fonds sans texte afin que les titres restent modifiables dans l’éditeur. Un PNG supplémentaire, par exemple `chapitre.png`, devient un modèle de diapo. Le fichier facultatif `theme.json` permet de nommer ce modèle, d’y placer des éléments préremplis et d’activer des formes animées lentes en projection.

```json
{
  "name": "Océan",
  "animate": true,
  "layouts": {
    "chapitre": {
      "name": "Chapitre",
      "elements": [
        {
          "id": "title",
          "kind": "text",
          "content": "## Nouveau chapitre",
          "x": 70,
          "y": 160,
          "width": 1050,
          "height": 170,
          "fontSize": 52,
          "bold": true
        }
      ]
    }
  }
}
```

Les positions et dimensions utilisent une grille de 1200 × 675, avec l’origine en haut à gauche. Consultez le [thème Cocktail](../../themes/cocktail/theme.json) pour voir des exemples de texte centré et de pied de page.
