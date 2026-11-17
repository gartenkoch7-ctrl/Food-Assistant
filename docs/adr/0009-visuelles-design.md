# ADR-0009: Visuelles Design

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Kontext

Web und Apps teilen eine Razor-Oberfläche auf MudBlazor (ADR-0001). Nutzer sind ganze Haushalte,
auch Kinder und ältere Personen; Ziel ist WCAG 2.2 AA (ADR-0008). Der Ablaufstatus ist die
wichtigste Information der App.

## Entscheidung

- **Grundstimmung:** ruhig und funktional – ein Werkzeug für den Alltag. Eine Hauptaktion pro
  Bildschirm, Status springt ins Auge, Dekoration nicht.
- **Hauptfarbe:** Tomate (`brand` #c0391f hell / #ff7a5c dunkel), sparsam: Hauptknopf, aktive
  Navigation, Wortmarke.
- **Ablaufstatus:** eigene Skala, **nie** Tomate – abgelaufen Karmin, bald Bernstein, haltbar
  Grün, ohne Datum neutral. Immer **Farbe + gefülltes Symbol + Text**.
- **Neutraltöne** leicht warm getönt; Fokusring blau (`focus`), damit er sich von Marke und Status
  abhebt.
- **Themes:** Hell, Dunkel, Hochkontrast; Standard folgt dem System.
- **MudBlazor mit eigenem Theme:** Komponenten bleiben, Farben/Schrift/Rundung/Schatten aus dem
  Designsystem. Eigene Komponenten nur, wo MudBlazor nichts Passendes hat (Status-Chip,
  Listenzeile, untere Leiste).
- **Schrift:** Atkinson Hyperlegible Next (Text), Figtree (Überschriften); kleinste Größe 14 px,
  Fließtext 16 px.
- **Symbole:** Material Symbols Rounded; Kategorie-Symbole in Listen, Produktfotos nur in der
  Detailansicht.
- **Dichte:** luftig – Ziele ≥ 48 px, Listenzeilen ≥ 64 px.
- **Navigation:** Handy untere Leiste (Vorrat, Einkauf, Kochen, Mehr) mit Scan-Knopf in der Mitte;
  ab 840 px Seitenleiste.
- **Formen:** Knöpfe und Chips als Pille, Karten 12 px, Sheets/Dialoge 20 px Rundung.
- **Ton:** Du, freundlich und knapp; keine Witze in Warnungen; MHD und Verbrauchsdatum werden
  unterschiedlich formuliert.
- **Name/Logo:** „Food-Assistant“ als Wortmarke in Figtree; eigenes Logo später.

## Quellen der Wahrheit

- Designsystem (Tokens, Leitfaden, Komponenten): Claude Design –
  https://claude.ai/artifact/JQzHoKt1PNRTDnRPtvNzQu
- Bildschirm-Entwürfe (Vorrat, Artikel, Einkauf, Scannen, Erkannt):
  https://claude.ai/artifact/MpAW2YuYdRfu4k6KPQSsJk
- Zusammenfassung und Abbildung auf MudBlazor: `docs/kb/design-system.md`.

## Konsequenzen

- Das MudBlazor-Theme (`MudTheme`) und eine CSS-Datei mit den Tokens werden aus den Tokens des
  Designsystems abgeleitet; Werte nicht von Hand abweichen lassen.
- Schriften und Symbole werden mit der App ausgeliefert (kein Laden von Google-Servern zur
  Laufzeit – Datenschutz, Offline).
- Tomate und Karmin liegen im Farbton nah beieinander; deshalb ist Text + Symbol beim Status Pflicht.
