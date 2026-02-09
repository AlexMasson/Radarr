# Radarr Copilot Instructions

## Architecture Overview

Radarr is a .NET 6 monolithic application with a React/Redux frontend for automated movie management. The codebase uses the historical "NzbDrone" namespace.

### Backend Structure (`src/`)
- **NzbDrone.Core** - Business logic, domain models, services, repositories (~50 subfolders: Movies, Download, DecisionEngine, Indexers, etc.)
- **NzbDrone.Host** - Application bootstrap, DI container (DryIoc), ASP.NET Core hosting
- **Radarr.Api.V3** - REST API controllers with SignalR for real-time updates
- **Radarr.Http** - HTTP infrastructure, REST base classes, validation, attributes
- **NzbDrone.Common** - Cross-cutting utilities (disk, networking, serialization)

### Frontend Structure (`frontend/src/`)
- React + TypeScript with Redux for state management
- Component structure mirrors API resources (Movie/, Settings/, Activity/)
- CSS Modules for styling (`.css` files with `.d.ts` type declarations)

## Key Patterns

### Provider Pattern (Extensibility)
Indexers, Download Clients, Notifications, Import Lists follow `IProvider`:
```
src/NzbDrone.Core/ThingiProvider/IProvider.cs  - Base interface
src/NzbDrone.Core/Indexers/                    - Example implementation
```
Each provider: Definition (DB model) + Settings (config) + Implementation.

### Repository Pattern
All data access uses `BasicRepository<TModel>` with Dapper + SQLite:
```csharp
public class MovieRepository : BasicRepository<Movie>, IMovieRepository
```
Models inherit from `ModelBase` (provides `Id`).

### Command/Event Messaging
- Commands (async tasks): Inherit from `Command`, processed by `IExecute<T>` handlers
- Events: Published via `IEventAggregator`, handled by `IHandle<T>` implementations
```csharp
// src/NzbDrone.Core/Movies/Commands/RefreshMovieCommand.cs
public class RefreshMovieCommand : Command { }

// src/NzbDrone.Core/Movies/RefreshMovieService.cs
public class RefreshMovieService : IExecute<RefreshMovieCommand>
```

### API Controllers
Controllers use `[V3ApiController]` attribute for routing `/api/v3/[controller]`:
```csharp
[V3ApiController]
public class MovieController : RestControllerWithSignalR<MovieResource, Movie>
```
- `RestResource` = API DTO (must have `Id` property)
- `RestControllerWithSignalR` = automatic SignalR broadcasting on changes

### Decision Engine (Release Selection)
Release evaluation flows through specifications in `NzbDrone.Core/DecisionEngine/Specifications/`:
```csharp
public class QualityAllowedByProfileSpecification : IDownloadDecisionEngineSpecification
{
    public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
    {
        return DownloadSpecDecision.Accept();  // or Reject(reason, args)
    }
}
```
Each spec returns `Accept()` or `Reject()` with rejection reason.

## Development Commands

```bash
# Frontend (from repo root)
yarn install                    # Install dependencies
yarn start                      # Watch mode with live reload
yarn lint --fix                 # Fix ESLint issues
yarn stylelint-linux --fix      # Fix CSS issues (use stylelint-windows on Windows)
yarn build --env production     # Production build

# Backend - VS Code/Rider
# Set startup project to Radarr.Console, framework to net6.0, then Debug

# Backend - Command line (from repo root)
dotnet clean src/Radarr.sln -c Debug
dotnet msbuild -restore src/Radarr.sln -p:Configuration=Debug -p:Platform=Posix -t:PublishAllRids
# Run from _output/

# Tests
./test.sh Linux Unit Test         # Linux unit tests
./test.sh Windows Integration Test # Windows integration tests
```

Output directories: `_output/` (compiled app + UI), `_tests/` (test assemblies)

## Testing Conventions

Tests use NUnit + Moq with AutoMoq pattern:
```csharp
public class MovieServiceFixture : CoreTest<MovieService>
{
    [Test]
    public void should_get_movie_by_id()
    {
        Mocker.GetMock<IMovieRepository>()
            .Setup(r => r.Get(1))
            .Returns(new Movie { Id = 1, Title = "Test" });
        
        var result = Subject.GetMovie(1);  // Subject is auto-resolved with mocked dependencies
        
        result.Title.Should().Be("Test");
    }
}
```

Test categories: `[Category("ManualTest")]`, `[IntegrationTest]`

## Conventions

- **Namespaces**: Match folder structure exactly
- **Naming**: Services (`*Service`), Repositories (`*Repository`), Commands (`*Command`)
- **Validation**: FluentValidation for API input validation
- **Logging**: NLog via dependency injection (`Logger` parameter in constructor)
- **Line endings**: Commit with Unix line endings (`\n`)
- **Indentation**: 4 spaces (not tabs)
- **PRs**: Only to `develop` branch, never `master`
- **Commit messages**: Prefix with `New:` or `Fixed:` for changelog-worthy changes
