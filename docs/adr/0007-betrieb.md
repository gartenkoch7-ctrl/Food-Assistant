# ADR-0007: Betrieb – HTTPS, Konfiguration, Backup, Updates, Verteilung

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Self-Hoster betreiben Food-Assistant per Docker (ADR-0001). Sie haben unterschiedlich viel
Erfahrung. Android und iOS blockieren unverschlüsseltes HTTP standardmäßig.

## Entscheidung

### Erreichbarkeit und HTTPS
- **HTTPS ist Pflicht** für Zugriffe von außerhalb des Containers. Der Server selbst spricht HTTP
  und erwartet einen **Reverse-Proxy** (Caddy, Traefik, Nginx Proxy Manager) oder **Tailscale**.
- Mitgeliefert: `docker-compose.yml` mit Caddy-Beispiel und eine Tailscale-Anleitung.
- Forwarded Headers werden ausgewertet; vertrauenswürdige Proxys sind konfigurierbar.
- Die öffentliche Adresse (`FOODASSISTANT__PUBLICURL`) ist Pflicht – daraus entstehen Issuer,
  Redirect-URIs und QR-Codes.

### Konfiguration
- **Infrastruktur per Umgebungsvariablen:** Datenbank, öffentliche URL, externer OIDC-Dienst,
  OTLP-Ziel, Backup-Pfad, vertrauenswürdige Proxys.
- **App-Einstellungen im Admin-Bereich**, gespeichert in der Datenbank: KI-Anbieter, Modell,
  API-Key, KI-Budgets, ntfy, Standard-Benachrichtigungen, Haushaltszeitzone, Einheiten.
- Geheimnisse in der Datenbank (KI-API-Key, ntfy-Zugang, VAPID) sind mit **ASP.NET Data
  Protection** verschlüsselt; der Schlüsselring liegt in einem eigenen Volume.
- Eine gesetzte Umgebungsvariable **überschreibt und sperrt** den passenden UI-Wert (in der UI
  als „vom Server festgelegt“ angezeigt).

### KI-Budget
- Der Server-Admin legt pro Haushalt ein **Monatsbudget** fest (Anfragen oder Tokens).
- Verbrauch ist im Admin-Bereich sichtbar; bei Erreichen pausiert KI für den Haushalt bis
  Monatsende, mit verständlicher Meldung.
- Bei lokalem Anbieter (Ollama) standardmäßig **unbegrenzt**.

### Backup und Export
- **Automatisches Datenbank-Backup** täglich (Uhrzeit einstellbar) und **vor jedem Update**, in
  ein Volume; Anzahl aufbewahrter Backups einstellbar (Standard 14).
- **Haushaltsexport:** JSON-Export und -Import eines Haushalts (versioniertes Schema), auch zum
  Umzug auf einen anderen Server. Schema in `docs/kb/export-schema.md`.
- Wiederherstellung eines Datenbank-Backups per Befehl im Container, dokumentiert.

### Updates und Migrationen
- Update = neues Docker-Image ziehen und neu starten.
- Beim Start: **Backup → EF-Core-Migrationen** automatisch. Schlägt eine Migration fehl, startet
  der Server **nicht**, protokolliert die Ursache und nennt das Backup für den Rückweg.
- Migrationen sind nur **vorwärts**; ein Downgrade erfolgt über das Backup.
- **Versionsprüfung:** Die App liest `GET /api/v1/info` (ADR-0004) und meldet, wenn App oder
  Server zu alt sind.
- Versionierung nach **SemVer**; Changelog pro Release.

### Verteilung
- **Server:** Docker-Image (GitHub Container Registry), `linux/amd64` und `linux/arm64`
  (Raspberry Pi, NAS).
- **Android:** zunächst **APK über GitHub Releases**, später Play Store und F-Droid.
- **iOS:** später App Store; bis dahin Eigenbau.

## Konsequenzen

- Erststart ohne `PUBLICURL` oder Datenbank bricht mit klarer Meldung ab.
- Data-Protection-Schlüssel gehen bei Verlust des Volumes verloren → gespeicherte Geheimnisse
  müssen neu eingegeben werden (dokumentieren, im Backup mitsichern).
- Backup braucht `pg_dump` im Server-Image oder einen Hilfscontainer – Entscheidung im Detailplan.
