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
| `LoginSucceeded` / 1100 | Log, Information | Anmeldung | Benutzer erfolgreich angemeldet | `UserId` |
| `LoginFailedUnknownUser` / 1101 | Log, Information | Anmeldung | Anmeldung mit unbekanntem Benutzernamen | – |
| `LoginFailed` / 1102 | Log, Information | Anmeldung | Anmeldung mit falschem Passwort | `UserId` |
| `LoginLockedOut` / 1103 | Log, Warning | Anmeldung | Anmeldung wegen Sperre abgelehnt | `UserId` |
| `LoggedOut` / 1104 | Log, Information | Anmeldung | Benutzer abgemeldet | `UserId` |
| `AntiforgeryRejected` / 1105 | Log, Warning | Anmeldung | Cookie-Anfrage ohne gültiges Antiforgery-Token abgewiesen | – |
| `AuthorizeRequiresLogin` / 1106 | Log, Information | Anmeldung | Autorisierungsanfrage ohne Web-Anmeldung abgewiesen | – |
| `AuthorizationCodeIssued` / 1107 | Log, Information | Anmeldung | Autorisierungscode für Client ausgestellt | `ClientId`, `UserId` |
| `TokensIssued` / 1108 | Log, Information | Anmeldung | Tokens ausgestellt | `GrantType`, `UserId` |
| `TokenRefused` / 1109 | Log, Warning | Anmeldung | Token-Anfrage abgelehnt: Konto fehlt oder ist gesperrt | `GrantType` |
| `ClientRegistered` / 1110 | Log, Information | Anmeldung | OpenID-Client registriert | `ClientId` |
| `TokenKeyCreated` / 1111 | Log, Information | Anmeldung | Neuer Token-Schlüssel erzeugt | `KeyPurpose` |
| `HouseholdNotAccessible` / 1112 | Log, Information | Anmeldung | Zugriff auf Haushalt verweigert: kein Mitglied | – |
| `HouseholdRoleInsufficient` / 1113 | Log, Information | Anmeldung | Zugriff verweigert: Rolle reicht nicht | `HouseholdId`, `Role` |
| `ServerAdminDenied` / 1114 | Log, Warning | Anmeldung | Server-Admin-Recht fehlt, Zugriff verweigert | `UserId` |
| `DatabaseUpToDate` / 2000 | Log, Information | Persistenz | Datenbankschema ist aktuell | – |
| `MigrationsStarting` / 2001 | Log, Information | Persistenz | Migrationen beginnen | `Count` |
| `MigrationsApplied` / 2002 | Log, Information | Persistenz | Migrationen angewendet | `Count`, `ElapsedMilliseconds` |
| `MigrationFailed` / 2003 | Log, Critical | Persistenz | Migration fehlgeschlagen; der Server stoppt | Exception |
| `AccountCreationRefused` / 2020 | Log, Information | Konten | Konto nicht angelegt; nur Fehlercodes, keine Eingaben | `ErrorCodes` |
| `InvitationRedemptionConflict` / 2021 | Log, Warning | Konten | Einladung parallel eingelöst; diese Einlösung zurückgerollt | `InvitationId` |
| `SetupCompleted` / 3000 | Log, Information | Konten | Ersteinrichtung abgeschlossen | `UserId`, `HouseholdId` |
| `SetupRejected` / 3001 | Log, Warning | Konten | Ersteinrichtung abgelehnt, sie war bereits abgeschlossen | – |
| `InvitationAccepted` / 3002 | Log, Information | Konten | Einladung angenommen | `InvitationId`, `HouseholdId`, `UserId`, `Role` |
| `InvitationRejected` / 3003 | Log, Warning | Konten | Einladung nicht angenommen | `Reason` |
| `HouseholdCreated` / 3020 | Log, Information | Haushalte | Haushalt angelegt | `HouseholdId` |
| `InvitationCreated` / 3021 | Log, Information | Haushalte | Einladung erstellt | `InvitationId`, `HouseholdId`, `Role` |
| `InvitationRevoked` / 3022 | Log, Information | Haushalte | Einladung widerrufen | `InvitationId`, `HouseholdId` |
| `MemberRoleChanged` / 3023 | Log, Information | Haushalte | Rolle eines Mitglieds geändert | `HouseholdId`, `UserId`, `Role` |
| `MemberRemoved` / 3024 | Log, Information | Haushalte | Mitglied aus Haushalt entfernt | `HouseholdId`, `UserId` |
| `LastOwnerProtected` / 3025 | Log, Warning | Haushalte | Änderung abgelehnt: letzter Besitzer würde fehlen | `HouseholdId` |
| `BrowserCallUnavailable` / 4000 | Log, Debug | UI | Browser-Aufruf nicht verfügbar; nur Operationsname | `Operation`, Exception |

## Abläufe (Activities)

| Name | Feature | Bedeutung | Attribute |
|---|---|---|---|
| `foodassistant.accounts.accept_invitation` | Konten | Einladung annehmen | `foodassistant.existing_account`, `foodassistant.outcome` |
| `foodassistant.accounts.authorize` | Anmeldung | OpenID-Autorisierungsanfrage | – |
| `foodassistant.accounts.login` | Anmeldung | Web-Anmeldung | `foodassistant.outcome` |
| `foodassistant.accounts.logout` | Anmeldung | Web-Abmeldung | – |
| `foodassistant.accounts.setup` | Konten | Ersteinrichtung | `foodassistant.outcome` |
| `foodassistant.accounts.token` | Anmeldung | Token-Ausstellung | `foodassistant.grant_type` |
| `foodassistant.households.change_member_role` | Haushalte | Mitgliedsrolle ändern | `foodassistant.outcome` |
| `foodassistant.households.create` | Haushalte | Haushalt anlegen | `foodassistant.household_id` |
| `foodassistant.households.create_invitation` | Haushalte | Einladung erstellen | – |
| `foodassistant.households.remove_member` | Haushalte | Mitglied entfernen | `foodassistant.outcome` |
| `foodassistant.households.revoke_invitation` | Haushalte | Einladung widerrufen | `foodassistant.outcome` |
| `foodassistant.persistence.migrate` | Persistenz | Datenbank-Migration | `foodassistant.migrations.pending` |
| HTTP-Anfragen | – | Kommen aus der ASP.NET-Core-Instrumentierung, nicht aus eigenen Abläufen | – |

## Messwerte

| Name | Art | Feature | Bedeutung | Attribute |
|---|---|---|---|---|
| `foodassistant.api.idempotency.requests` | Counter `{request}` | API – Idempotenz | Schreibende Anfragen mit `Idempotency-Key` nach Ergebnis | `outcome`: `stored`, `replayed`, `key_reused`, `in_progress`, `invalid_key`, `abandoned` (5xx, Exception oder vor dem Handler abgewiesen, z. B. Antiforgery; Wiederholung läuft erneut), `stored_without_body` (Antwort > 1 MiB: nur Status und Header gespeichert), `store_failed`, `write_failed` (Handler fertig, Ergebnis nicht schreibbar; Schlüssel bleibt bis Lease-Ende reserviert) |
| `foodassistant.api.rate_limit.rejections` | Counter `{request}` | API – Rate-Limits | Mit 429 abgewiesene Anfragen | `policy`: `auth-strict`, `api`, `openid` (`/connect/*`, `/.well-known/*`, vor der Authentifizierung pro Netz), `global` (API-Anfragen ohne passenden Endpunkt) |
