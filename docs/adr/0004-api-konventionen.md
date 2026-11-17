# ADR-0004: API-Konventionen

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Web, Mobil und später Fremdprogramme (API-Tokens, ADR-0003) nutzen dieselbe HTTP-API. Mobil
arbeitet teilweise offline (ADR-0005), mehrere Personen ändern gleichzeitig.

## Entscheidung

### Form
- **REST** mit ASP.NET-Core-Minimal-APIs, Endpunkte pro Feature gruppiert (`MapGroup`).
- Versionierung in der URL: `/api/v1/…`. Innerhalb von `v1` nur **abwärtskompatible** Änderungen
  (neue Felder/Endpunkte); Brüche → `/api/v2`, `v1` bleibt mindestens ein Minor-Release erhalten.
- Haushaltsdaten unter `/api/v1/households/{householdId}/…`.
- JSON mit `camelCase`, Datum `DateOnly` als `yyyy-MM-dd`, Zeitpunkte als ISO 8601 UTC.
- Mengen immer als Objekt `{ "value": 1.5, "unit": "kg" }`.

### IDs
- **UUIDv7** (`Guid.CreateVersion7()`), vom Client erzeugbar – nötig für Offline-Anlage.

### Fehler
- Einheitlich **ProblemDetails (RFC 9457)**, `Content-Type: application/problem+json`.
- Zusatzfeld `errorCode` (stabil, z. B. `inventory.item_not_found`) – die UI übersetzt darüber,
  nie über den Text.
- Validierungsfehler: `400` mit `errors` pro Feld (`ValidationProblemDetails`).
- Keine Stacktraces oder internen Details in Antworten; `traceId` immer enthalten.
- Statuscodes: `400` Validierung, `401` nicht angemeldet, `403` keine Rechte, `404` nicht
  vorhanden **oder fremder Haushalt** (kein Hinweis auf Existenz), `409` fachlicher Konflikt,
  `412` Versionskonflikt, `422` fachliche Regel verletzt, `429` Rate-Limit.

### Nebenläufigkeit
- Jede änderbare Ressource hat eine **Version** (PostgreSQL `xmin` bzw. Versionsspalte),
  ausgegeben als `ETag`.
- Änderungen senden `If-Match`; passt die Version nicht → `412` mit aktuellem Stand.
- Ausnahme: Offline-Warteschlange der Einkaufsliste nutzt die Konfliktregeln aus ADR-0005.

### Idempotenz
- Schreibende Anfragen dürfen einen Header `Idempotency-Key` (UUID) tragen. Der Server merkt sich
  das Ergebnis 24 Stunden und liefert bei Wiederholung dieselbe Antwort.
- Die Offline-Warteschlange (ADR-0005) sendet **immer** einen Idempotency-Key.

### Listen
- **Cursor-Pagination:** `?limit=50&cursor=…`, Antwort mit `nextCursor`; kein Offset.
- Filter und Sortierung als Query-Parameter mit festen, dokumentierten Namen.

### Dokumentation und Client
- **OpenAPI-Dokument** (`Microsoft.AspNetCore.OpenApi`) ist die Quelle der Wahrheit; der
  typisierte Client in `FoodAssistant.Client` nutzt die DTOs aus `Contracts`.
- Ein Test vergleicht das erzeugte OpenAPI-Dokument mit einem eingecheckten Stand
  (Snapshot) – unbeabsichtigte API-Brüche fallen auf.

### Versionsprüfung
- `GET /api/v1/info` (ohne Anmeldung): Server-Version, minimale App-Version, unterstützte
  API-Versionen, aktivierte Funktionen (z. B. KI an/aus).

## Konsequenzen

- Endpunkte werden nur über gemeinsame Hilfen gebaut (Fehler-Mapping, Haushaltsprüfung,
  Idempotenz), damit die Regeln nicht pro Endpunkt neu entstehen.
- Der OpenAPI-Snapshot muss bei gewollten Änderungen bewusst aktualisiert werden.
