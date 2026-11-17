# ADR-0001: Tech-Stack

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Food-Assistant ist ein **Open-Source-Server zum Selbsthosten**, ausgelegt für **einen Haushalt**
(mehrere Personen teilen Vorrat, Einkaufsliste und Rezepte). Funktionen: Vorrat und Haltbarkeit,
Einkaufsverwaltung, KI-gestütztes Kochen, weitere Features nach Bedarf.

Anforderungen:
- Clients für **Web/Browser, Android und iOS**.
- Self-Hoster sollen den Server mit wenig Aufwand betreiben (Docker).
- KI muss **optional** und **austauschbar** sein: lokal (privat, kostenlos) oder Cloud.
- Mobil im Supermarkt ohne Netz: Vorrat und Einkaufsliste lesen, Einkäufe abhaken.
- Entwickler-Präferenz: **.NET / C#** (Erfahrung aus ZoomIN und anderen Projekten).

## Entscheidung

| Bereich | Wahl |
|---|---|
| Sprache/Runtime | C# 14, **.NET 10 (LTS)** |
| Server | **ASP.NET Core** (Minimal APIs, `/api/v1`), hostet zugleich die Web-App |
| Web-Client | **Blazor Web App**, Render-Modus *Interactive WebAssembly*, als **PWA** installierbar |
| Mobil (Android, iOS) | **.NET MAUI Blazor Hybrid** |
| Gemeinsame UI | Razor-Klassenbibliothek, von Web und Mobil gemeinsam genutzt; Komponenten: **MudBlazor** |
| Datenbank Server | **PostgreSQL** über **EF Core** (Npgsql), Migrationen per EF Core |
| Offline-Cache Mobil | **SQLite** auf dem Gerät (nur Cache + Änderungswarteschlange, keine zweite Wahrheit) |
| Offline-Umfang | Vorrat und Einkaufsliste **lesen**, Einkaufsliste **abhaken/ergänzen**; Sync beim Wiederverbinden |
| Anmeldung | **ASP.NET Core Identity** (lokale Konten je Person, Haushalt als Mandant) + optional **OIDC** (Authentik, Authelia, Keycloak …) |
| KI | **Microsoft.Extensions.AI** (`IChatClient`) als Abstraktion; Anbieter wählbar: Ollama (lokal), Anthropic Claude, OpenAI-kompatibel. KI ist abschaltbar |
| Externe Produktdaten | Open Food Facts (Barcode → Produkt), hinter Schnittstelle |
| Telemetrie | `ILogger<T>` mit `[LoggerMessage]`, `ActivitySource`/`Meter`, **OpenTelemetry** mit optionalem OTLP-Export |
| Betrieb | **Docker-Image** + `docker-compose.yml` (Server + PostgreSQL); Konfiguration per Umgebungsvariablen |
| Tests | xUnit v3 (Microsoft.Testing.Platform), NetArchTest, bUnit (Komponenten), Testcontainers (PostgreSQL) |
| Paketverwaltung | Central Package Management (`Directory.Packages.props`) |
| Lizenz | **MIT** |

### Projektstruktur

```
src/
  FoodAssistant.Domain          Fachlogik, keine Abhängigkeiten
  FoodAssistant.Contracts       API-DTOs v1, keine Abhängigkeiten
  FoodAssistant.Application     Anwendungsfälle, Schnittstellen  → Domain, Contracts
  FoodAssistant.Infrastructure  EF Core/PostgreSQL, KI-Anbieter, Open Food Facts → Application
  FoodAssistant.Server          ASP.NET Core, Composition Root, hostet Web → Application, Infrastructure, Web.Client
  FoodAssistant.Client          typisierter API-Client, Offline-Schnittstellen → Contracts
  FoodAssistant.UI              gemeinsame Razor-Komponenten → Client, Contracts
  FoodAssistant.Web.Client      Blazor WebAssembly → UI, Client
  FoodAssistant.Mobile          MAUI Blazor Hybrid, Kamera/Barcode, SQLite-Cache → UI, Client
tests/
  FoodAssistant.Domain.Tests, .Application.Tests, .Infrastructure.Tests,
  FoodAssistant.Server.Tests, .UI.Tests, FoodAssistant.Architecture.Tests
```

Web und Mobil sprechen **beide über die HTTP-API** mit dem Server. Die UI kennt weder Domain
noch Application – damit verhalten sich Browser und App gleich.

## Begründung

- **Ein Stack für alles:** C# vom Server bis zur App, gemeinsame DTOs und UI-Komponenten.
- **Blazor Hybrid statt nativer XAML-UI:** eine Oberfläche für drei Plattformen; MAUI liefert
  trotzdem native Kamera, Benachrichtigungen und lokale Datenbank.
- **WebAssembly statt Blazor Server:** gleicher Datenweg wie die App (HTTP-API), PWA-fähig,
  keine dauerhafte SignalR-Verbindung pro Nutzer.
- **PostgreSQL:** robust bei gleichzeitigem Schreiben mehrerer Haushaltsmitglieder, im
  Self-Hosting-Umfeld verbreitet, per Compose ohne Mehraufwand mitgeliefert.
- **Microsoft.Extensions.AI:** Anbieterwechsel ohne Codeänderung; Self-Hoster ohne GPU und ohne
  Cloud-Konto können KI ausschalten.
- **Begrenzter Offline-Umfang:** deckt den Hauptfall (Einkaufen ohne Netz) ab, ohne volle
  Konfliktauflösung für alle Daten.
- **MIT:** maximal freie Nutzung, gleiche Lizenz wie ZoomIN.

## Konsequenzen

- iOS-Builds brauchen einen Mac oder einen Cloud-Build-Dienst.
- MudBlazor muss in WebView (Hybrid) und Browser gleich gut funktionieren; Abweichungen kommen in
  `docs/kb/build-und-tooling.md`.
- Sync der Einkaufsliste braucht Versionsfelder und Konfliktregeln → eigenes ADR, sobald umgesetzt.
- Integrationstests brauchen Docker (Testcontainers).
- Telemetrie-, Architektur- und Dateilängenregeln aus `AGENTS.md` werden auf diesen Stack
  konkretisiert.
