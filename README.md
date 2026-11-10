# BetterSteamAchievementManager

Une bibliothèque de succès Windows inspirée de l’interface Steam. Version modifiée de [Steam Achievement Manager de gibbed](https://github.com/gibbed/SteamAchievementManager/), basée sur `de8b71048a0cee3c3e97cd8535e0f55ca86513e4`. Le code, l’historique et les licences d’origine sont conservés.

[**Télécharger la dernière version**](https://github.com/RK28A/BetterSteamAchievementManager/releases/latest) · [Compilations et tests](https://github.com/RK28A/BetterSteamAchievementManager/actions)

## Installation

Windows avec **.NET Framework 4.8**, Steam ouvert et un compte connecté. L’application est compilée en **x86**, également utilisable sur Windows 64 bits.

- **Un seul téléchargement `.exe`** : lancer `BetterSteamAchievementManager.exe`. Le distributeur extrait les fichiers de l’application et les licences dans `%LOCALAPPDATA%\BetterSteamAchievementManager\app-<empreinte>`, puis ouvre la bibliothèque. Il ne demande pas de droits administrateur. Il s’agit d’une distribution en un fichier, pas d’un binaire sans dépendance au framework.
- **ZIP portable** : extraire tous les fichiers ensemble dans un dossier hors du répertoire Steam, puis lancer `SAM.Picker.exe`.

Les sommes SHA-256 sont fournies dans les Releases. Les exécutables ne sont pas signés.

## Bibliothèque et cartes de succès

1. Les jeux s’affichent à gauche, avec recherche et filtres (jeux, démos, mods, autres). **App ID / Ajouter un jeu** ajoute un jeu possédé sans effacer la bibliothèque existante.
2. Cliquez sur un jeu : ses succès apparaissent à droite dans la même fenêtre, avec image, nom, description, état et date de déblocage lorsqu’elle est disponible.
3. Utilisez **Tout sélectionner**, **Tout désélectionner**, **Inverser**, ou **Non débloqués**. Un clic sur une carte ou la touche Espace bascule sa sélection ; Ctrl+A sélectionne les cartes visibles. Les succès protégés restent en lecture seule.
4. **Débloquer** ou **Verrouiller** prépare les changements. Les cartes portent une mention « en attente d’enregistrement ». Sélectionner une carte ne modifie jamais à lui seul un succès.
5. **Enregistrer les modifications** envoie les changements. L’application attend la confirmation de Steam avant de les annoncer comme enregistrés.

La recherche et les filtres conservent les sélections et les changements masqués. Les opérations de sélection portent sur les cartes affichées ; **Tout désélectionner** vide aussi les sélections masquées. **Annuler** rétablit les valeurs de succès et de statistiques chargées. L’onglet **Statistiques**, l’accord d’édition, l’actualisation et la réinitialisation restent accessibles.

Changer de jeu conserve sa vue, sa sélection et sa file dans un processus Steam distinct. Fermer la bibliothèque arrête ses processus après confirmation ; les changements déjà envoyés ne sont pas annulés. Les vues se ferment aussi si leur bibliothèque disparaît. Les sélections et files ne sont pas persistées après fermeture.

## Déblocage progressif actuel

L’onglet **Déblocage progressif** conserve la file actuelle par jeu. Depuis les cartes, le bouton du même nom y transfère la sélection des succès éligibles.

- Délai aléatoire **3 à 10 minutes par défaut**, réglable de 1 à 1440 minutes. Des bornes identiques donnent un délai fixe.
- Un délai avant chaque succès, y compris le premier. Un seul envoi à la fois, dans l’ordre affiché, avec confirmation Steam avant le délai suivant.
- Compte à rebours, journal et bouton **Arrêter**. Arrêter pendant une confirmation laisse terminer l’envoi en cours puis annule la suite.
- Les changements manuels doivent être enregistrés ou annulés avant de démarrer une file. Les autres écritures sont bloquées pendant son exécution.
- Après 60 secondes sans confirmation, les écritures restent bloquées jusqu’à une réponse tardive ou au redémarrage : l’issue de l’envoi est inconnue.

Le panier multi-jeux « Achievement Order », les sliders minutes/heures/jours et le délai entre jeux sont réservés à une prochaine évolution. Les délais actuels espacent les envois ; ils ne simulent pas le gameplay, les prérequis ou l’ordre logique des succès.

## Compiler, tester et publier

Avec le SDK .NET 8 et le pack de ciblage .NET Framework 4.8 :

```powershell
dotnet build SAM.Tests/SAM.Tests.csproj -c Release -p:Platform=x86
./SAM.Tests/bin/x86/Release/net48/SAM.Tests.exe
dotnet build SAM.Launcher/SAM.Launcher.csproj -c Release -p:Platform=x86
```

`upload/` contient l’application portable ; `dist/BetterSteamAchievementManager.exe` est le distributeur en un seul fichier. Compilez toujours l’application avant le distributeur pour embarquer la bonne version.

Le workflow **Windows build** compile et exécute les régressions hors ligne sous Windows, vérifie l’extraction et la réparation du cache du distributeur, et fournit le `.exe`, le ZIP et les sommes SHA-256 en artifacts. Les captures des tests sont dans l’artifact **Windows-UI-test-results**. Pousser un tag `v*` publie ces trois fichiers dans une **GitHub Release**, seulement si les tests passent.

`SAM.Picker.exe --demo` ou `BetterSteamAchievementManager.exe --demo` ouvre une bibliothèque de démonstration sans Steam ni écriture de compte. Les images de démonstration sont générées, pas des données de compte.

## Portée des vérifications

Les régressions automatisées vérifient les sélections, les protections, les filtres, les changements en attente, les délais et confirmations via une API factice, les arrêts et erreurs, le redimensionnement, l’intégration réelle d’un processus de démonstration et son nettoyage. Elles ne valident pas une écriture sur un compte Steam réel. Une vérification manuelle sous Windows avec Steam reste nécessaire pour les différences de jeux, schémas, connexion et affichages à plusieurs facteurs de zoom. Aucun logiciel ne peut être garanti sans bug.

## Licence et attributions

Le logiciel d’origine est de Rick / gibbed, distribué sous licence zlib : [LICENSE.txt](LICENSE.txt). Les modifications sont celles de BetterSteamAchievementManager. Les icônes héritées proviennent principalement de [Fugue Icons](https://p.yusukekamiyamane.com/). Steam et les illustrations des jeux appartiennent à leurs titulaires. Ce projet n’est pas affilié à Valve.
