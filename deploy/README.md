# Food-Assistant selbst hosten (Docker)

Food-Assistant läuft als Docker-Stack aus **Server** und **PostgreSQL** (ADR-0007). Der Server
spricht im Container nur HTTP auf Port 8080. Von außen ist **HTTPS Pflicht** – über den
mitgelieferten **Caddy** oder über **Tailscale**. Die Android- und iOS-App verbinden sich nur per
HTTPS.

## Voraussetzungen

- Docker mit Compose v2 (`docker compose version`), Linux `amd64` oder `arm64` (auch Raspberry Pi, NAS)
- Für Caddy: eine Domain, die auf den Rechner zeigt, und erreichbare Ports 80 und 443

## Start

```bash
git clone <repo-url> food-assistant && cd food-assistant
cp .env.example .env
# .env bearbeiten: POSTGRES_PASSWORD (langes Zufallspasswort, z. B. `openssl rand -base64 32`)
# und FOODASSISTANT_PUBLICURL (spätere Adresse; für eine erste Prüfung http://localhost:8080).
docker compose up -d --build
docker compose ps          # beide Dienste "healthy"?
curl http://localhost:8080/api/v1/info
```

Ohne Reverse-Proxy ist der Server nur auf diesem Rechner erreichbar (`127.0.0.1:8080`).

**Über einfaches HTTP lässt sich nicht anmelden.** Sitzungs- und Antiforgery-Cookie sind nur
über HTTPS erlaubt (`Secure`, `__Host-`-Präfix). `http://localhost:8080` zeigt deshalb nur, dass
der Server läuft (`/health`, `/api/v1/info`); Einrichtungs- und Anmeldeseite melden „Anmelden
und Einrichten gehen nur über HTTPS“ (`accounts.https_required`). Zum Einrichten und Anmelden HTTPS nutzen – Caddy oder
Tailscale, siehe unten.

### HTTPS mit Caddy

In `.env` `FOODASSISTANT_DOMAIN=food.example.com` setzen, dann:

```bash
docker compose -f docker-compose.yml -f deploy/caddy/docker-compose.caddy.yml up -d --build
```

Caddy holt und erneuert das Zertifikat selbst (Let's Encrypt). Die öffentliche Adresse wird
automatisch auf `https://<FOODASSISTANT_DOMAIN>` gesetzt.

### HTTPS mit Tailscale

Ohne Domain und ohne offene Ports: Tailscale auf dem Rechner installieren und HTTPS für das
Tailnet aktivieren (Admin-Konsole → DNS → *HTTPS Certificates*). Dann:

```bash
tailscale serve --bg --https=443 http://127.0.0.1:8080
```

In `.env` `FOODASSISTANT_PUBLICURL=https://<rechner>.<tailnet>.ts.net` setzen und
`docker compose up -d` erneut ausführen. Die Geräte (Handy, Laptop) brauchen ebenfalls Tailscale.
`tailscale serve` erreicht den Container über das Docker-Netz; dessen Adressen sind in der Standardeinstellung von `FOODASSISTANT_TRUSTEDPROXIES` enthalten.

## Einstellungen (Umgebungsvariablen)

Infrastruktur wird per Umgebungsvariablen in `.env` gesetzt; App-Einstellungen (KI, Benachrichtigungen …)
später im Admin-Bereich.

| Variable in `.env` | Pflicht | Bedeutung |
|---|---|---|
| `POSTGRES_PASSWORD` | ja | Passwort der Datenbank |
| `FOODASSISTANT_PUBLICURL` | ja¹ | Adresse, unter der Handys und Browser den Server erreichen; für eine erste Prüfung `http://localhost:8080` (nur dieser Rechner, nur `/health` und `/api/v1/info`; Anmelden braucht HTTPS). Mit Caddy `https://<FOODASSISTANT_DOMAIN>`. |
| `FOODASSISTANT_DOMAIN` | nur Caddy | Domain für das HTTPS-Zertifikat |
| `FOODASSISTANT_TRUSTEDPROXIES` | nein | Reverse-Proxys, deren `X-Forwarded-*`-Header gelten (IPs/CIDR, kommagetrennt); Standard `172.16.0.0/12` (Docker-Netze). Loopback gilt immer. Wer den Server-Port über `127.0.0.1` hinaus freigibt, setzt hier nur die echten Proxys. |
| `FOODASSISTANT_PORT` | nein | Port auf dem Rechner, Standard `8080` |
| `POSTGRES_PORT` | nein | Port der Datenbank auf dem Rechner (nur für Entwicklung), Standard `5432` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | nein | OpenTelemetry-Collector für Logs, Traces und Messwerte. Leer: nichts verlässt den Server. |

¹ Außerhalb der Entwicklung startet der Server ohne gültige öffentliche Adresse nicht und nennt
den Grund im Log (`docker compose logs server`).

Weitere Server-Einstellungen lassen sich direkt im Abschnitt `environment` von
`docker-compose.yml` setzen, z. B. `FOODASSISTANT__MINIMUMAPPVERSION` oder die Rate-Limits
`FOODASSISTANT__RATELIMITS__APIPERMITLIMIT` (Anfragen pro Minute und Person/IP, Standard 600) und
`FOODASSISTANT__RATELIMITS__AUTHSTRICTPERMITLIMIT` (Anmeldung, Standard 5 pro Minute und IP).

`ASPNETCORE_ENVIRONMENT` bleibt `Production`. Die Umgebungen `Development` und `Testing` sind nur
für Entwicklung und automatische Tests: Sie verzichten auf die öffentliche Adresse und HTTPS-Pflicht
der Anmeldung und halten Token-Schlüssel nur im Speicher (nach jedem Neustart sind alle Handys
abgemeldet).

## Daten und Volumes

| Volume | Inhalt | Sichern? |
|---|---|---|
| `db-data` | PostgreSQL-Datenbank | ja (über die Backups) |
| `data-protection-keys` | Schlüssel, mit denen gespeicherte Geheimnisse (KI-Key, ntfy …) verschlüsselt sind | **ja** – ohne sie müssen Geheimnisse neu eingegeben werden |
| `backups` | automatische Datenbank-Backups (folgt mit dem Backup-Feature) | ja, zusätzlich außerhalb des Rechners |

Die Volumes überleben `docker compose down` und Updates. `docker compose down -v` **löscht alle
Daten unwiderruflich**.

## Update

```bash
git pull
docker compose up -d --build     # mit Caddy: dieselben -f-Optionen wie beim Start
docker image prune -f            # alte Images aufräumen (optional)
```

Beim Start sichert der Server die Datenbank und führt danach die Migrationen aus (ADR-0007, folgt
mit der Persistenz). Migrationen gehen nur vorwärts; zurück geht es über das Backup.

## Fehlersuche

- `docker compose logs -f server` – Startfehler (z. B. fehlende öffentliche Adresse) stehen hier.
- `docker compose ps` – Status der Healthchecks.
- `curl http://localhost:8080/health` – antwortet `Healthy`, wenn der Server läuft.
- Alle Anmeldungen teilen sich ein Rate-Limit oder Links zeigen `http://` statt `https://`: Das
  Docker-Netz liegt außerhalb von `172.16.0.0/12` (Docker weicht bei vielen Netzen auf
  `192.168.x.x` aus). Subnetz mit `docker network inspect food-assistant_default` nachsehen und in
  `.env` als `FOODASSISTANT_TRUSTEDPROXIES` eintragen.
