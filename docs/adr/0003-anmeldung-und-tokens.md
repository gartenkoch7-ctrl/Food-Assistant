# ADR-0003: Anmeldung, Tokens und Zugriffsschutz

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Web (Blazor WebAssembly, gleiche Domain) und Mobil (MAUI, eigene Geräte) melden sich am
Self-Hosted-Server an. Optional bindet der Self-Hoster einen externen OIDC-Dienst an
(Authentik, Authelia, Keycloak). Später sollen Home Assistant und Skripte zugreifen können.
Es gibt keinen E-Mail-Versand.

## Entscheidung

### Anmeldedienst
- Der Server ist mit **OpenIddict** sein **eigener OIDC-Anbieter**; Konten verwaltet
  **ASP.NET Core Identity** (PostgreSQL).
- **Web:** Backend-for-Frontend – der Server hält die Tokens, der Browser bekommt nur ein
  `HttpOnly`-, `Secure`-, `SameSite=Strict`-Cookie. Keine Tokens im Browser-Speicher.
- **Mobil:** Authorization Code Flow mit **PKCE** im System-Browser; Tokens im sicheren Speicher
  des Geräts (`SecureStorage`).
- **Externer OIDC-Dienst (optional):** erscheint als zusätzlicher Login-Knopf. Er **verknüpft nur**
  bestehende oder per Einladung angenommene Konten; unbekannte externe Logins werden abgewiesen,
  es werden nie automatisch Konten angelegt.

### Laufzeiten
| Token/Sitzung | Gültigkeit |
|---|---|
| Access-Token | 15 Minuten |
| Refresh-Token App | 90 Tage, gleitend, Rotation bei jeder Nutzung |
| Web-Sitzung (Cookie) | 14 Tage, gleitend |
| Einladung | 7 Tage, einmalig (ADR-0002) |
| Passwort-Reset-Link | 24 Stunden, einmalig |

### Geräteverwaltung
- Jede Anmeldung (App, Browser) ist ein **Gerät** mit Name, Typ, letzter Nutzung.
- Jede Person sieht ihre Geräte und kann jedes einzeln abmelden (widerruft das Refresh-Token).
- Besitzer und Server-Admin können alle Geräte einer Person abmelden.

### Schutz
- **Passwortregeln:** mindestens 10 Zeichen, keine Zeichenklassen-Pflicht.
- **Kontosperre:** nach 5 Fehlversuchen 15 Minuten gesperrt; im Sicherheitsprotokoll vermerkt.
- **Rate-Limits** (ASP.NET Core Rate Limiting): streng für Login, Reset, Einladung annehmen;
  großzügig für die übrige API, pro Person bzw. pro IP.
- **Keine** Zwei-Faktor-Anmeldung und **keine** Passkeys im ersten Ausbau; der Anmeldedienst
  (OpenIddict/Identity) lässt beides später zu → dann neues ADR.
- CSRF-Schutz für Cookie-Endpunkte (Antiforgery), CORS nur für die eigene Origin.

### Passwort zurücksetzen
- Besitzer (für Haushaltsmitglieder) oder Server-Admin erzeugen einen **Reset-Link/QR-Code**.
- **Notweg** für den einzigen Server-Admin: Befehl im Container, z. B.
  `docker compose exec server foodassistant admin reset-password <benutzer>`; gibt einen
  Reset-Link aus.

### Kopplung der App
- Die App fragt die **Server-Adresse** per **QR-Code** (aus Web-App bzw. Einladung) oder
  manueller Eingabe ab, prüft `/.well-known/openid-configuration` und die API-Version
  (ADR-0007) und startet danach den Login.

### Fremdzugriff
- **Persönliche API-Tokens**: jede Person erzeugt in den Einstellungen Tokens mit
  **Rechtebereichen** (z. B. `shopping:read`, `shopping:write`, `inventory:read`) und
  **Ablaufdatum**; jederzeit widerrufbar. Gespeichert nur als Hash.
- Tokens erben höchstens die Rechte der Person; eingeschränkte Personen können keine
  Schreib-Scopes außerhalb ihrer Rolle erzeugen.
- Grundlage für eine spätere Home-Assistant-Integration.

## Konsequenzen

- OpenIddict ist sicherheitskritisch: Änderungen daran sind Stufe 3 (AGENTS.md §13).
- Die MAUI-App braucht Redirect-URI-Handling pro Plattform (Custom Scheme).
- Hinter dem Reverse-Proxy müssen Forwarded Headers korrekt konfiguriert sein, sonst stimmen
  Issuer und Redirect-URIs nicht (ADR-0007).
