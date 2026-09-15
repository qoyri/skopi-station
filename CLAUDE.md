# CLAUDE.md — skopi-station

La spécification complète est dans `SPEC.md`. Ce fichier rappelle ce qui ne doit pas dériver.

## Méthode

- Suivre le plan de travail de `SPEC.md` étape par étape ; s'arrêter à la fin de chaque étape pour validation.
- Un commit par étape, message en anglais.
- Code, nommage et commentaires en anglais. Pas de `#region`, pas de code mort, pas de `TODO`.

## Cibles par projet

| Projet | Cible |
|---|---|
| `SkopiStation.Domain`, `SkopiStation.Data` | `net8.0` (C# pur, indépendant de la plateforme) |
| `SkopiStation.App`, `SkopiStation.Tests` | `net8.0-windows` |
| `SkopiStation.Devices`, `tools/SerialSimulator` | `net8.0` (`System.IO.Ports` est multiplateforme) |

La cible reste .NET 8 même si le SDK installé est plus récent. Solution au format `.sln` (pas `.slnx`).
CI sur `windows-latest`.

## Les six critères d'acceptation

1. **Thread UI** : les mesures arrivent sur un thread de fond → `EnableCollectionSynchronization` ou `Dispatcher`, choix justifié. Aucune exception cross-thread.
2. **DbContext** : `IDbContextFactory<SkopiStationDbContext>`, un contexte court par opération, jamais de contexte long-vivant dans un ViewModel.
3. **Code-behind vide** : les `.xaml.cs` ne contiennent que `InitializeComponent()`. Seule exception : `App.xaml.cs`, racine de composition (hôte, DI, démarrage). C'est `App` qui affecte les `DataContext`, pas les vues.
4. **Validation** via `INotifyDataErrorInfo` (pas `IDataErrorInfo`) : champs requis, format du numéro de dossier, date de naissance cohérente.
5. **Au moins un `IValueConverter`** (ex. couleur d'une mesure hors plage).
6. **Async correct** : `async`/`await` de bout en bout, `CancellationToken` sur la connexion, ni `.Result` ni `.Wait()`, `async void` uniquement pour les gestionnaires d'événements (le démarrage passe par l'événement `Startup`, pas par un `override async void OnStartup`).

## Décisions validées

- **Base de données** : **SQL Server Express** en installation native, instance `.\SQLEXPRESS`.
  LocalDB a été écarté (aucun paquet winget, installation non scriptable) et Docker aussi
  (WSL 2 + Docker Desktop = un redémarrage et de l'outillage à configurer, pour zéro gain
  sur ce que le projet démontre). Le provider EF Core reste `SqlServer` dans les trois cas,
  donc le choix n'a aucun impact technique. Le README dit « SQL Server Express (ou LocalDB) ».
- **Chaîne de connexion** : jamais en dur dans le code. Elle vit dans
  `src/SkopiStation.App/appsettings.json`, sous la clé `ConnectionStrings:SkopiStation`.
- **Pas de `IDesignTimeDbContextFactory`**. Les migrations se génèrent avec `SkopiStation.App`
  comme projet de démarrage : les outils EF appellent le `CreateHostBuilder` statique de `App`
  et résolvent le `DbContext` depuis le conteneur de l'application. Une seule chaîne de
  connexion, une seule composition.

  ```
  dotnet ef migrations add <Name> --project src/SkopiStation.Data --startup-project src/SkopiStation.App
  ```

- **Plages de référence** (indicatives) : pression intraoculaire 10–21 mmHg, longueur axiale 22–25 mm, épaisseur cornéenne 500–600 µm. Définies dans `MeasurementKindExtensions`.
- **Unité** : `Unit` reste persisté. Chaque `Kind` a une unité canonique (mmHg, mm, µm). Une trame dont l'unité ne correspond pas à l'unité canonique du type (ex. kPa pour une pression) est rejetée avec un log explicite.
- **Valeur impossible ≠ valeur hors plage** : une valeur physiquement impossible rend la trame invalide (rejetée, journalisée) ; une valeur hors plage clinique est une mesure valide, enregistrable, signalée par `IsOutOfRange`.
- **Numéro de dossier** : `SK-` suivi de 6 chiffres (`SK-004217`), voir `RecordNumberFormat`.
- **Seed Bogus** : seed fixe **et** date de référence fixe (Bogus calcule les dates relatives depuis `DateTime.Now`, le seed seul ne suffit pas).
- **FluentAssertions 7.2.2** : dernière version sous licence Apache 2.0 (la v8 est sous licence commerciale).

## À mettre dans le README (section décisions)

- Les six critères ci-dessus, le *pourquoi* en deux ou trois phrases chacun.
- La couche métier (Domain, Data) est en `net8.0`, indépendante de la plateforme ; seuls App et Tests dépendent de Windows.
- Le choix de SQL Server Express plutôt que LocalDB ou Docker, et le fait que le provider EF Core est le même dans les trois cas. Donner le `docker run` en alternative pour qui préfère un conteneur.
- L'absence de design-time factory : une seule chaîne de connexion, dans `appsettings.json`. Donner la commande `dotnet ef` exacte.
- La distinction valeur impossible / valeur hors plage, et la validation de l'unité reçue.
- La raison du choix de FluentAssertions 7.2.2 (licence).
- **Obligatoire** : les plages de référence sont indicatives, choisies pour la démonstration, et non cliniquement validées.
