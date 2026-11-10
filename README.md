# BetterSteamAchievementManager

Une bibliothèque de succès avec une interface sombre inspirée de Steam : jeux à gauche, succès en cartes à droite et panier multi-jeux **Achievement Order**.

[**Télécharger la dernière version**](https://github.com/RK28A/BetterSteamAchievementManager/releases/latest)

## Installation

Windows, **.NET Framework 4.8**, Steam ouvert et un compte connecté.

- **BetterSteamAchievementManager.exe** : un seul fichier à télécharger. Il extrait automatiquement l’application dans `%LOCALAPPDATA%\BetterSteamAchievementManager` et la lance.
- **ZIP portable** : extraire tous les fichiers ensemble, puis lancer `SAM.Picker.exe`.

La barre de fenêtre est intégrée au thème : déplacer, réduire, agrandir et fermer. Les sommes SHA-256 sont disponibles dans chaque Release.

## Bibliothèque

Les jeux sont à gauche. **★ Favoris** et **Installés** filtrent la liste et peuvent se combiner. Le bouton **☆ / ★**, le clic droit ou **Ctrl+D** ajoute ou retire le jeu sélectionné des favoris. Les favoris et filtres sont conservés après fermeture.

Chaque jeu affiche sa dernière progression connue. Elle est mise à jour à l’ouverture de sa vue et après une actualisation Steam ; **—** signifie qu’aucune progression n’a encore été chargée. Le bouton d’actualisation vérifie aussi les jeux installés.

## Succès

Sélectionnez un jeu, puis ses succès. Les boutons permettent de tout sélectionner, tout désélectionner, inverser ou sélectionner les succès non débloqués. La recherche, les filtres et le tri conservent les sélections et les modifications en attente.

Le menu **Trier**, dans le panneau de droite, propose le nom, les plus rares, les plus courants, les non débloqués et les déblocages récents. Les cartes affichent le pourcentage mondial fourni par Steam ; les données indisponibles restent en fin de tri par rareté. Une connexion est nécessaire pour charger ces pourcentages.

**Débloquer** et **Verrouiller** préparent des changements ; **Enregistrer les modifications** les envoie à Steam. **Annuler** rétablit les valeurs chargées. Les statistiques, filtres de jeux, ajout par App ID, actualisation et réinitialisation restent disponibles.

## Achievement Order

1. Sélectionnez les succès souhaités d’un jeu et cliquez sur **+ Achievement Order**.
2. Répétez pour vos autres jeux, puis ouvrez **Achievement Order** en haut de la bibliothèque.
3. Sélectionnez un jeu dans le panier. Réglez ses délais minimum et maximum avec les sliders et choisissez **minutes, heures ou jours**. Par défaut : **3 à 10 minutes** entre ses succès.
4. Activez au besoin **Pause entre les jeux**, avec ses propres bornes et son unité. Cette pause s’ajoute au délai avant le premier succès du jeu suivant.
5. Glissez-déposez les en-têtes pour déplacer les jeux entiers, ou les succès pour changer leur ordre à l’intérieur de leur jeu. Les boutons **Monter**, **Descendre**, **Retirer** et **Vider** restent disponibles. Vérifiez le planning puis cliquez sur **Démarrer**.

Le panier regroupe les jeux et leurs succès avec leurs images, exclut les doublons et les succès protégés ou déjà obtenus au moment de l’ajout. Il est enregistré automatiquement. Un seul succès est envoyé à la fois ; la commande attend la confirmation de Steam avant de poursuivre. L’état Steam est relu avant chaque envoi, y compris à la reprise : un succès déjà obtenu est ignoré sans nouvelle écriture.

Le planning affiche la durée restante minimum/maximum et une fourchette de fin estimée, hors réponses Steam et pauses manuelles. Les délais de chaque succès et les pauses entre jeux sont inclus.

**Pause** sauvegarde le délai restant. Si un envoi est déjà parti, la pause attend sa confirmation. **Reprendre** continue avec ce délai, y compris après avoir fermé puis rouvert l’application. Le temps hors ligne ne raccourcit pas l’attente. Une modification du panier ou de ses délais annule ce point de reprise et recalcule le planning. Une fermeture brutale peut conserver jusqu’à une seconde d’attente supplémentaire.

**Arrêter** remet le délai à zéro pour un prochain démarrage et annule les envois futurs ; un envoi déjà parti attend sa confirmation. Le panier ne reprend jamais automatiquement au démarrage. Les éléments terminés restent visibles et ne sont pas renvoyés au prochain départ. Les modifications du panier sont bloquées pendant son exécution. En cas d’erreur ou d’absence de confirmation, la commande s’arrête ; vérifiez la vue du jeu avant de réessayer.

L’ancienne file par jeu reste dans **Déblocage progressif**. Enregistrez ou annulez les modifications manuelles d’un jeu avant de l’utiliser dans une commande. Gardez Steam et la bibliothèque ouverts pendant l’exécution.

## Historique

Le bouton **Historique** affiche les démarrages, pauses, reprises, confirmations Steam, erreurs et fins de commande, avec le jeu, le succès et l’heure locale. Les 2 000 derniers événements sont conservés dans le panier local. Un résultat incertain reste signalé comme erreur ; il n’est pas présenté comme un succès confirmé.

## Statut Steam et temps de jeu

Ouvrir une vue de jeu crée une session Steam pour son App ID, sans lancer son exécutable. Steam peut alors afficher le compte « en jeu ». Plusieurs vues peuvent rester ouvertes simultanément. **Valider des succès n’ajoute pas une durée de jeu précise** : le temps enregistré est déterminé par Steam, et son cumul pour plusieurs sessions simultanées n’est pas garanti par cette application.

## GameTime Spoofer

Un onglet supplémentaire permet de maintenir plusieurs sessions Steam en parallèle, avec une **durée fixe** ou une **durée aléatoire indépendante par jeu**, en minutes, heures ou jours.

1. Ouvrez **GameTime Spoofer** et cochez les jeux souhaités.
2. Choisissez la durée. Activez **Durée aléatoire pour chaque jeu** pour définir un minimum et un maximum.
3. Cliquez sur **Démarrer la sélection**. Jusqu’à **10 sessions** peuvent fonctionner simultanément.
4. Consultez leur état et le temps restant. Chaque session s’arrête automatiquement ; vous pouvez aussi arrêter une session ou toutes les sessions.

Une session déjà active du même jeu n’est pas démarrée une seconde fois. Le compteur commence lorsque la connexion Steam réussit. Les sessions créées par GameTime se terminent si leur durée expire ou si la bibliothèque est fermée ; elles ne reprennent pas automatiquement après redémarrage.

Ces sessions utilisent les App ID sans lancer les exécutables des jeux. Le minuteur ferme uniquement ses propres sessions : une vue de succès ouverte peut maintenir le jeu actif. **Le temps crédité et son cumul entre plusieurs jeux restent déterminés par Steam, sans garantie de correspondance avec la durée affichée.** La durée affichée mesure la session de l’application.

En mode `--demo`, GameTime lance des sessions simulées sans connexion Steam.

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
