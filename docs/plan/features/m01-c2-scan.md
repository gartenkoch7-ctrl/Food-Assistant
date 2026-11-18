# M1 C2 – Scan-Ablauf im Web

Scan-Seite mit Kamera (Browser) und **immer** manueller Alternative (Barcode eintippen, Produkt
suchen). Modi **Einbuchen** und **Verbraucht**. Bekanntes Produkt → Charge einbuchen bzw. FEFO
verbrauchen; unbekannt → Open-Food-Facts-Vorschlag → Produkt anlegen → Charge einbuchen.
Etiketten-QR (`/inventory/batches/{id}`) wird erkannt. KB: [externe-daten](../../kb/externe-daten.md)
(Attribution), [barrierefreiheit](../../kb/barrierefreiheit.md) („Barcode-Scan hat eine Alternative“).

Branch `feat/m01-c2-scan` · Welle 2 · ein Paket.

## Aufgaben
- [ ] C2 Scanner-Schnittstelle, Browser-Scanner, Scan-Seite, Abläufe, Tests

## Detailplan

### Paket C2 – Scan

- **Stufe / Modell / Effort:** 2 / Sonnet / medium
- **Wartet auf:** Merge A, B2, C1 · **Blockiert durch:** F6 (Modus „Verbraucht“), F5 (über `AddBatchDialog`, schon in B2 gelöst)
- **Muster:** JS-Aufrufe wie `UI/Common/BrowserInterop.cs` + `UI/wwwroot/foodassistant.js` (Modul,
  Fehler abfangen, `CommonLog.BrowserCallUnavailable`-Muster); Dialoge aus A2 (`ProductEditDialog`, `ProductPicker`)
  und B2.3 (`AddBatchDialog`); Tests wie A2.

**Scanner im Browser:** `BarcodeDetector` (Formate `ean_13`, `ean_8`, `upc_a`, `upc_e`, `qr_code`),
falls verfügbar; sonst **ZXing** (`@zxing/browser` 0.1.5, MIT, mit `@zxing/library` 0.21.3,
Apache-2.0) lokal unter `UI/wwwroot/lib/zxing/` (keine CDN, ADR-0009-Gedanke: nichts von Fremdservern
laden). Kamera nur nach Knopfdruck (`getUserMedia({ video: { facingMode: "environment" } })`),
stoppt beim Verlassen der Seite. Gleicher Code 2 s lang ignorieren (Entprellung).

**Verbraucht-Menge (F6, Annahme a):** Einheit des frühesten Bestands `piece`/`package`/`can` →
1 abziehen (`Partial` bzw. `UsedUp`, wenn Rest = 1); Masse/Volumen → ganze früheste Charge
`UsedUp`. Danach Snackbar „{Produkt}: verbraucht“ mit „Rückgängig“ (B2-Undo) und „Menge ändern“
(öffnet `ConsumeDialog`).

| Datei | neu/ändern | Was genau | Tests | ca. Zeilen |
|---|---|---|---|---|
| `src/FoodAssistant.Client/Scan/IBarcodeScanner.cs` | neu | `Task<bool> IsAvailableAsync(CancellationToken)`; `Task StartAsync(string videoElementId, Func<string, Task> onCode, CancellationToken)`; `Task StopAsync()` – Doku: Mobil (Spur M3) liefert eine native Implementierung | – | 25 |
| `src/FoodAssistant.Client/FoodFacts/IFoodFactsClient.cs` + `FoodFactsClient.cs` | neu | `LookupAsync(Guid householdId, string barcode, CancellationToken)` → `ApiResult<FoodFactsProductDto>` | `FoodFactsClientTests` | 15+40 |
| `src/FoodAssistant.Client/Composition/FoodFactsClientRegistration.cs` | ändern (Stub) | Registrierung | – | +1 |
| `src/FoodAssistant.UI/wwwroot/scan.js` | neu | ES-Modul: `isAvailable()`, `start(videoId, dotnetRef)`, `stop()`; ruft `dotnetRef.invokeMethodAsync("OnCode", text)`; Fallback lädt `./lib/zxing/zxing-browser.min.js` dynamisch | manuell (Browser-Pane) | 120 |
| `src/FoodAssistant.UI/wwwroot/lib/zxing/zxing-browser.min.js` + `LICENSE` | neu | vendored Datei (Version oben), Lizenztext daneben | – | (Bibliothek) |
| `src/FoodAssistant.UI/Scan/BrowserBarcodeScanner.cs` | neu | implementiert `IBarcodeScanner` über `IJSRuntime` + `DotNetObjectReference`; `IAsyncDisposable`; JS-Fehler → `ScanLog.CameraUnavailable` | – | 90 |
| `src/FoodAssistant.UI/Scan/ScanLog.cs` | neu | `CameraUnavailable` (4400, Information, `Reason` als Fehlername, kein Text) | – | 15 |
| `src/FoodAssistant.UI/Scan/ScanCodeParser.cs` | neu | `static ScanCode Parse(string raw, Uri appBase)`: Ziffern 8/12/13/14 → `Barcode`; URL mit gleichem Host und Pfad `/inventory/batches/{guid}` → `BatchLink`; sonst `Unknown` | `ScanCodeParserTests` | 50 |
| `src/FoodAssistant.UI/Scan/ScanPage.razor` | ändern (ersetzt Platzhalter aus S0.5) | Modus-Umschalter (Einbuchen/Verbraucht, Radiogruppe), Kamera-Bereich mit Start/Stop-Knopf (ohne Kamera: Hinweis), **immer sichtbar**: Feld „Barcode eingeben“ + `ProductPicker` („Produkt suchen“); Ergebnis-Ansage über `ILiveAnnouncer` | `ScanPageTests` | 150 |
| `src/FoodAssistant.UI/Scan/ScanFlow.cs` | neu | Ablaufsteuerung (testbar ohne Razor): `Task<ScanOutcome> HandleAsync(ScanCode code, ScanMode mode, CancellationToken)` mit `ICatalogClient.FindByBarcodeAsync` → bekannt: Einbuchen → `AddBatchDialog` (Produkt, Standardeinheit, `Origin = Scanned`), Verbraucht → F6-Regel über `IInventoryClient.ConsumeProductAsync`; unbekannt → `IFoodFactsClient.LookupAsync` → `ProductEditDialog` mit Prefill (Name, Marke, Kategorie, Barcode, Standardeinheit aus `PackageQuantity.Unit` oder `piece`, `FoodFactsBarcode`), danach `AddBatchDialog` mit `PackageQuantity` als Menge; OFF nicht verfügbar → Dialog leer mit Barcode; `BatchLink` → Navigation | `ScanFlowTests` (alle Zweige mit Fakes) | 200 |
| `src/FoodAssistant.UI/Scan/FoodFactsAttribution.razor` | neu | Zeile „Daten von Open Food Facts (ODbL)“ mit Link `https://world.openfoodfacts.org`; wird bei OFF-Vorbelegung über den Parameter `Notice` (A2) in `ProductEditDialog` angezeigt | – | 25 |
| `src/FoodAssistant.UI/Scan/ScanText*.resx` | ändern | alle Texte, Kamera-Hinweise, Attribution, `Error.food_facts.*` | `ResourceCompletenessTests` | +2×40 |
| `tests/FoodAssistant.UI.Tests/Scan/*` + `Support/FakeBarcodeScanner.cs`, `FakeFoodFactsClient.cs` | neu | bUnit/Unit | ja | 3×120+80 |

**Barrierefreiheit:** Checkliste; Scan nie einziger Weg; Ergebnisse angesagt; Kamera-Start ist
ein beschrifteter Knopf; Fokus nach Dialogen zurück aufs Barcode-Feld.
**Akzeptanz:** Tests grün; Sichtprüfung im Browser-Pane mit Webcam oder per manueller Eingabe:
bekannter Barcode einbuchen und verbrauchen, unbekannter Barcode mit OFF-Vorschlag anlegen.
`docs/kb/externe-daten.md`: ZXing-Lizenzen eintragen.
