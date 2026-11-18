# Designsystem

Entscheidung: [ADR-0009](../adr/0009-visuelles-design.md). Quelle der Wahrheit für Werte ist das
Designsystem in Claude Design: https://claude.ai/artifact/JQzHoKt1PNRTDnRPtvNzQu
(Entwürfe: https://claude.ai/artifact/MpAW2YuYdRfu4k6KPQSsJk). Ändert sich dort ein Wert, wird
diese Datei und das MudBlazor-Theme nachgezogen.

## Farben (Hell / Dunkel / Hochkontrast)

| Token | Hell | Dunkel | Hochkontrast | Verwendung |
|---|---|---|---|---|
| `surface` | #fbf9f7 | #171412 | #ffffff | Seitenhintergrund |
| `surface-raised` | #ffffff | #211d1a | #ffffff | Karten, Leisten |
| `surface-sunken` | #f2eeea | #110f0d | #ffffff | Eingaben, Suche |
| `line` | #e2dbd4 | #3a332e | #000000 | Trennlinien |
| `line-strong` | #8c8178 | #857a71 | #000000 | Ränder von Eingaben |
| `ink` | #221c18 | #f4efea | #000000 | Text |
| `ink-muted` | #6b615a | #b3a89f | #2b2b2b | Nebentext |
| `brand` | #c0391f | #ff7a5c | #9e2a12 | Hauptaktion, aktive Navigation |
| `brand-strong` | #a12f18 | #ff9a82 | #7a1f0b | Hover/gedrückt |
| `brand-tint` | #fbe6e0 | #5a2416 | #ffffff | Auswahl-Hintergrund |
| `on-brand` | #ffffff | #1a1513 | #ffffff | Text auf `brand` |
| `focus` | #1c5fd1 | #8ab4ff | #0038a8 | Fokusring |
| `status-expired` / `-bg` | #8a0f3c / #fbe4ec | #ff9fc0 / #4a0f25 | #6b0a2e / #ffffff | abgelaufen |
| `status-soon` / `-bg` | #7a4b00 / #fff1d6 | #ffcb66 / #3d2a00 | #5c3800 / #ffffff | läuft bald ab |
| `status-ok` / `-bg` | #1e5e2e / #e3f2e6 | #8edba2 / #12331c | #0f4420 / #ffffff | haltbar |
| `status-none` / `-bg` | #6b615a / #f2eeea | #b3a89f / #2a2521 | #2b2b2b / #ffffff | ohne Datum |
| `danger` | #8a0f3c | #ff9fc0 | #6b0a2e | Löschen, Fehler (nur mit Text) |

## Schrift

| Stil | Familie | Größe/Zeile | Gewicht |
|---|---|---|---|
| display | Figtree | 32/38 | 700 |
| title | Figtree | 24/30 | 700 |
| heading | Figtree | 20/26 | 600 |
| body-lg | Atkinson Hyperlegible Next | 18/26 | 400 |
| body | Atkinson Hyperlegible Next | 16/24 | 400 |
| label | Atkinson Hyperlegible Next | 15/20 | 700 |
| caption | Atkinson Hyperlegible Next | 14/20 | 400 |

## Maße

- Abstände: 4, 8, 12, 16, 24, 32 px (`space-1` … `space-8`).
- Rundung: 8 (Eingaben), 12 (Karten), 20 (Sheets), Pille (Knöpfe, Chips).
- Ziele ≥ 48 px, Listenzeile ≥ 64 px, Symbol 24 px, Scan-Knopf 64 px.

## Komponenten (eigene)

- **StatusChip:** Farbe + gefülltes Symbol (`error`, `schedule`, `check_circle`, `help`) + Text.
- **ListRow:** Kategorie-Symbol, Name (`body-lg`), Nebenangabe (`caption`), rechts Status oder
  Abhaken-Knopf.
- **BottomNav:** Vorrat (`kitchen`), Einkauf (`shopping_cart`), Scan-Knopf (`barcode_scanner`),
  Kochen (`skillet`), Mehr (`more_horiz`); ab 840 px Seitenleiste.
- Alles andere: MudBlazor mit Theme (Abbildung siehe Designsystem-README, „Umsetzung in MudBlazor“).

## Offen

- **Schriftdateien fehlen noch.** Atkinson Hyperlegible Next und Figtree sind im Theme (`AppTypography`) und in `design-tokens.css` bereits eingetragen, die Dateien selbst aber noch nicht eingebunden. Bis dahin greifen die Rückfälle `"Segoe UI", system-ui, sans-serif`. Zu tun: `.woff2` (lizenzkonform, SIL OFL) in `src/FoodAssistant.UI/wwwroot/fonts/` ablegen, `@font-face` mit `font-display: swap` in `design-tokens.css` ergänzen und die Lizenzhinweise in `docs/kb/externe-daten.md` vermerken. Nicht von Google-Servern laden (ADR-0009).
- **Symbole:** Statt Material Symbols Rounded (Schriftart) nutzt die UI die in MudBlazor enthaltenen `Icons.Material.Rounded.*` (SVG, gefüllt, keine Icon-Schrift). Das Navigationssymbol für „Kochen“ ist `SoupKitchen`, das Scan-Symbol `QrCodeScanner`: `skillet` und `barcode_scanner` gibt es im MudBlazor-Satz nicht. Bei Bedarf eigene SVGs ergänzen.
- **PWA-Symbole:** Das Manifest nennt nur `icon.svg` (Platzhalter ohne Logo). Für die Installierbarkeit auf allen Systemen werden PNGs in 192 und 512 px (auch maskierbar) benötigt; sie entstehen mit dem endgültigen Logo.
- **Hochkontrast** folgt der Tabelle, hat aber keine Systemerkennung (`forced-colors`); er wird nur auf der Seite „Mehr“ gewählt.
- **Theme-Flackern:** Beim Vorrendern und bis zum ersten Render im Browser trägt die MudBlazor-Palette immer das helle Theme, die eigenen `--fa-*`-Werte folgen dagegen schon der gespeicherten Wahl. MudBlazor-Komponenten (Knöpfe, Symbole) können deshalb kurz hell auf dunklem Grund erscheinen. Abhilfe später: `--mud-palette-*` in `design-tokens.css` je Theme auf die `--fa-*`-Werte abbilden.
