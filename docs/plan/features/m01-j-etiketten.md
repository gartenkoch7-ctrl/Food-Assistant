# M1 J – Etiketten (PDF)

PDF mit Etiketten für ausgewählte Chargen (v. a. eingefroren, Reste) auf gängigen Bögen: Name,
Datum eingefroren/zubereitet, Ablaufdatum mit Art, QR-Code mit **nur** der Chargen-Adresse
(`{PublicUrl}/inventory/batches/{id}`, öffnet B2-Seite; C2 erkennt ihn beim Scannen).
Labeldrucker erst M5. KB: [vorrat-und-haltbarkeit](../../kb/vorrat-und-haltbarkeit.md) „Etiketten“.

Branch `feat/m01-j-labels` · M1b · ein Paket. Vor Start: Detailplan gegen Stand nach M1a prüfen.

## Aufgaben
- [ ] J Bogenformate, Layout, PDF-Renderer, Endpunkt, Auswahlseite

## Detailplan

### Paket J – Etiketten

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Akzeptanz M1a · **Blockiert durch:** F9 (Bibliothek; Plan nimmt Empfehlung a an)
- **Muster:** Domain rein (wie `FefoPlanner.cs`); Renderer hinter Interface in Infrastructure (wie `OpenFoodFactsApi`, C1).

**Pakete (F9 a):** `PDFsharp` 6.x (MIT), `QRCoder` (MIT, schon durch K vorhanden). Schrift:
Atkinson Hyperlegible Next (SIL OFL) als `.ttf` eingebettete Ressource + `IFontResolver` (der
Container hat keine Systemschriften); Lizenz in `docs/kb/externe-daten.md`.

**Formate** (A4, mm): `avery-l7160` 3×7, 63,5×38,1, Rand oben 15,15, links 7,2, Abstand x 2,5, y 0;
`avery-l7163` 2×7, 99,1×38,1, oben 15,15, links 4,65, x 2,5, y 0;
`avery-l7651` 5×13, 38,1×21,2, oben 10,7, links 4,7, x 2,5, y 0; plus `custom` aus der Anfrage
(Grenzen: 1–10 Spalten, 1–30 Zeilen, alles innerhalb A4). Startposition 1…Anzahl (angebrochene Bögen).
Inhalt je Etikett: Produktname (max. 2 Zeilen, gekürzt mit …), „Eingefroren am 18.11.“ bzw.
„Zubereitet am …“ falls vorhanden, „Verbrauchen bis …“ / „Mindestens haltbar bis …“ / „Kein Datum“,
QR rechts (Kantenlänge = min(Höhe − 4 mm, 25 mm)). Sprache aus Anfrage-Kultur.

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Domain/Labels/LabelSheetFormat.cs` + `LabelSheetFormats.cs` + `LabelLayout.cs` | neu | Record mit Maßen + `Validate()`; Standardformate; `static IReadOnlyList<LabelSlot> Slots(LabelSheetFormat f, int startPosition, int count)` (Seiten, x/y in mm) | `LabelLayoutTests` | 40+40+60 |
| `src/FoodAssistant.Application/Labels/LabelContent.cs`, `ILabelRenderer.cs`, `ILabelStore.cs`, `LabelService.cs`, `LabelsLog.cs` | neu | `LabelContent(string Name, string? StateLine, string ExpiryLine, Uri QrTarget)`; `Task<Stream> RenderAsync(LabelSheetFormat, IReadOnlyList<LabelSlot>, IReadOnlyList<LabelContent>, CancellationToken)`; Store lädt Chargen + Produktnamen (fremde Ids → `NotFound`); Service baut Texte (Lokalisierung `LabelText.resx`), max. 200 Etiketten; Log `LabelsRendered` (3700, Information, `Count`, `Format`) | `LabelServiceTests` | 4×20+100+15 |
| `src/FoodAssistant.Application/Labels/LabelText.cs` + `.resx` + `.en.resx` | neu | Zeilentexte | – | 5+2×10 |
| `src/FoodAssistant.Infrastructure/Labels/PdfLabelRenderer.cs` + `EmbeddedFontResolver.cs` + `Fonts/*.ttf` | neu | PDFsharp: Seite A4, Text mit `XTextFormatter`, QR als Vektor-Rechtecke aus `QRCodeGenerator.CreateQrCode(..).ModuleMatrix` | `PdfLabelRendererTests` (PDF beginnt mit `%PDF`, Seitenzahl, keine Ausnahme bei 200 Etiketten) | 140+40 |
| `src/FoodAssistant.Contracts/Labels/LabelRequest.cs` + `LabelSheetFormatDto.cs` + `LabelErrorCodes.cs` | neu | `LabelRequest(IReadOnlyList<Guid> BatchIds, string FormatId, int StartPosition, LabelSheetFormatDto? Custom)` | – | 3×12 |
| Registrierungen (Stubs Labels) | ändern | | – | +3 |
| `src/FoodAssistant.Server/Labels/LabelEndpoints.cs` | ändern (Stub) | `POST /labels` (Member) → `application/pdf` (`Results.File`, Name `etiketten.pdf`), `.WithoutIdempotency()` (keine Zustandsänderung); `GET /labels/formats` (Any); `PublicUrl` aus `HostingOptions` für `QrTarget` | `LabelEndpointTests` (PDF, fremde Charge 404, Eingeschränkt 403, ungültiges Format 400) | 60+150 |
| `src/FoodAssistant.Client/Labels/*`, Registrierung | neu | `GetPdfAsync(…)` → Stream, `ListFormatsAsync` | – | 50 |
| `src/FoodAssistant.UI/Labels/LabelsPage.razor` + `LabelsText.cs` + resx + `UI/wwwroot/download.js` | neu | `@page "/labels"`: Chargenauswahl (Filter „eingefroren“, „Reste“, „letzte 7 Tage“, Checkboxen), Format, Startposition (Rasterbild des Bogens mit nummerierten, per Tastatur wählbaren Feldern), „PDF erstellen“ → Download über JS (`URL.createObjectURL`) | `LabelsPageTests` | 200+5+2×30+30 |
| `src/FoodAssistant.UI/Shell/MorePage.razor`, `ShellText*.resx` | ändern (Hotspot M1b) | Link „Etiketten drucken“ | – | +5 |
| `docs/kb/externe-daten.md`, `docs/kb/telemetrie.md`, `openapi-v1.json` | ändern | Schrift-/Bibliothekslizenzen; Log, Ablauf `foodassistant.labels.render` | – | – |

**Akzeptanz:** Tests grün; Sichtprüfung: PDF für L7160 drucken (oder als PDF ansehen), QR mit
Handy scannen → öffnet die Charge (nach Anmeldung).
