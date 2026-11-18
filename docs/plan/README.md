# Roadmap

Funktionsumfang im Detail: [produkt-und-scope.md](../kb/produkt-und-scope.md). Jeder Meilenstein
bekommt eine eigene Datei `docs/plan/mNN-<name>.md` mit Feature-Liste, Parallelisierung
(AGENTS.md §11) und Detailplan (§13), sobald er geplant wird.

| Meilenstein | Inhalt | Stand |
|---|---|---|
| **[M0 Grundgerüst](m00-grundgeruest.md)** | Solution und Projekte nach ADR-0001, Build/Tests/CI, Dateilängen-Skript, Architektur- und Telemetrie-Konventionstests, Docker-Compose (Server + PostgreSQL), Einrichtungsassistent, Konten/Haushalt/Rollen/Einladungen (ADR-0002/0003), API-Grundlagen (ADR-0004), MudBlazor-Theme aus dem Designsystem (ADR-0009) | fertig (Branch `feat/m00-foundation`) |
| **M1 MVP Vorrat & Einkauf** | Produkte + Chargen, Lagerorte, Haltbarkeitsregeln und Zustände, Barcode/Open Food Facts, Verbrauch (Tippen, Scan), Benachrichtigungen, mehrere Listen mit Läden und gelernter Reihenfolge, offline abhaken + Live-Updates, Einkauf abschließen, Mindestbestand, wiederkehrende Artikel, Verschwendungsstatistik, Etiketten (PDF), Import Grocy + CSV, Backup/Export | offen |
| **M2 Rezepte & Essensplan** | Rezepte (pro Haushalt, teilbar), Zutaten mit Produkten, „habe ich alles?“, Wochenplan mit Mahlzeiten, „Einkauf für diese Woche“, Kochen zieht ab, Import Mealie/Tandoor | offen |
| **M3 KI** | Rezeptideen aus dem Vorrat, Rezept-Import (Link/Foto/Text), Kochmodus mit Fragen, Wochenplan-Vorschlag, Kassenbon → Vorrat und Preise, KI-Budget | offen |
| **M4 Home Assistant** | HACS-Integration: Sensoren, Todo-Listen, Dienste, Assist-Intents (auf Liste, was läuft ab, verbraucht) | offen |
| **M5 Personen, Geld, Extras** | Preise/Budget, Verschwendung in €, Allergien und Vorlieben pro Person, Nährwerte pro Portion (abschaltbar), Bring!-Text-Import, Labeldrucker | offen |

Die Mobil-Apps (Android-APK) laufen ab M1 mit; iOS und Stores nach ADR-0007.
