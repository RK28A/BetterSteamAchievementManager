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

Le panier regroupe les jeux et leurs succès avec leurs images, exclut les doublons et les succès protégés ou déjà obtenus au moment de l’ajout. Il est enregistré automatiquement. En mode séquentiel, un seul succès est envoyé à la fois ; la commande attend la confirmation de Steam avant de poursuivre. L’état Steam est relu avant chaque envoi, y compris à la reprise : un succès déjà obtenu est ignoré sans nouvelle écriture.

Le planning affiche la durée restante minimum/maximum et une fourchette de fin estimée, hors réponses Steam et pauses manuelles. Les délais de chaque succès et les pauses entre jeux sont inclus.

**Pause** sauvegarde le délai restant. Si un envoi est déjà parti, la pause attend sa confirmation. **Reprendre** continue avec ce délai, y compris après avoir fermé puis rouvert l’application. Le temps hors ligne ne raccourcit pas l’attente. Une modification du panier ou de ses délais annule ce point de reprise et recalcule le planning. Une fermeture brutale peut conserver jusqu’à une seconde d’attente supplémentaire.

**Arrêter** remet le délai à zéro pour un prochain démarrage et annule les envois futurs ; un envoi déjà parti attend sa confirmation. Le mode séquentiel se reprend manuellement au démarrage. Le mode simultané décrit ci-dessous dispose de sa propre reprise automatique. Les éléments terminés restent visibles et ne sont pas renvoyés au prochain départ. Les modifications du panier sont bloquées pendant son exécution. En cas d’erreur ou d’absence de confirmation, la commande s’arrête ; vérifiez la vue du jeu avant de réessayer.

L’ancienne file par jeu reste dans **Déblocage progressif**. Enregistrez ou annulez les modifications manuelles d’un jeu avant de l’utiliser dans une commande. Gardez Steam et la bibliothèque ouverts pendant l’exécution.

## Historique

Le bouton **Historique** affiche les démarrages, pauses, reprises, confirmations Steam, erreurs et fins de commande, avec le jeu, le succès et l’heure locale. Les 2 000 derniers événements sont conservés dans le panier local. Un résultat incertain reste signalé comme erreur ; il n’est pas présenté comme un succès confirmé.

## Statut Steam et temps de jeu

Ouvrir une vue de jeu crée une session Steam pour son App ID, sans lancer son exécutable. Steam peut alors afficher le compte « en jeu ». Plusieurs vues peuvent rester ouvertes simultanément. **Valider des succès n’ajoute pas une durée de jeu précise** : le temps enregistré est déterminé par Steam, et son cumul pour plusieurs sessions simultanées n’est pas garanti par cette application.

## Order simultané : objectifs par jeu

Dans **Achievement Order**, cliquez sur **Mode simultané**. Cette vue affiche uniquement les jeux, leur image, les succès connus et le temps Steam disponible dans le cache local du compte connecté.

1. Choisissez **1 à 10 jeux simultanés**. Les autres jeux configurés attendent qu’une place se libère.
2. Pour chaque jeu, chargez la moyenne **100 % / Completionist de HowLongToBeat**. Une correspondance certaine préremplit l’objectif total en heures ; vous pouvez le modifier. La fiche source reste accessible. L’accès automatique peut être refusé ou changer : dans ce cas, « indisponible » est affiché et la saisie manuelle reste possible. Aucune moyenne n’est inventée. Une fiche HLTB `/game/…` peut aussi être indiquée directement.
3. Réglez l’**objectif total en heures**. Le temps Steam déjà connu est soustrait : un objectif de 40 h avec 15 h connues donne une session de 25 h. Sans donnée Steam, la valeur correspond aux heures à effectuer. Le cache Steam peut être ancien ; la progression locale ne garantit pas le crédit Steam final.
4. Définissez le début et la fin de la période de déblocage en **heures actives de session**, puis **Enregistrer ce jeu**. Exemple : entre la 2e et la 24e heure d’une session de 25 h. Les succès choisis sont mélangés et répartis aléatoirement dans cette période. Cette période est relative à la session, pas à l’horloge du PC.
5. Cliquez sur **Démarrer / reprendre**. Plusieurs jeux peuvent envoyer un succès simultanément ; chaque jeu attend sa propre confirmation avant son envoi suivant. Le planning tiré au sort est sauvegardé et reste identique après une reprise.

Une session continue jusqu’à son objectif, même si ses succès sont déjà terminés. Lorsque le temps et les succès sont terminés, ses processus se ferment. En cas de réponse Steam incertaine, le jeu concerné se met en pause ; les autres continuent. À la reprise, l’état Steam est relu pour éviter de renvoyer un succès déjà obtenu. Réenregistrer la configuration d’un jeu recrée son planning local ; les succès déjà confirmés restent acquis.

## GameTime Spoofer

Sélectionnez vos jeux et saisissez une **durée en heures dans une zone de texte**, avec décimales (ex. `1,5`). L’option aléatoire utilise deux valeurs : minimum et maximum. Chaque jeu reçoit sa propre durée. Jusqu’à **20 sessions GameTime** peuvent fonctionner en parallèle, en plus des **10 jeux maximum dans Order**. Un même jeu ne peut pas être lancé à la fois dans les deux modes.

Chaque session s’arrête à son objectif. **Arrêter**, **Tout arrêter** et **Reprendre** permettent de gérer les sessions conservées. Le compteur démarre après la connexion à Steam ; il mesure le temps actif du processus, sans compter les arrêts ni les longues suspensions. Le temps est enregistré chaque seconde, avec une perte possible d’environ une seconde lors d’un arrêt brutal.

Ces sessions utilisent les App ID sans lancer les exécutables des jeux. **Le temps crédité et son cumul entre plusieurs jeux restent déterminés par Steam.** Une vue de succès ouverte séparément peut maintenir ce jeu actif après l’arrêt de sa session GameTime.

## Arrière-plan et reprise

- La **croix** masque la fenêtre ; les tâches continuent via l’icône de la zone de notification Windows. Un double-clic la rouvre.
- Démarrer GameTime ou Order simultané active **Reprise au démarrage de Windows**, désactivable dans le menu de cette icône. L’application enregistre uniquement une entrée de démarrage pour l’utilisateur courant, sans droits administrateur.
- Au démarrage de Windows, l’application attend Steam puis reprend ses objectifs enregistrés. Un superviseur relance l’application après une fermeture anormale quand la reprise est activée. Une session isolée interrompue est réessayée après 30 secondes.
- **Quitter** ferme volontairement l’application et ses sessions ; le superviseur ne la relance pas. La progression reste disponible pour le prochain lancement. **Tout mettre en pause** désactive la reprise des tâches en cours.
- Un PC éteint ou en veille ne fait pas progresser ces tâches. Elles peuvent tourner en continu tant que Windows et Steam restent actifs. Aucune date système ni date Steam n’est modifiée.

Les données sont dans `%LOCALAPPDATA%\BetterSteamAchievementManager`. Pour le ZIP portable, conservez le dossier au même emplacement si la reprise Windows est activée. Le `.exe` unique utilise son dossier extrait persistant. Le mode `--demo` n’ajoute aucune entrée au démarrage de Windows et n’écrit pas sur Steam.

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
