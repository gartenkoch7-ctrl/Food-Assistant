# ADR-0006: Benachrichtigungen

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Erinnerungen wie „Milch läuft morgen ab“ sind Kern des Produkts. Ein selbst gehosteter Server
kann ohne zentralen Weiterleitungsdienst keine Push-Nachrichten über Google (FCM) oder Apple
(APNs) an eine fremd veröffentlichte App senden. Einen solchen Dienst wollen wir nicht betreiben.

## Entscheidung

- **Mobil: lokale Benachrichtigungen.** Die App plant Erinnerungen selbst aus den synchronisierten
  Ablaufdaten (ADR-0005) und plant bei jedem Sync neu. Funktioniert auch offline.
- **Web: Web Push** (VAPID). Der Server erzeugt das Schlüsselpaar beim ersten Start selbst und
  speichert es geschützt (Data Protection). Ein Hintergrunddienst prüft täglich die Ablaufdaten
  und sendet.
- **Optional: ntfy.** Self-Hoster tragen eine ntfy-URL (und ggf. Zugangsdaten) ein; dann gehen
  dieselben Meldungen zusätzlich dorthin – pro Person oder pro Haushalt.
- **Keine E-Mails.** Der Server versendet keine E-Mails (auch nicht für Einladungen oder
  Passwort-Reset, siehe ADR-0002/0003).
- Pro Person einstellbar: welche Meldungen (Ablauf, Einkaufsliste geändert, …), Vorlaufzeit
  (Standard: 2 Tage vor Ablauf), Uhrzeit (Standard: 8:00 in der Haushaltszeitzone), Ruhezeiten.
- Inhalte: kurz, ohne sensible Details auf dem Sperrbildschirm (nur Artikelname und Datum).
- **Sicherheitsrelevant:** Bei Verbrauchsdatum („zu verbrauchen bis“) ist der Text deutlicher als
  bei Mindesthaltbarkeit; die Formulierungen stehen in `docs/kb/vorrat-und-haltbarkeit.md`.

## Konsequenzen

- Benachrichtigungen sind hinter `INotificationScheduler` (Mobil) bzw. `INotificationSender`
  (Server: Web Push, ntfy) gekapselt.
- iOS begrenzt die Zahl geplanter lokaler Benachrichtigungen (64): die App plant nur die
  nächsten Fälle und fasst mehrere Artikel pro Tag zu einer Meldung zusammen.
- Wird später doch ein zentraler Push-Dienst gewünscht → neues ADR.
