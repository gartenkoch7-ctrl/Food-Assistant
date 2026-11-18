# Telemetrie – Katalog

Alle Logs, Abläufe (Activities) und Messwerte des Servers (AGENTS.md 2.5). Wer Telemetrie
hinzufügt, trägt sie hier ein – in derselben Änderung.

## Regeln in Kürze

- Logs nur über `[LoggerMessage]` in einer `*Log.cs` je Feature-Ordner (Analyzer CA1848).
- Abläufe: `Telemetry.Source.StartActivity("foodassistant.<feature>.<vorgang>")`.
- Messwerte: `Telemetry.Meter`, Namen `foodassistant.<feature>.<messwert>`.
- **Keine Inhalte des Nutzers**: keine Namen, E-Mails, Freitexte, Prompts, IP-Adressen, keine
  konkreten Pfade (nur Routen-Vorlagen wie `/api/v1/households/{householdId}/items`). Erlaubt:
  IDs, Zählwerte, Kategorien, Dauer, Status.
- Export per OpenTelemetry (Logs, Traces, Messwerte). OTLP nur, wenn
  `OTEL_EXPORTER_OTLP_ENDPOINT` gesetzt ist (Schalter; Variablen je Signal verfeinern nur); sonst verlässt nichts den Server
  (`TelemetryRegistration`). `service.name` = `foodassistant-server`.
- Automatisch instrumentiert: eingehende HTTP-Anfragen (ASP.NET Core, ohne `/health`) und
  ausgehende HTTP-Aufrufe (HttpClient) als Traces und Messwerte. Datenschutz: `url.path`
  eingehender Spans wird durch die Routen-Vorlage ersetzt, `url.full` ausgehender Spans auf
  Schema und Host gekürzt; Query-Strings schwärzt die Instrumentierung selbst.

## EventId-Bereiche

| Projekt / Spur | Bereich | Vergeben in |
|---|---|---|
| Server – API-Konventionen (M0 Spur C) | 1000–1099 | M0 |
| Server – Konten, Anmeldung (M0 Spur D) | 1100–1199 | M0 |
| Server – UI-Host (M0 Spur E) | 1200–1299 | M0 |
| Infrastructure (M0 Spur D) | 2000–2199 | M0 |
| Application (M0 Spur D) | 3000–3099 | M0 |
| UI (M0 Spur E) | 4000–4099 | M0 |

Innerhalb von 1000–1099: Fehlerbehandlung 1000–1009, Idempotenz 1010–1019, Rate-Limits 1020–1029,
Rest frei für weitere API-Konventionen.

## Logs

| Name / EventId | Art | Feature | Bedeutung | Attribute |
|---|---|---|---|---|
| `UnhandledException` / 1000 | Log, Error | API – Fehler | Unerwartete Exception in einer API-Anfrage; Antwort 500 `common.internal_error` ohne Details | `HttpMethod`, `Route`, Exception |
| `BadRequestRejected` / 1001 | Log, Debug | API – Fehler | Unlesbare Anfrage (z. B. kaputtes JSON, falscher Content-Type) | `StatusCode`, `HttpMethod`, `Route` |
| `Replayed` / 1010 | Log, Debug | API – Idempotenz | Gespeicherte Antwort zu einem `Idempotency-Key` erneut ausgeliefert | `HttpMethod`, `Route`, `StatusCode` |
| `KeyReused` / 1011 | Log, Warning | API – Idempotenz | Schlüssel für eine andere Anfrage wiederverwendet → 422 | `HttpMethod`, `Route` |
| `RequestInProgress` / 1012 | Log, Information | API – Idempotenz | Gleichzeitige Wiederholung, während die erste Anfrage läuft → 409 | `HttpMethod`, `Route` |
| `KeyInvalid` / 1013 | Log, Debug | API – Idempotenz | `Idempotency-Key` ist keine einzelne UUID → 400 | `HttpMethod`, `Route` |
| `StoreFailed` / 1014 | Log, Warning | API – Idempotenz | Speicher für Idempotenz fehlgeschlagen (Speichern oder Freigeben); der Client bekommt seine Antwort, der Schlüssel bleibt bis Lease-Ende (10 min) reserviert | `HttpMethod`, `Route`, Exception |
| `Rejected` / 1020 | Log, Debug | API – Rate-Limits | Anfrage durch Rate-Limit abgewiesen → 429 | `Policy`, `HttpMethod`, `Route` |

## Abläufe (Activities)

| Name | Feature | Bedeutung | Attribute |
|---|---|---|---|
| – | – | Noch keine eigenen; HTTP-Anfragen kommen aus der ASP.NET-Core-Instrumentierung. | – |

## Messwerte

| Name | Art | Feature | Bedeutung | Attribute |
|---|---|---|---|---|
| `foodassistant.api.idempotency.requests` | Counter `{request}` | API – Idempotenz | Schreibende Anfragen mit `Idempotency-Key` nach Ergebnis | `outcome`: `stored`, `replayed`, `key_reused`, `in_progress`, `invalid_key`, `abandoned` (5xx oder Exception, Wiederholung läuft erneut), `stored_without_body` (Antwort > 1 MiB: nur Status und Header gespeichert), `store_failed`, `write_failed` (Handler fertig, Ergebnis nicht schreibbar; Schlüssel bleibt bis Lease-Ende reserviert) |
| `foodassistant.api.rate_limit.rejections` | Counter `{request}` | API – Rate-Limits | Mit 429 abgewiesene Anfragen | `policy`: `auth-strict`, `api`, `global` (API-Anfragen ohne passenden Endpunkt) |
