# BetterSteamAchievementManager

Une bibliothèque de succès avec une interface sombre inspirée de Steam : jeux à gauche, succès en cartes à droite et panier multi-jeux **Achievement Order**.

[**Télécharger la dernière version**](https://github.com/RK28A/BetterSteamAchievementManager/releases/latest)

## Installation

Windows, **.NET Framework 4.8**, Steam ouvert et un compte connecté.

- **BetterSteamAchievementManager.exe** : un seul fichier à télécharger. Il extrait automatiquement l’application dans `%LOCALAPPDATA%\BetterSteamAchievementManager` et la lance.
- **ZIP portable** : extraire tous les fichiers ensemble, puis lancer `SAM.Picker.exe`.

La barre de fenêtre est intégrée au thème : déplacer, réduire, agrandir et fermer. Les sommes SHA-256 sont disponibles dans chaque Release.

## Succès

Sélectionnez un jeu, puis ses succès. Les boutons permettent de tout sélectionner, tout désélectionner, inverser ou sélectionner les succès non débloqués. La recherche et les filtres conservent les sélections.

**Débloquer** et **Verrouiller** préparent des changements ; **Enregistrer les modifications** les envoie à Steam. **Annuler** rétablit les valeurs chargées. Les statistiques, filtres de jeux, ajout par App ID, actualisation et réinitialisation restent disponibles.

## Achievement Order

1. Sélectionnez les succès souhaités d’un jeu et cliquez sur **+ Achievement Order**.
2. Répétez pour vos autres jeux, puis ouvrez **Achievement Order** en haut de la bibliothèque.
3. Sélectionnez un jeu dans le panier. Réglez ses délais minimum et maximum avec les sliders et choisissez **minutes, heures ou jours**. Par défaut : **3 à 10 minutes** entre ses succès.
4. Activez au besoin **Pause entre les jeux**, avec ses propres bornes et son unité. Cette pause s’ajoute au délai avant le premier succès du jeu suivant.
5. Utilisez **Monter**, **Descendre**, **Retirer** ou **Vider** pour organiser le panier, puis **Démarrer**.

Le panier regroupe les jeux et leurs succès avec leurs images, exclut les doublons et les succès protégés ou déjà obtenus au moment de l’ajout. Il est enregistré automatiquement. Un seul succès est envoyé à la fois ; la commande attend la confirmation de Steam avant de poursuivre. Un succès déjà obtenu lors de l’exécution est ignoré.

**Arrêter** annule les envois futurs ; un envoi déjà parti attend sa confirmation. Le panier ne reprend jamais automatiquement au démarrage. Les éléments terminés restent visibles et ne sont pas renvoyés au prochain départ. Les modifications du panier sont bloquées pendant son exécution. En cas d’erreur ou d’absence de confirmation, la commande s’arrête ; vérifiez la vue du jeu avant de réessayer.

L’ancienne file par jeu reste dans **Déblocage progressif**. Enregistrez ou annulez les modifications manuelles d’un jeu avant de l’utiliser dans une commande. Gardez Steam et la bibliothèque ouverts pendant l’exécution.

## Build et Releases

```powershell
dotnet build SAM.Tests/SAM.Tests.csproj -c Release -p:Platform=x86
./SAM.Tests/bin/x86/Release/net48/SAM.Tests.exe
dotnet build SAM.Launcher/SAM.Launcher.csproj -c Release -p:Platform=x86
```

Le workflow **Windows build** compile, exécute les tests Windows hors ligne, vérifie le `.exe` unique et fournit les captures dans ses artifacts. Un tag `v*` publie le `.exe`, le ZIP et les sommes SHA-256 dans une Release après réussite des tests. Les tests n’effectuent aucune écriture sur un compte Steam réel.

`BetterSteamAchievementManager.exe --demo` ouvre une démonstration sans connexion Steam ni écriture de compte. Le panier de démonstration reste temporaire.

Code et mentions de licence d’origine conservés dans [LICENSE.txt](LICENSE.txt) et les fichiers sources. Les images des jeux et Steam appartiennent à leurs titulaires.

Merci à [gibbed](https://github.com/gibbed/SteamAchievementManager) pour le projet original Steam Achievement Manager.
