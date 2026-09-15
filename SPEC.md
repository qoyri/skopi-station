# SPEC — skopi-station : poste de saisie de mesures (WPF)

## Contexte et objectif

Application WPF de démonstration technique. Elle doit prouver la maîtrise de
WPF/MVVM sur .NET 8, d'Entity Framework Core sur SQL Server, et de
l'intégration d'un équipement de mesure via liaison série.

Public : un lead technique qui va lire le README puis survoler le code en
dix minutes. **La qualité et la lisibilité comptent plus que le nombre de
fonctionnalités.**

Périmètre volontairement réduit : deux écrans, terminés et propres.

### Non-objectifs

- Pas d'authentification, de multi-utilisateur, de rôles
- Pas d'API web, pas de déploiement
- Pas de « clone » d'un logiciel médical existant : c'est une démo technique
- Aucune donnée réelle de patient. **Uniquement des données synthétiques.**

---

## Stack imposée

| Rôle | Choix |
|---|---|
| Framework | .NET 8, WPF |
| MVVM | `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`) |
| ORM | `Microsoft.EntityFrameworkCore.SqlServer` |
| Base | SQL Server LocalDB |
| DI / hôte | `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` |
| Série | `System.IO.Ports` |
| Données de test | `Bogus` |
| Tests | `xUnit` + `FluentAssertions` 7.2.2 (dernière version Apache 2.0 ; la v8 est sous licence commerciale) |

Ne pas ajouter d'autres dépendances sans nécessité. Pas de framework MVVM
tiers (Prism, Caliburn), pas de bibliothèque de contrôles payante.

---

## Structure de la solution

```
SkopiStation.sln
src/
  SkopiStation.Domain/          entités, pas de dépendance externe
  SkopiStation.Data/            DbContext, configurations, migrations, repositories
  SkopiStation.Devices/         abstraction série + implémentation + simulateur
  SkopiStation.App/             WPF : Views, ViewModels, Converters, App.xaml.cs
tests/
  SkopiStation.Tests/           tests unitaires (domaine, ViewModels, parsing série)
tools/
  SerialSimulator/       console qui émet des trames sur un port série
```

### Cibles

| Projet | Cible |
|---|---|
| `SkopiStation.Domain`, `SkopiStation.Data` | `net8.0` — couche métier indépendante de la plateforme |
| `SkopiStation.App`, `SkopiStation.Tests` | `net8.0-windows` |

La cible reste .NET 8 même si le SDK installé est plus récent. Solution au
format `.sln`. `SkopiStation.Devices` et `tools/SerialSimulator` sont créés à
l'étape 3 (pas de projet vide d'ici là).

---

## Modèle de données

```
Patient
  Id (Guid), LastName, FirstName, BirthDate, RecordNumber (unique), CreatedAt

Measurement
  Id (Guid), PatientId (FK), TakenAt,
  Kind (enum: IntraocularPressure, AxialLength, CornealThickness),
  Value (decimal), Unit (string), Source (enum: Manual, Device),
  IsOutOfRange (calculé, non mappé)
```

- Configuration via `IEntityTypeConfiguration<T>`, jamais d'attributs sur les entités
- Index unique sur `RecordNumber`
- Migrations générées et **committées** dans le repo
- Seed au premier lancement : ~50 patients et ~300 mesures via Bogus,
  avec un `Seed` fixe **et une date de référence fixe** pour que les données
  soient reproductibles (Bogus calcule les dates relatives depuis
  `DateTime.Now`, le seed seul ne suffit pas)
- `RecordNumber` : `SK-` suivi de 6 chiffres (ex. `SK-004217`)
- `Unit` est persisté. Chaque `Kind` a une unité canonique et une plage de
  référence, sur laquelle se base `IsOutOfRange` :

| Kind | Unité canonique | Plage de référence |
|---|---|---|
| `IntraocularPressure` | mmHg | 10–21 |
| `AxialLength` | mm | 22–25 |
| `CornealThickness` | µm | 500–600 |

Ces plages sont **indicatives, choisies pour la démonstration, et non
cliniquement validées**. Le README doit le dire explicitement.

---

## Écran 1 — Liste des patients

- `DataGrid` (ou `ListView`) des patients : numéro de dossier, nom, prénom,
  date de naissance, nombre de mesures
- Champ de recherche filtrant sur nom, prénom et numéro de dossier
- Tri par clic sur en-tête de colonne
- Sélection d'une ligne → panneau de détail à droite
- Le détail affiche l'identité et l'historique des mesures du patient
- Formulaire d'édition de l'identité, avec validation

**Contrainte technique :** le filtre et le tri passent par `ICollectionView`
(`CollectionViewSource.GetDefaultView` + `Filter` + `SortDescriptions`).
Ne pas reconstruire la collection à chaque frappe.

---

## Écran 2 — Acquisition depuis l'appareil

- Sélection du port COM (liste des ports disponibles), bouton Connecter /
  Déconnecter, indicateur d'état
- Les mesures reçues s'affichent en direct dans une liste
- Bouton pour rattacher une mesure reçue au patient sélectionné et
  l'enregistrer en base
- Saisie manuelle possible, avec `Source = Manual`
- Un graphique simple de l'historique d'un type de mesure pour le patient
  courant — **optionnel, à couper en dernier si le temps manque**

### Protocole série (simulateur)

Trames texte, une par ligne, terminées par `\n` :

```
MEAS|<KIND>|<VALUE>|<UNIT>|<ISO8601>
MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z
```

- Parsing isolé dans une classe testable (`MeasurementFrameParser`),
  couverte par des tests unitaires incluant les trames malformées
- Une trame invalide est journalisée et ignorée, elle ne fait pas tomber l'app
- L'unité reçue est validée contre l'unité canonique du `Kind` : une trame
  dont l'unité ne correspond pas (ex. `kPa` pour une pression intraoculaire)
  est rejetée avec un log explicite
- **Valeur impossible ≠ valeur hors plage.** Une valeur physiquement
  impossible (négative, absurde) rend la trame invalide : elle est rejetée.
  Une valeur hors plage de référence clinique est une mesure valide,
  enregistrable, simplement signalée par `IsOutOfRange`

### Abstraction

```csharp
public interface IMeasurementDevice
{
    event EventHandler<MeasurementReceivedEventArgs> MeasurementReceived;
    event EventHandler<DeviceStateChangedEventArgs> StateChanged;
    Task ConnectAsync(string portName, CancellationToken ct);
    Task DisconnectAsync();
}
```

Deux implémentations : `SerialMeasurementDevice` (réelle) et
`FakeMeasurementDevice` (émet des mesures en mémoire, pour les tests et pour
une démo sans matériel). Choix par configuration.

---

## Exigences de qualité — critères d'acceptation

Ces six points sont l'objet même du projet. Chacun doit être
implémenté et **expliqué dans le README**.

1. **Marshalling vers le thread UI.** Les mesures arrivent sur un thread de
   fond. Utiliser `BindingOperations.EnableCollectionSynchronization` sur la
   collection observable concernée, ou passer par le `Dispatcher`. Justifier
   le choix retenu. Aucun `InvalidOperationException` de cross-thread.

2. **Durée de vie du DbContext.** Injecter `IDbContextFactory<SkopiStationDbContext>`
   et créer un contexte court par opération. Pas de `DbContext` long-vivant
   dans un ViewModel. Expliquer pourquoi (change tracking qui s'accumule,
   données obsolètes, absence de thread-safety).

3. **Aucune logique dans le code-behind.** Les fichiers `.xaml.cs` ne
   contiennent que `InitializeComponent()`. Toute la logique est dans les
   ViewModels, liée par binding et commandes. Seule exception :
   `App.xaml.cs`, racine de composition (hôte, DI, démarrage) ; c'est `App`
   qui affecte les `DataContext`, pas les vues.

4. **Validation via `INotifyDataErrorInfo`**, pas `IDataErrorInfo`
   (obsolète). Champs requis, format du numéro de dossier, date de naissance
   cohérente.

5. **Au moins un `IValueConverter`** — par exemple pour colorer une mesure
   hors plage de référence.

6. **Asynchrone correct.** `async`/`await` jusqu'au bout, `CancellationToken`
   sur la connexion, aucun `.Result` ni `.Wait()`, aucun `async void` sauf
   gestionnaire d'événement. Le démarrage asynchrone passe par l'événement
   `Startup`, pas par un `override async void OnStartup`.

---

## Tests

Priorité : le parsing de trames, la validation, la logique de ViewModel.
Pas de test d'interface graphique.

- `MeasurementFrameParser` : trames valides, malformées, champs manquants,
  valeurs physiquement impossibles (rejetées), valeurs hors plage de
  référence (acceptées), unité ne correspondant pas au type, dates invalides
- Validation patient : cas limites
- `PatientListViewModel` : le filtre retourne les bons éléments
- `AcquisitionViewModel` avec `FakeMeasurementDevice` : une mesure reçue
  arrive bien dans la collection

Viser une couverture utile, pas un pourcentage.

---

## CI

Un workflow GitHub Actions (`.github/workflows/ci.yml`) sur `push` et
`pull_request` : `dotnet restore`, `dotnet build --configuration Release`,
`dotnet test`. Le job doit être bloquant. Runner `windows-latest` (App et
Tests ciblent `net8.0-windows`).

---

## README

C'est la pièce que le lecteur verra en premier. Il doit contenir :

1. **Pourquoi ce projet** — une phrase honnête : transposition d'acquis
   XAML/MVVM issus de .NET MAUI vers WPF, avec intégration d'un appareil de
   mesure via liaison série.
2. **Comment lancer** — prérequis, LocalDB, migrations, et comment démarrer
   le simulateur série (mentionner `com0com` pour créer une paire de ports
   virtuels sous Windows).
3. **Décisions d'architecture** — les six points ci-dessus, expliqués en
   deux ou trois phrases chacun. Le *pourquoi*, pas le *comment*. S'y
   ajoutent :
   - la couche métier (Domain, Data) en `net8.0`, indépendante de la
     plateforme ; seuls App et Tests dépendent de Windows
   - la distinction valeur impossible / valeur hors plage, et la validation
     de l'unité reçue
   - le choix de FluentAssertions 7.2.2 et sa raison (licence)
   - la mention que les plages de référence sont indicatives, choisies pour
     la démonstration, et non cliniquement validées
4. **Données** — entièrement synthétiques, générées par Bogus. Aucune donnée
   réelle de patient. Le dire explicitement.
5. **Limites et suite** — ce qui manquerait en production : journal d'audit
   des accès, chiffrement au repos, gestion des déconnexions d'appareil et
   des reprises, authentification. Montrer qu'on connaît les limites de son
   propre projet.
6. Deux ou trois captures d'écran.

Ton : sobre et factuel. Pas de superlatifs, pas d'emoji.

---

## Plan de travail

**Étape 1 — Socle.** Solution, projets, DI avec `IHost`, `SkopiStationDbContext`,
entités, configurations, première migration, seed Bogus. Critère : l'app
démarre et la base est créée.

**Étape 2 — Écran patients.** Liste, recherche via `ICollectionView`, tri,
détail, édition avec validation. Critère : navigable et fonctionnel.

**Étape 3 — Appareil.** Abstraction, simulateur console, parser + tests,
implémentation série.

**Étape 4 — Écran acquisition.** Connexion, réception temps réel avec
marshalling correct, rattachement au patient, enregistrement.

**Étape 5 — Finition.** Tests restants, CI, README, captures. Le graphique
seulement s'il reste du temps.

Committer à chaque étape, messages de commit clairs et en anglais.

---

## Style

- C# 12, `nullable` activé, warnings traités comme importants
- Nommage et commentaires **en anglais** dans le code ; le README en anglais
  ou en français, au choix, mais cohérent
- Commenter uniquement ce qui n'est pas évident : les six décisions
  d'architecture méritent un commentaire, un getter non
- Pas de région `#region`, pas de code mort, pas de `TODO` laissé en place
