# Ça Sent le Soufre

*[English version below](#english)*

**Ça Sent le Soufre** est un jeu de cartes pour Windows, une version du « Trois-Trois » (connu en anglais sous le nom de *Shithead*), avec deux modes de jeu : les règles officielles et un mode **démon** qui pimente les parties. Vous affrontez des joueurs contrôlés par l'ordinateur, à trois niveaux de difficulté.

> **Le jeu est téléchargeable gratuitement. Il est interdit de le revendre tel quel** (voir [Licence](#licence)).

## Télécharger et jouer

1. Rendez-vous dans l'onglet [**Releases**](https://github.com/Makushimu14/CaSentLeSoufre/releases) du dépôt et téléchargez le fichier `CaSentLeSoufre-Windows.zip` de la dernière version.
2. Décompressez l'archive où vous voulez.
3. Lancez `CaSentLeSoufre.exe`.

Prérequis : Windows 64 bits. Aucune installation. Le jeu enregistre ses réglages, ses statistiques et la partie en cours dans `%USERPROFILE%\AppData\LocalLow\Makushimu\CaSentLeSoufre`.

## Le jeu

Chaque joueur reçoit trois cartes en main, trois cartes visibles et trois cartes cachées. On pose à tour de rôle une carte (ou plusieurs de même valeur) au moins aussi forte que celle du dessus de la défausse, sinon on ramasse la défausse. On joue d'abord sa main, puis ses cartes visibles, puis ses cartes cachées, à l'aveugle. Le dernier joueur qui a encore des cartes perd la manche. **Le 2 est la carte la plus forte du jeu**, le 3 la plus faible.

### Deux modes de jeu

- **Classique** : les règles officielles, de 3 à 5 joueurs.
- **Démon** : de 3 à 10 joueurs, avec un seul changement : le dernier de la manche reçoit une carte visible et une carte cachée de plus à la manche suivante, le premier en perd une de chaque (au minimum une). Le nombre de paquets de cartes est recalculé à chaque manche.

### Une partie

- Une partie compte de **1 à 999 manches**, choisies avant de commencer.
- Les adversaires sont des **IA** dont vous choisissez le nom et le niveau : facile, normal ou difficile. Les IA difficiles se souviennent des cartes que tout le monde a vues et savent s'en servir, sans tricher.
- Un **échange** optionnel en début de manche permet d'échanger des cartes de la main avec les cartes visibles.
- Le **jeu à la volée** (optionnel) permet de poser, hors de son tour, la dernière carte d'un jeu complet de même valeur, tant que le joueur dont c'est le tour n'a pas joué. Plus l'IA est forte, plus elle réagit vite.
- Quatre cartes identiques à la suite brûlent la défausse (le seuil augmente avec le nombre de paquets).

### Des pouvoirs à la carte

Aucun pouvoir n'est actif par défaut : vous choisissez ceux que vous voulez, sans limite de nombre, et la valeur de carte qui porte chacun.

| Pouvoir | Effet |
| --- | --- |
| Remise à zéro | Se pose sur tout, et on peut jouer n'importe quoi dessus (par tradition, le 2). |
| Brûlure | Brûle la défausse, le joueur rejoue (le 10). Se pose selon sa valeur. |
| Transparent | Le suivant doit battre la carte située dessous (le 3). |
| Plus bas | Le suivant doit jouer une valeur inférieure ou égale (le 7). |
| Même ou tête | Le suivant doit jouer la même valeur ou une tête (valet, dame, roi). |
| Passe | Le suivant passe son tour (le 8). |
| Inversion | Le sens du jeu s'inverse (le 9). |
| Ramasse | Le suivant ramasse la défausse et passe son tour (le valet). |
| Rejoue | Le joueur rejoue immédiatement (le 5). |

### Et aussi

- **Statistiques** : pourcentages de victoires et de défaites par joueur (IA comprises), pour les manches et pour les parties, en global et par mode, avec des classements séparés.
- **Sauvegarde automatique** : on peut quitter une partie en cours et la reprendre plus tard exactement là où elle s'était arrêtée.
- **Animations et sons** : distribution, cartes qui volent, effets des pouvoirs, effets sonores et musique, avec volumes réglables.
- **Menu pause** (Échap) pour régler le son, quitter, ou abandonner.
- **Deux langues** : français et anglais, modifiables à tout moment.

### Commandes

À la souris : on clique sur une carte pour la sélectionner, et à nouveau pour la jouer.

Au clavier :

| Touche | Action |
| --- | --- |
| Flèches (ou ZQSD en AZERTY, WASD en QWERTY) | Viser une carte ; haut et bas changent de zone pendant l'échange. |
| Entrée | Choisir ou jouer la carte visée (comme un clic). |
| Espace | Action principale : jouer la sélection, ramasser, valider l'échange, jouer à la volée. |
| Échap | Menu pause. |

Les menus se parcourent aux flèches, avec Entrée ou Espace pour valider.

## Pour les développeurs

Le jeu est fait avec **Unity 6000.6.7f1** (interface construite par code, nouveau système d'entrées, Unity Localization). Le moteur de règles, indépendant d'Unity, est dans `Assets/Scripts/Core` et testé par une suite de tests NUnit dans `Assets/Tests/EditMode`.

- **Construire le jeu** : ouvrir le projet dans Unity, puis menu **Build > Windows**. Le résultat est dans `Builds/Windows` (dossier ignoré par git).
- **Traductions** : le texte source est dans `Assets/Localization/Source/Game.csv` ; les tables sont régénérées par l'outil de l'éditeur à partir de ce fichier.
- **Illustrations et sons** : des petits outils .NET (Windows) génèrent les cartes, les sons, le fond du menu et l'icône : `Tools/CardArt`, `Tools/SoundArt` et `Tools/MenuArt` (lancer `dotnet run --project Tools/<outil>` depuis la racine du projet ; `-- icon` pour l'icône).

## Licence

Ce jeu est **gratuit** : vous pouvez le télécharger, y jouer et le partager gratuitement, et modifier le code pour votre usage personnel. Il est **interdit de le revendre**, tel quel ou modifié, seul ou dans un lot, ni de le proposer en échange d'un paiement. Le texte complet est dans le fichier [LICENSE](LICENSE).

---

## English

**Ça Sent le Soufre** ("It smells like sulphur") is a Windows card game, a version of *Shithead* (called "Trois-Trois" in French) with two modes: the official rules and a **demon** mode that spices things up. You play against computer-controlled opponents at three difficulty levels.

> **The game is free to download. Reselling it as is, is forbidden** (see [License](#license)).

### Download and play

1. Open the [**Releases**](https://github.com/Makushimu14/CaSentLeSoufre/releases) tab of this repository and download `CaSentLeSoufre-Windows.zip` from the latest version.
2. Extract the archive anywhere.
3. Run `CaSentLeSoufre.exe`.

Requires 64-bit Windows. No installation. Settings, statistics and the game in progress are saved in `%USERPROFILE%\AppData\LocalLow\Makushimu\CaSentLeSoufre`.

### Features

- **Classic** mode (3 to 5 players) and **Demon** mode (3 to 10 players: the last player of a round gets one more face-up and one more face-down card next round, the first loses one of each, minimum one; the number of decks is recomputed every round).
- Matches of **1 to 999 rounds**; AI opponents with chosen names and Easy, Normal or Hard difficulty.
- **Nine card powers** that you choose freely, each on the card value of your choice, with no limit on how many (none are active by default). The 2 is the strongest card, the 3 the weakest.
- Optional **initial swap** of hand and face-up cards, and optional **jump-in** (play out of turn the last card of a complete set of the same value).
- **Statistics** (win/loss percentages for rounds and matches, per player, per mode and overall), **automatic save and resume**, animations, sound effects and music, pause menu.
- **French and English**, mouse and keyboard (arrows, or WASD / ZQSD to aim, Enter to pick or play, Space for the main action, Esc for the pause menu).

### Building from source

Open the project with **Unity 6000.6.7f1** and use **Build > Windows**; the output goes to `Builds/Windows`. The rules engine in `Assets/Scripts/Core` is independent of Unity and covered by NUnit tests in `Assets/Tests/EditMode`.

### License

This game is **free**: you may download it, play it and share it for free, and modify the code for your own personal use. **Reselling it, as is or modified, alone or in a bundle, or offering it in exchange for payment, is forbidden.** The full text is in the [LICENSE](LICENSE) file.
