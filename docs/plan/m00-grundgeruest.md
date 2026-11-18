# M0 – Grundgerüst

Ziel: lauffähiger Server mit Web-App-Hülle, Konten/Haushalten, API-Konventionen, Domain-Kern für
M1, Architektur- und Telemetrie-Regeln als Tests, CI und Docker-Betrieb.
Integrationsbranch: `feat/m00-foundation` (von `docs/project-foundation`, es gibt noch kein
`develop`/`main`).

## Schritt 0 (erledigt, steuernde Sitzung)
- Solution `FoodAssistant.slnx`, Projekte nach ADR-0001 (ohne Mobile – kommt mit M1).
- `Directory.Build.props` (Warnungen = Fehler, Nullable), `Directory.Packages.props`
  (zentrale Versionen), `.editorconfig` (CA1848 = Fehler in `src/`), `.gitattributes`, `.gitignore`.
- `scripts/check-file-length.ps1`, `.githooks/pre-commit`.
- Einhängepunkte (leere Erweiterungsmethoden) in `Program.cs`: `AddServerTelemetry`, `AddApplication`,
  `AddInfrastructure`, `AddApi`/`UseApi`, `AddAuth`/`UseAuth`, `AddUi`, `MapApiV1`.
- `Telemetry` (ActivitySource/Meter) in `Application/Diagnostics`.
- Je Testprojekt ein Test; `/health`-Endpunkt.

## Parallelisierung

```
Schritt 0 ──┬── A Architektur + CI ─────────┐
            ├── B Domain-Kern ──────────────┤
            ├── C API + Telemetrie + Docker ┼── Merge B, A, C, E, D ── Gesamt-Review
            ├── E UI-Hülle + Theme ─────────┤
            └── D Persistenz + Konten ──────┘
```

| Spur | Inhalt | Branch | Modell / Effort | EventIds |
|---|---|---|---|---|
| A | NetArchTest-Schichtregeln (AGENTS.md 2.2), Telemetrie-Konventionstests, Feature-Ordner-Regel, GitHub-Actions-CI | `feat/m00-a-architecture` | Sonnet / medium | – |
| B | Domain-Kern: Mengen/Einheiten, Ablaufstatus, Chargen-Zustände + Haltbarkeitsregeln, FEFO, Mindestbestand, Konfliktregeln Einkaufsliste | `feat/m00-b-domain` | Opus / high | – |
| C | ProblemDetails + `errorCode`, Validierung, Idempotency-Key, ETag/`If-Match`-Hilfen, Cursor-Pagination, `/api/v1/info`, OpenAPI + Snapshot-Test, OpenTelemetry, Forwarded Headers, Rate-Limits, Dockerfile + Compose + Caddy | `feat/m00-c-api` | Opus / high | Server 1000–1099 |
| E | MudBlazor-Theme aus dem Designsystem (Hell/Dunkel/Hochkontrast), App-Shell (untere Leiste / Seitenleiste), StatusChip, ListRow, Lokalisierung DE/EN, PWA-Manifest | `feat/m00-e-ui` | Sonnet / high | Server 1200–1299, UI 4000–4099 |
| D | EF Core + PostgreSQL, Haushaltsfilter, Migrationen beim Start, Identity, OpenIddict (Code + PKCE, Cookie fürs Web), Einrichtungsassistent-API, Haushalte, Rollen-Policies, Einladungen | `feat/m00-d-accounts` | Opus / high | Server 1100–1199, Infrastructure 2000–2199, Application 3000–3099 |

### Exklusiv-Dateien
- **A:** `tests/FoodAssistant.Architecture.Tests/**`, `.github/**`
- **B:** `src/FoodAssistant.Domain/**`, `tests/FoodAssistant.Domain.Tests/**`
- **C:** `src/FoodAssistant.Server/Api/**` (außer neuen Feature-Gruppen), `Server/Composition/ApiRegistration.cs`,
  `Server/Composition/TelemetryRegistration.cs`, `src/FoodAssistant.Contracts/Common/**`, `deploy/**`,
  `Dockerfile`, `docker-compose.yml`, `tests/FoodAssistant.Server.Tests/Api/**`
- **E:** `src/FoodAssistant.UI/**`, `src/FoodAssistant.Web.Client/**`, `Server/Components/**`,
  `Server/wwwroot/**`, `Server/Composition/UiRegistration.cs`, `tests/FoodAssistant.UI.Tests/**`
- **D:** `src/FoodAssistant.Infrastructure/**`, `src/FoodAssistant.Application/{Accounts,Households}/**`,
  `src/FoodAssistant.Contracts/{Accounts,Households}/**`, `src/FoodAssistant.Server/Auth/**`,
  `Server/Composition/AuthRegistration.cs`, `tests/FoodAssistant.Infrastructure.Tests/**`,
  `tests/FoodAssistant.Server.Tests/Auth/**`

### Hotspots (nur ergänzen, nichts umschreiben)
`Directory.Packages.props`, `Server/Api/ApiV1Routes.cs`, `Application/Composition/ApplicationRegistration.cs`,
`Infrastructure/Composition/InfrastructureRegistration.cs` (D), `tests/FoodAssistant.Server.Tests/*.csproj`,
`docs/kb/telemetrie.md` (C legt an), `appsettings*.json`.

### Merge-Reihenfolge
B → A → C → E → D. Nach jedem Merge: Build, Tests, `check-file-length.ps1`.

## Akzeptanz M0
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün (inkl. Testcontainers mit Docker) – 507 Tests
- [x] Architektur- und Telemetrie-Konventionstests aktiv
- [x] `docker compose up` startet Server + PostgreSQL, `/health` und `/api/v1/info` antworten
- [x] Einrichtung: erstes Konto + Haushalt anlegen, Einladung erzeugen und annehmen (API-Tests)
- [x] Web-App zeigt App-Shell im Theme mit Navigation, DE/EN
- [x] Gesamt-Review nach §9 ohne offene Befunde (2 Runden)

## Integration (steuernde Sitzung)
- Fehlerantworten von D laufen über `ApiProblems` (C); `ValidationFailed` nimmt einen Feature-Code.
- Rate-Limiter zwischen Authentifizierung und Autorisierung (`api` pro Person); `auth-strict` auf
  Login, Einrichtung, Einladung annehmen; eigener Limiter pro Netz für `/connect/*` und
  `/.well-known/*` **vor** der Authentifizierung, weil OpenIddict dort selbst antwortet.
- Idempotenz: `WithoutIdempotency()` für Login, Logout, Einrichtung, Einladung annehmen (Wiederholung
  käme ohne Cookie, Fingerprint enthielte das Passwort); Antiforgery-Ablehnungen werden nicht gespeichert.
- `PublicUrl` prüft nur noch `HostingOptionsValidator`; Umgebung `Testing` gilt wie `Development`.
- API-Tests (C) brauchen jetzt die Datenbank (`PostgresContainerFixture`), weil der Server beim Start migriert.

## Rückblick
- **C (Opus/high) zu breit geschnitten:** API-Konventionen, Telemetrie und Docker in einer Spur, 14
  Review-Runden, ca. 2,5 h. Künftig Docker/Deploy als eigene Sonnet-Spur abtrennen und
  Review-Runden bei reinen Kompromissfragen früher an die steuernde Sitzung zurückgeben.
- **D (Opus/high) passend:** Auth/Datenmodell brauchten die Stufe.
- **B (Opus/high) eher zu hoch:** Domain-Logik nach klaren Regeln hätte Sonnet geschafft.
- **A, E (Sonnet) passend.**
- **Nahtstellen unterschätzt:** Middleware-Reihenfolge, Rate-Limits und Idempotenz zwischen C und D
  ergaben die meisten Review-Befunde. Solche Querschnittsthemen gehören in Schritt 0 oder in eine Spur.
