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

## Tests

```
dotnet test
```

## Avertissement

Les plages de référence utilisées pour signaler une mesure hors norme sont
**indicatives, choisies pour la démonstration, et non cliniquement validées**.
