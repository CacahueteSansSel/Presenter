# Code Flow

Chaque diapo garde son propre script et ses identifiants d’éléments. Retrouvez ces identifiants dans la barre **Éléments** et dans l’onglet **Description**. Dans **Code Flow**, **Ctrl + Espace** suggère les fonctions et, dans le premier argument entre guillemets, les identifiants des éléments.

```js
async function beforePresent() {
    hide("demo");
}

part(async () => {
    await fadeIn("demo", 500);
});

async function afterPresent() {
    await fadeOut("demo", 250);
}
```

Presenter exécute `beforePresent()` à l’arrivée. Chaque clic, pression sur **Espace** ou **→** déclenche la prochaine fonction inscrite avec `part`. Après la dernière étape, l’avance suivante exécute `afterPresent()` et change de diapo. **←** revient à l’étape précédente en rejouant le script depuis son état initial. **Échap** termine la projection. **Tester** démarre à la diapo courante.

| Fonction | Effet |
| --- | --- |
| `show(id)`, `hide(id)` | Change immédiatement la visibilité |
| `await fadeIn(id, ms)`, `await fadeOut(id, ms)` | Anime l’opacité sur une durée en millisecondes, 400 par défaut |
| `await wait(ms)` | Attend avant de poursuivre |
| `text(id, value)` | Remplace le contenu, avec prise en charge du Markdown pour le texte |
| `move(id, x, y)` | Déplace sur la grille de 1200 × 675 |
| `rotate(id, degrees)` | Applique un angle en degrés |

Dans l’éditeur, les éléments s’alignent sur les bords et les centres des autres éléments pendant le déplacement. Maintenez **Alt** (Option sur macOS) pour déplacer un élément librement.
