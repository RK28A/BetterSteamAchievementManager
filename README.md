# BetterSteamAchievementManager

Une bibliothèque de succès avec une interface graphite et violette, un panier multi-jeux et des sessions **GameTime Spoofer**.

[**Télécharger la dernière version**](https://github.com/RK28A/BetterSteamAchievementManager/releases/latest)

## Installation

Windows, **.NET Framework 4.8**, Steam ouvert et un compte connecté.

- **BetterSteamAchievementManager.exe** : un seul fichier à télécharger ; l’application extrait ses composants dans `%LOCALAPPDATA%\BetterSteamAchievementManager`.
- **ZIP portable** : extraire tous les fichiers ensemble et lancer `SAM.Picker.exe`.

## Bibliothèque et succès

Les jeux restent à gauche, avec recherche, favoris, filtre des jeux installés et progression connue. Ouvrez un jeu pour actualiser sa progression. Les cartes de succès affichent les images, descriptions et pourcentages mondiaux. Le tri se trouve à droite : nom, rareté, état ou déblocage récent.

Le menu **Sélection** propose Tout sélectionner, Tout désélectionner, Inverser et Non débloqués. **Débloquer** et **Verrouiller** préparent des modifications ; **Enregistrer les modifications** les transmet à Steam. **Annuler** rétablit les valeurs chargées. L’édition des statistiques, l’ajout par App ID et les paramètres restent disponibles.

Les images visibles sont chargées en priorité, avec quatre téléchargements simultanés et déduplication des demandes. Un cache local partagé les réutilise entre les vues et les ouvertures. Le cache disque est limité à environ 256 Mo lors du nettoyage au démarrage ; les images décodées en mémoire sont également limitées. Un fichier image corrompu est retéléchargé. Les schémas de succès inchangés sont réutilisés lors des actualisations.

## Achievement Order : déblocage immédiat

1. Sélectionnez des succès, puis **+ Ajouter au panier**. Répétez pour les jeux souhaités.
2. Ouvrez **Achievement Order**. Réorganisez les jeux et les succès par glisser-déposer ou avec Monter / Descendre.
3. Activez au besoin **Préparer plusieurs jeux**, de **2 à 5**. Le panier alterne alors les jeux préparés.
4. Cliquez sur **Démarrer**.

**Aucun délai de déblocage n’est ajouté. Un seul succès est envoyé à la fois, et le suivant attend la confirmation Steam.** La préparation parallèle ne produit pas d’envois simultanés. Les anciens délais enregistrés sont ignorés ; l’ancien onglet de déblocage progressif a été retiré.

L’état Steam est relu avant chaque envoi. Les succès déjà obtenus sont ignorés, les doublons exclus et les succès protégés restent en lecture seule. Enregistrez ou annulez vos changements manuels avant de démarrer le panier.

**Pause**, **Reprendre** et **Arrêter** conservent les succès confirmés. Une pause ou un arrêt attend la réponse d’un envoi déjà parti. Après une fermeture complète, la reprise est manuelle. Les erreurs ou résultats incertains arrêtent le traitement ; l’historique conserve les 2 000 derniers événements.

## GameTime Spoofer

Sélectionnez les jeux de votre bibliothèque, choisissez une durée en **minutes, heures ou jours**, puis démarrez. Avec **Durée aléatoire**, chaque jeu reçoit une durée indépendante entre vos bornes. Sans cette option, la durée choisie est fixe.

Jusqu’à **10 sessions** peuvent fonctionner simultanément. Chaque session affiche son état et le temps restant, s’arrête automatiquement à son échéance et peut être arrêtée séparément ou avec **Tout arrêter**. Une nouvelle session ne duplique pas une session déjà active du même jeu.

Il s’agit de sessions Steam utilisant les App ID, sans lancement des exécutables des jeux. Le minuteur ferme uniquement ses propres sessions : une vue de succès ouverte peut maintenir le jeu actif. **Le temps crédité et son cumul entre plusieurs jeux sont déterminés par Steam, sans garantie de correspondance avec la durée affichée.** GameTime ne reprend pas automatiquement après une fermeture complète de l’application.

## Arrière-plan Windows

La **croix masque la fenêtre dans la zone de notification**, près de l’horloge. Les commandes du panier et GameTime continuent tant que Windows et Steam restent actifs.

Double-cliquez sur l’icône pour rouvrir la même fenêtre. Le menu **Quitter complètement** arrête les sessions GameTime et ferme l’application, après confirmation si des vues ou sessions sont ouvertes. En veille ou à l’arrêt du PC, les tâches ne peuvent pas s’exécuter.

## Dates des succès

L’application ne change pas l’horloge Windows et ne promet pas de dates de déblocage rétroactives. L’API Steam permet de lire les dates mais ne fournit pas de paramètre pour les choisir. [Précision de l’auteur de SAM](https://github.com/gibbed/SteamAchievementManager/issues/534).

## Build et Releases

```powershell
dotnet build SAM.Tests/SAM.Tests.csproj -c Release -p:Platform=x86
./SAM.Tests/bin/x86/Release/net48/SAM.Tests.exe
dotnet build SAM.Launcher/SAM.Launcher.csproj -c Release -p:Platform=x86
```

Le workflow Windows compile, teste le cache, le panier, l’interface, l’hébergement et les sessions simulées, puis vérifie le `.exe` unique et sa réparation de cache. Un tag `v*` publie le `.exe`, le ZIP et les SHA-256 après réussite des tests. Ces tests ne modifient aucun compte Steam réel.

`BetterSteamAchievementManager.exe --demo` ouvre la démonstration sans connexion Steam. GameTime y utilise des sessions simulées ; l’écriture des succès est désactivée.

Code et mentions d’origine conservés dans [LICENSE.txt](LICENSE.txt) et les sources. Les images des jeux appartiennent à leurs titulaires.

Merci à [gibbed](https://github.com/gibbed/SteamAchievementManager) pour le projet original Steam Achievement Manager.
