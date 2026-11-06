# Horloge pour Windows

Une application d’horloge pour Windows, inspirée de l’interface de l’iPhone. Retrouvez un chronomètre, des minuteurs, des alarmes et les heures du monde dans une interface sombre aux accents orange.

## Fonctionnalités

- **Chronomètre** : démarrage, pause, reprise et enregistrement des tours, avec indication des tours les plus rapides et les plus lents.
- **Minuteurs** : durée personnalisable, plusieurs minuteurs simultanés, anneau de progression, pause et reprise, et accès aux durées récentes.
- **Alarmes** : nom, heure, jours de répétition, choix de sonnerie et rappel après 9 minutes.
- **Horloges du monde** : ajout de villes et affichage de leur heure locale.
- **Interface sombre** : navigation par onglets, commandes arrondies, barre de titre noire et interface redimensionnée en plein écran.

## Utilisation

1. Sur Windows 10 ou 11, ouvrez `Horloge-adaptive.exe`.
2. Choisissez une fonction dans la barre en bas de la fenêtre.
3. Utilisez le bouton **+** pour ajouter une alarme, une ville ou un minuteur personnalisé.

Aucune installation n’est nécessaire. L’exécutable peut être utilisé seul, avec **.NET Framework 4.8** installé sur le PC. Dans l’onglet Chronomètre, la touche **Espace** démarre ou arrête le chronomètre.

### Alarmes et minuteurs

Pour sonner à l’heure prévue, **l’application doit rester ouverte et le PC éveillé**. Vous pouvez réduire la fenêtre : les décomptes et alarmes continuent. Si le PC se met en veille pendant une échéance, celle-ci sonne au retour tant que l’application est restée ouverte. Les sonneries sont des tonalités originales et non les sons Apple.

## Données

Les réglages sont enregistrés localement dans :

```text
%LOCALAPPDATA%\HorlogeWindows\settings.xml
```

Le fichier contient notamment les alarmes, minuteurs, durées récentes et villes. Conservez-le pour garder vos réglages en changeant de PC.

## Compiler depuis les sources

Le dossier `Source` contient le code C#, l’icône et le script de compilation. Sur Windows avec .NET Framework 4.8 :

```powershell
.\Source\build.ps1
```

Le script crée `Horloge-adaptive.exe` dans le dossier parent de `Source`.

## À propos

Projet indépendant inspiré de l’application Horloge sur iPhone. Il n’est ni développé ni affilié à Apple.
