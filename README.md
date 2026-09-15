# skopi-station

Poste de saisie de mesures ophtalmologiques : application WPF de démonstration
technique sur .NET 8, Entity Framework Core et acquisition via liaison série.

Les données sont **entièrement synthétiques**, générées par Bogus. Aucune donnée
réelle de patient n'est utilisée ni stockée.

## Prérequis

- SDK .NET 8 (`winget install --id Microsoft.DotNet.SDK.8 --exact`)
- SQL Server Express, instance `.\SQLEXPRESS` — voir ci-dessous
- Outils EF Core : `dotnet tool install --global dotnet-ef --version 8.*`

## Comment lancer

### Base de données

L'installation de SQL Server Express est scriptée dans
[`tools/install-sqlexpress.ps1`](tools/install-sqlexpress.ps1), à lancer depuis
une session PowerShell **administrateur** :

```powershell
Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass',`
    '-File','tools\install-sqlexpress.ps1'
```

N'utilisez pas `winget install Microsoft.SQLServer.2022.Express` : son manifeste
épingle le bootstrapper 16.0.1000.6, que Microsoft rejette désormais côté serveur
avec le message « cette version du programme d'installation n'est plus prise en
charge ». Le script passe par le lien de téléchargement officiel, qui sert une
version courante.

SQL Server LocalDB convient également, tout comme un conteneur — le provider EF
Core est le même dans les trois cas, seule la chaîne de connexion change :

```
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=<motdepasse> \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

La chaîne de connexion se trouve dans
`src/SkopiStation.App/appsettings.json`, sous la clé `ConnectionStrings:SkopiStation`.
Elle n'apparaît nulle part dans le code.

### Application

```
dotnet run --project src/SkopiStation.App
```

Au premier démarrage, l'application applique les migrations et peuple la base
avec une cinquantaine de patients et environ trois cents mesures. Le jeu de
données est reproductible : le générateur fixe à la fois la graine Bogus et une
date de référence, Bogus calculant ses dates relatives depuis `DateTime.Now`.

### Migrations

Les migrations sont committées. Pour en ajouter une :

```
dotnet ef migrations add <Name> --project src/SkopiStation.Data --startup-project src/SkopiStation.App
```

Le projet de démarrage est l'application : les outils EF appellent le
`CreateHostBuilder` statique de `App` et résolvent le `DbContext` depuis le
conteneur de l'application. Il n'y a donc pas d'`IDesignTimeDbContextFactory`,
et la chaîne de connexion n'existe qu'à un seul endroit.

## Décisions d'architecture

### Affichage des messages de validation

La validation passe par `INotifyDataErrorInfo` (via `ObservableValidator`), mais
publier une erreur ne suffit pas à la montrer. La façon canonique en WPF est un
`Validation.ErrorTemplate` : un adorner dessiné par-dessus le champ fautif.
À l'usage, cet adorner s'est révélé inexploitable ici — le bouton *Enregistrer*
se désactivait correctement, mais aucun message n'apparaissait, et l'utilisateur
se retrouvait devant un bouton grisé sans explication. Le défaut n'était visible
ni à la compilation ni dans les tests unitaires, qui portent sur le ViewModel et
non sur le rendu ; il a fallu piloter l'interface réelle pour le constater.

Les messages sont donc rendus par des `TextBlock` placés dans le flux normal de
la mise en page, sous chaque champ, liés à l'élément de saisie :

```xml
<TextBox x:Name="RecordNumberBox" Text="{Binding RecordNumber, ...}" />
<TextBlock Text="{Binding ElementName=RecordNumberBox,
                          Path=(Validation.Errors)/ErrorContent}" />
```

La syntaxe `(Validation.Errors)/ErrorContent` — la barre oblique désignant
l'élément courant de la collection — évite l'indexation `[0]`, qui produit un
échec de binding dès que le champ redevient valide et que la collection se vide.
Les messages étant dans l'arbre visuel plutôt que dans la couche d'adorners, ils
sont aussi exposés aux outils d'accessibilité.

### Valeur impossible et valeur hors plage

Chaque type de mesure porte deux intervalles, qui répondent à deux questions
différentes et ne doivent pas être confondus.

`ReferenceRange` est la plage de référence clinique — 10 à 21 mmHg pour la
pression intraoculaire. Une valeur en dehors est une **mesure valide** : elle est
enregistrée, et signalée par `IsOutOfRange`. C'est précisément le genre de valeur
qu'on veut voir, pas jeter.

`PlausibleRange` est l'enveloppe de ce qu'un appareil en état de marche peut
physiquement rapporter — 1 à 80 mmHg. Une valeur en dehors n'est pas une mesure
mais une trame corrompue ou un appareil défaillant : elle est **rejetée** par le
parser, journalisée, et n'atteint jamais la base.

Les deux intervalles sont indicatifs, choisis pour la démonstration.

### Parsing numérique : pourquoi pas `NumberStyles.Number`

Les valeurs des trames sont lues avec
`NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign`, et non avec le
`NumberStyles.Number` qu'on écrit par réflexe. `Number` inclut `AllowThousands`,
et le séparateur de milliers de la culture invariante est **la virgule**. Une
trame contenant `2,5` est alors lue comme `25` sans la moindre erreur.

Sur une longueur axiale, `25 mm` est physiquement plausible *et* dans la plage de
référence. La valeur fausse traverse donc toutes les vérifications et se retrouve
en base, attribuée à un patient, sans rien pour la distinguer d'une mesure
correcte. C'est plus grave qu'un plantage : un plantage se voit et se corrige,
une donnée fausse et silencieuse se propage. Un appareil configuré en locale
française, ou une trame corrompue sur un octet, suffit à la produire.

### Déconnexion série : fermer le port avant d'attendre la boucle

`SerialPort.BaseStream` **n'observe pas le `CancellationToken`**. Annuler le
jeton puis attendre la boucle de lecture ne suffit donc pas : le `ReadLineAsync`
en cours reste bloqué jusqu'à ce qu'un octet arrive — c'est-à-dire indéfiniment
si l'appareil s'est tu ou a été débranché. `DisconnectAsync` gelait, et avec lui
l'interface au clic sur *Déconnecter*.

`DisconnectAsync` ferme donc le port **avant** d'attendre la boucle : c'est la
fermeture qui avorte la lecture en cours et permet à la boucle de se terminer.
La boucle traite l'erreur d'entrée-sortie qui en résulte comme une sortie normale
dès lors que l'annulation a été demandée, faute de quoi un arrêt volontaire
laisserait l'appareil en état `Faulted`.

### Ce qui n'est pas couvert par les tests

L'**ouverture du port série lui-même** n'est pas testée automatiquement : cela
demanderait une paire de ports virtuels (`com0com` sous Windows), donc un pilote
et une installation privilégiée sur la machine de test comme sur le runner de CI.

Le contournement tient en deux parties. La boucle de lecture — qui porte
l'essentiel de la logique : découpage en lignes, rejet des trames invalides,
annulation — est exercée sur un `TextReader` via un `PumpingDevice` défini dans
le projet de tests, qui dérive de `MeasurementDeviceBase` et emprunte exactement
le même `PumpAsync` que l'implémentation série. Le reste — ouverture réelle,
échec sur un port inexistant, déconnexion sans blocage — a été vérifié
manuellement avec un harnais jetable contre le port `COM1` de la machine de
développement. C'est d'ailleurs ce harnais qui a révélé le blocage décrit
ci-dessus, qu'aucun test unitaire n'aurait vu.

## Tests

```
dotnet test
```

## Avertissement

Les plages de référence utilisées pour signaler une mesure hors norme sont
**indicatives, choisies pour la démonstration, et non cliniquement validées**.
