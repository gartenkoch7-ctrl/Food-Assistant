# ADR-0008: Sprache, Einheiten und Barrierefreiheit

- **Status:** Akzeptiert
- **Datum:** 2026-11-17

## Entscheidung

### Sprache
- **Deutsch und Englisch** zum Start; weitere Sprachen über `.resx`-Dateien (Community-Beiträge).
- Sprache **pro Person** einstellbar; Standard ist die Sprache von Browser bzw. Gerät,
  Rückfall Englisch.
- Datums- und Zahlenformate folgen der Sprache der Person; die **Zeitzone gilt pro Haushalt**.
- Fehlertexte der API werden über `errorCode` in der UI übersetzt (ADR-0004).
- KI-Antworten in der Sprache der anfragenden Person.

### Einheiten
- **Metrisch** mit Umrechnung: g, kg, ml, l; dazu **Stück**.
- **Haushaltsmaße** als eigene Einheiten: EL, TL (ungefähre Umrechnung in ml), Prise, Bund,
  Packung, Dose (ohne Umrechnung).
- Keine imperialen Einheiten im ersten Ausbau.
- Mengen tragen ihre Einheit im Typ (`Quantity`); Umrechnung ist reine Domain-Logik.

### Barrierefreiheit
- Ziel **WCAG 2.2 AA** für Web und App.
- Vollständige Tastaturbedienung, sichtbarer Fokus, logische Fokusführung in Dialogen.
- Screenreader: NVDA (Web), TalkBack, VoiceOver – jede Aktion hat einen zugänglichen Namen.
- Kontraste nach AA, Hell/Dunkel/Hochkontrast, Schriftgröße bis 200 % ohne Funktionsverlust.
- Kein Inhalt nur über Farbe (z. B. Ablaufstatus zusätzlich als Text/Symbol).
- Barcode-Scan hat immer eine Alternative (manuelle Eingabe/Suche).
- Prüfung pro Feature über die Checkliste in `docs/kb/barrierefreiheit.md`; automatisiert mit
  axe in den UI-Tests, soweit möglich.

## Konsequenzen

- MudBlazor-Komponenten werden auf Barrierefreiheit geprüft; Lücken werden in der KB vermerkt
  und mit eigenen Wrappern geschlossen.
- Neue UI-Texte immer in beiden Sprachen.
