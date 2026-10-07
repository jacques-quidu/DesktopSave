# DesktopSave

Application WinUI 3 (.NET 8) permettant de **sauvegarder et restaurer la position des icônes du bureau Windows 11**.

Chaque sauvegarde est stockée dans un fichier JSON **distinct par résolution d'écran**, afin de retrouver la bonne disposition après un changement de résolution ou le branchement/débranchement d'un écran.

## Fonctionnalités

- Lecture en temps réel des icônes du bureau et de leurs coordonnées.
- Sauvegarde de la disposition courante au format JSON.
- Restauration de la disposition correspondant à la résolution active.
- Ouverture du dossier de sauvegardes depuis l'application.

## Emplacement des sauvegardes

| Mode | Dossier |
| --- | --- |
| Non packagé | `%LOCALAPPDATA%\DesktopSave` |
| Packagé (MSIX) | `ApplicationData.Current.LocalFolder\DesktopSave` |

Nom des fichiers : `layout_{largeur}x{hauteur}.json`, par exemple `layout_1920x1080.json`.

## Architecture

| Fichier | Rôle |
| --- | --- |
| `Models/DesktopLayout.cs` | Modèles `DesktopIcon` et `DesktopLayout`. |
| `Services/DesktopIconService.cs` | Interop Win32 avec le `SysListView32` du bureau (lecture/écriture des positions). |
| `Services/DesktopLayoutStore.cs` | Résolution de l'écran et persistance JSON. |
| `Services/DesktopLayoutJsonContext.cs` | Sérialisation source-generated (compatible trimming). |
| `MainWindow.xaml(.cs)` | Interface utilisateur. |

## Fonctionnement technique

Les positions sont lues et appliquées via les messages `LVM_GETITEMTEXTW`, `LVM_GETITEMPOSITION` et `LVM_SETITEMPOSITION` envoyés au ListView du Shell. Comme ces messages échangent des pointeurs, un tampon est alloué dans le processus `explorer.exe` (`VirtualAllocEx` / `ReadProcessMemory` / `WriteProcessMemory`).

## Prérequis

- Windows 10 1809 (17763) ou supérieur, Windows 11 recommandé.
- .NET 8 SDK et la charge de travail « Développement d'applications de bureau .NET » / Windows App SDK.

## Compilation et exécution

```powershell
dotnet build .\DesktopSave\DesktopSave\DesktopSave.csproj
dotnet run --project .\DesktopSave\DesktopSave\DesktopSave.csproj
```

> L'application doit s'exécuter au même niveau d'intégrité qu'`explorer.exe` (processus **non élevé**) pour pouvoir manipuler les icônes du bureau.

## Remarques

- Pour que la restauration fonctionne, l'option « Aligner les icônes sur la grille » ne doit pas replacer les éléments : la disposition automatique (`Réorganiser automatiquement les icônes`) doit être **désactivée** dans le menu contextuel du bureau.
- Les icônes sont identifiées par leur libellé ; une icône renommée ou supprimée ne sera pas repositionnée.
