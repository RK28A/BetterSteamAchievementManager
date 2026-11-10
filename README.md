# BetterSteamAchievementManager

Version modifiée de [Steam Achievement Manager de gibbed](https://github.com/gibbed/SteamAchievementManager/), basée sur le commit `de8b71048a0cee3c3e97cd8535e0f55ca86513e4`. Le code original, son historique et les licences sont conservés.

## Déblocage progressif

1. Lancez Steam et connectez-vous, puis ouvrez `SAM.Picker.exe` et sélectionnez un jeu.
2. Ouvrez l’onglet **Déblocage progressif**.
3. Cochez les succès souhaités ou utilisez **Tout sélectionner**. Seuls les succès verrouillés et modifiables sont proposés, indépendamment des filtres de l’onglet d’origine.
4. Choisissez les délais minimum et maximum en minutes : **3 à 10 minutes par défaut**, de 1 à 1440 minutes. Deux valeurs égales donnent un délai fixe.
5. Cliquez sur **Démarrer**. Un délai aléatoire est tiré avant chaque succès, y compris le premier. Les succès suivent l’ordre affiché ; chaque envoi est confirmé par Steam avant de programmer le suivant.

L’onglet affiche le prochain succès, le compte à rebours, la progression et un journal horodaté. **Arrêter** annule les envois futurs ; un envoi déjà parti attend sa confirmation. Fermer l’application supprime la file. Laissez l’application et Steam ouverts pendant l’exécution. Les réglages et la file ne sont pas sauvegardés entre les sessions.

Les modifications manuelles restent accessibles hors exécution de la file. La file ne valide pas les cases ou statistiques modifiées dans les autres onglets. En cas de refus de Steam, elle s’arrête. Après 60 secondes sans confirmation, les écritures restent bloquées jusqu’à une réponse tardive ou au redémarrage, car le résultat de l’envoi est inconnu.

Ces délais espacent les envois ; ils ne simulent pas le gameplay ni les prérequis ou l’ordre logique des succès.

## Compilation et téléchargement

Application **Windows**, .NET Framework **4.8**, cible **x86**. Avec Visual Studio Build Tools et le SDK .NET installés :

```powershell
dotnet build SAM.sln --configuration Release -p:Platform=x86
```

Les exécutables sont générés dans `upload/`. Le workflow GitHub Actions **Windows build** compile également le projet et fournit l’archive **BetterSteamAchievementManager-Windows-x86** : extraire tous les fichiers ensemble et lancer `SAM.Picker.exe`.

## Vérification manuelle sous Windows

- Vérifier les valeurs initiales 3/10, la sélection vide, minimum supérieur au maximum et le délai fixe.
- Sélectionner deux succès modifiables : vérifier l’attente avant le premier, un seul envoi par échéance, la confirmation puis le second délai.
- Arrêter pendant l’attente : aucun nouvel envoi. Arrêter pendant une confirmation : aucune suite après cette confirmation.
- Vérifier que les succès déjà obtenus et protégés sont exclus, et que les filtres de l’onglet d’origine n’affectent pas la sélection.
- Vérifier les erreurs de lecture/enregistrement et l’absence de confirmation : la file doit s’arrêter sans envoyer le succès suivant.
- Fermer et rouvrir : aucune reprise automatique. Vérifier ensuite les fonctions manuelles d’origine.

---

# Steam Achievement Manager

Steam Achievement Manager (SAM) is a lightweight, portable application used to manage achievements and statistics in the popular PC gaming platform Steam. This application requires the [Steam client](https://store.steampowered.com/about/), a Steam account and network access. Steam must be running and the user must be logged in.

This is the code for SAM. The closed-source version originally released in 2008, last major release in 2011, and last updated in 2013 (a hotfix).

The code is being made available so that those interested can do as they like with it.

There are some changes to the code since the last closed-source release:
- General code maintenance to bring it into a more modern state.
- Icons have been replaced with ones from the Fugue Icons set.
- Version has been bumped to 7.0.x.x to indicate the open-source release.

[Download latest release](https://github.com/gibbed/SteamAchievementManager/releases/latest).

[![Build status](https://ci.appveyor.com/api/projects/status/00vic6jliar6j0ol/branch/master?svg=true)](https://ci.appveyor.com/project/gibbed/steamachievementmanager/branch/master)

## Attribution

Most (if not all) icons are from the [Fugue Icons](https://p.yusukekamiyamane.com/) set.
