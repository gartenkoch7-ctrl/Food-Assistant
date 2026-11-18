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
| Server – Sync (Live-Updates; M1 S0, E1, E2) | 1300–1324 | M1 S0.2 |
| Infrastructure – Sync (Änderungsprotokoll; M1 S0, E2) | 2200–2249 | M1 S0.2 |
| Server/Infrastructure/Application/UI/Client – Activity (M1 S0, V) | 1325–1349 / 2250–2299 / 3150–3199 / 4150–4199 | M1 S0.3 |
| Server – Households (M1 K); UI/Client – Session, Accounts | 1350–1374; 4200–4249 | M1 S0.3 |
| Application – Sync (M1 S0, E1, E2) / UI – Sync | 3100–3149 / 4100–4149 | M1 S0.3 |
| Server/Infrastructure/Application/UI/Client – Catalog (M1 A) | 1375–1399 / 2300–2349 / 3200–3249 / 4250–4299 | M1 S0.3 |
| ShelfLife (M1 B1) | 1400–1424 / 2350–2399 / 3250–3299 / 4300–4349 | M1 S0.3 |
| Inventory (M1 B2) | 1425–1449 / 2400–2449 / 3300–3349 / 4350–4399 | M1 S0.3 |
| FoodFacts (M1 C1) | 1450–1474 / 2450–2499 / 3350–3399 | M1 S0.3 |
| Scan (M1 C2, nur UI/Client) | 4400–4449 | M1 S0.3 |
| Shopping (M1 D) | 1475–1499 / 2500–2549 / 3400–3449 / 4450–4499 | M1 S0.3 |
| Stores (M1 D) | 1500–1524 / 2550–2599 / 3450–3499 / 4500–4549 | M1 S0.3 |
| Checkout (M1 F) | 1525–1549 / 2600–2649 / 3500–3549 / 4550–4599 | M1 S0.3 |
| Restock (M1 G) | 1550–1574 / 2650–2699 / 3550–3599 / 4600–4649 | M1 S0.3 |
| Notifications (M1 H) | 1575–1599 / 2700–2749 / 3600–3649 / 4650–4699 | M1 S0.3 |
| Statistics (M1 I) | 1600–1624 / 2750–2799 / 3650–3699 / 4700–4749 | M1 S0.3 |
| Labels (M1 J) | 1625–1649 / 2800–2849 / 3700–3749 / 4750–4799 | M1 S0.3 |
| Import (M1 L) | 1650–1674 / 2850–2899 / 3750–3799 / 4800–4849 | M1 S0.3 |
| Backup (M1 N) | 1675–1699 / 2900–2949 / 3800–3849 | M1 S0.3 |
| Export (M1 N) | 1700–1724 / 2950–2999 / 3850–3899 / 4850–4899 | M1 S0.3 |
| Mobile (M1 M) | 4900–4999 | M1 S0.3 |

Im Server heißt der Verlauf-Ordner `Activities/` (Namespace `FoodAssistant.Server.Activities`):
`FoodAssistant.Server.Activity` würde im Server-Code `System.Diagnostics.Activity` verdecken. Der
Präfix in `TelemetryConventionTests` lautet deshalb dort `FoodAssistant.Server.Activities`; die
anderen Projekte bleiben bei `Activity`.

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
| `HttpsRequired` / 1115 | Log, Warning | Anmeldung | Antiforgery-Token über einfaches HTTP angefragt; Web-Anmeldung braucht HTTPS (Antwort 400 `accounts.https_required`) | – |
| `HubJoinDenied` / 1300 | Log, Information | Sync | Beitritt zur Live-Gruppe eines Haushalts abgelehnt: kein Mitglied (unbekannt oder fremd) | – |
| `HubJoined` / 1301 | Log, Debug | Sync | Verbindung empfängt Live-Updates eines Haushalts | `HouseholdId` |
| `DatabaseUpToDate` / 2000 | Log, Information | Persistenz | Datenbankschema ist aktuell | – |
| `MigrationsStarting` / 2001 | Log, Information | Persistenz | Migrationen beginnen | `Count` |
| `MigrationsApplied` / 2002 | Log, Information | Persistenz | Migrationen angewendet | `Count`, `ElapsedMilliseconds` |
| `MigrationFailed` / 2003 | Log, Critical | Persistenz | Migration fehlgeschlagen; der Server stoppt | Exception |
| `AccountCreationRefused` / 2020 | Log, Information | Konten | Konto nicht angelegt; nur Fehlercodes, keine Eingaben | `ErrorCodes` |
| `InvitationRedemptionConflict` / 2021 | Log, Warning | Konten | Einladung parallel eingelöst; diese Einlösung zurückgerollt | `InvitationId` |
| `ChangeNotificationFailed` / 2200 | Log, Warning | Sync | Live-Update nach dem Commit nicht gesendet; die Speicherung bleibt gültig, Clients holen über das Änderungsprotokoll nach | `HouseholdId`, Exception |
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
| `ShoppingConflictResolved` / 3400 | Log, Information | Einkauf | Konflikt eines Einkaufslisten-Eintrags nach ADR-0005 aufgelöst (Ergebnis ≠ `Created`/`NoConflict`) | `ItemId`, `Outcome` |
| `ShoppingListCreated` / 3401 | Log, Information | Einkauf | Einkaufsliste angelegt | `ListId` |
| `ShoppingListDeleted` / 3402 | Log, Information | Einkauf | Einkaufsliste samt Einträgen gelöscht | `ListId` |
| `RestrictedChangeDenied` / 3403 | Log, Information | Einkauf | Eingeschränktes Mitglied wollte mehr als abhaken → 403 `shopping.restricted_change` | – |
| `BrowserCallUnavailable` / 4000 | Log, Debug | UI | Browser-Aufruf nicht verfügbar; nur Operationsname | `Operation`, Exception |
| `NetworkError` / 4010 | Log, Debug | Client | API-Aufruf ohne Antwort fehlgeschlagen (auch Zeitüberschreitung des HttpClient); nur HTTP-Methode, nie Pfad/Query | `Method`, Exception |
| `InvalidResponse` / 4011 | Log, Warning | Client | Erfolgsantwort der API mit unlesbarem JSON-Körper (z. B. HTML eines Proxys); nur HTTP-Methode, ohne Exception (Meldung kann Teile des Körpers zitieren) | `Method` |
| `ExpiryReloadFailed` / 4200 | Log, Warning | UI – Sitzung | Neuladen der Sitzung nach einer 401-Antwort eines Datenaufrufs ist mit einer Exception gescheitert (das Neuladen läuft ohne Aufrufer; höchstens einmal je 30 s) | Exception |

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
| `foodassistant.shopping.add_item` | Einkauf | Eintrag anlegen (auch intern: Mindestbestand, wiederkehrend) | `foodassistant.outcome` |
| `foodassistant.shopping.change_item` | Einkauf | Eintrag ändern/abhaken mit Konfliktregeln | `foodassistant.outcome` |
| `foodassistant.shopping.create_list` | Einkauf | Einkaufsliste anlegen | `foodassistant.outcome` |
| `foodassistant.shopping.delete_item` | Einkauf | Eintrag löschen mit Konfliktregeln | `foodassistant.outcome` |
| `foodassistant.shopping.delete_list` | Einkauf | Einkaufsliste löschen | `foodassistant.outcome` |
| `foodassistant.shopping.move_item` | Einkauf | Eintrag auf andere Liste verschieben | `foodassistant.outcome` |
| `foodassistant.shopping.update_list` | Einkauf | Einkaufsliste ändern (auch Standardliste wechseln) | `foodassistant.outcome` |
| HTTP-Anfragen | – | Kommen aus der ASP.NET-Core-Instrumentierung, nicht aus eigenen Abläufen | – |

## Messwerte

| Name | Art | Feature | Bedeutung | Attribute |
|---|---|---|---|---|
| `foodassistant.api.idempotency.requests` | Counter `{request}` | API – Idempotenz | Schreibende Anfragen mit `Idempotency-Key` nach Ergebnis | `outcome`: `stored`, `replayed`, `key_reused`, `in_progress`, `invalid_key`, `abandoned` (5xx, Exception oder vor dem Handler abgewiesen, z. B. Antiforgery; Wiederholung läuft erneut), `stored_without_body` (Antwort > 1 MiB: nur Status und Header gespeichert), `store_failed`, `write_failed` (Handler fertig, Ergebnis nicht schreibbar; Schlüssel bleibt bis Lease-Ende reserviert) |
| `foodassistant.api.rate_limit.rejections` | Counter `{request}` | API – Rate-Limits | Mit 429 abgewiesene Anfragen | `policy`: `auth-strict`, `api`, `openid` (`/connect/*`, `/.well-known/*`, vor der Authentifizierung pro Netz), `global` (API-Anfragen ohne passenden Endpunkt) |
| `foodassistant.shopping.conflicts` | Counter `{conflict}` | Einkauf | Nach ADR-0005 aufgelöste Konflikte von Einträgen | `outcome`: `CheckedWins`, `ChangeWinsOverDelete`, `CheckedWinsOverDelete`, `BothDeleted`, `LastWriteWins` |
| `foodassistant.shopping.items.checked` | Counter `{item}` | Einkauf | Abgehakte Einträge (Übergang offen → abgehakt) | – |
