# Datenschutz

Jede neue Datenquelle, jeder neue Drittanbieter und jede neue Speicherung personenbezogener Daten
wird hier eingetragen.

## Wo Daten liegen
| Daten | Ort | Aufbewahrung |
|---|---|---|
| Konten, Mitgliedschaften, Geräte | PostgreSQL (Server) | bis Konto gelöscht (ADR-0002) |
| Vorrat, Einkaufslisten, Rezepte | PostgreSQL (Server) | bis Haushalt gelöscht + 30 Tage |
| Offline-Cache | SQLite auf dem Handy | bis Abmeldung/Haushaltswechsel |
| Aktivitätsverlauf | PostgreSQL | 90 Tage |
| Sicherheitsprotokoll | PostgreSQL | 180 Tage |
| Backups | Volume des Self-Hosters | Standard 14 Stück (ADR-0007) |
| Logs/Telemetrie | Server-Ausgabe, optional OTLP | nach Konfiguration; **keine Nutzerinhalte** |

## Was den Server verlässt
| Ziel | Wann | Daten |
|---|---|---|
| KI-Anbieter (Cloud) | nur wenn konfiguriert und Funktion genutzt | nur nötige Fachdaten (z. B. Vorratsnamen, Mengen, Rezeptwunsch), keine Personendaten |
| Ollama | wie oben, bleibt im eigenen Netz | wie oben |
| Open Food Facts | Barcode-Abfrage unbekannter Produkte | nur der Barcode |
| ntfy | wenn konfiguriert | Text der Benachrichtigung |
| Web-Push-Dienst des Browsers | Benachrichtigungen im Web | verschlüsselter Inhalt der Benachrichtigung |
| OTLP-Ziel | wenn konfiguriert | Telemetrie ohne Nutzerinhalte |

Ohne Konfiguration verlässt nur die Barcode-Abfrage an Open Food Facts den Server.

## Löschen und Export
- Konto selbst löschbar; Haushalt mit 30 Tagen Frist (ADR-0002).
- JSON-Export pro Haushalt (ADR-0007).
- Gelöschte Daten verschwinden aus Backups erst, wenn diese rotiert sind – in der Doku für
  Self-Hoster erwähnen.
