# Barrierefreiheit – Checkliste pro Feature

Ziel WCAG 2.2 AA (ADR-0008). Jede neue Seite, jeder Dialog und jede Komponente wird hiermit
geprüft; Ergebnis kurz im PR nennen.

## Checkliste
- [ ] Alles per **Tastatur** erreichbar und bedienbar, Reihenfolge logisch, kein Fokus-Gefängnis
- [ ] **Fokus sichtbar**; Dialoge setzen den Fokus hinein und beim Schließen zurück
- [ ] Jedes Bedienelement hat einen **zugänglichen Namen** (auch Symbolknöpfe)
- [ ] Formularfelder mit Label; Fehler am Feld, per Screenreader angesagt
- [ ] **Kontrast** ≥ 4,5 : 1 (Text) bzw. 3 : 1 (große Schrift, Bedienelemente) in Hell, Dunkel, Hochkontrast
- [ ] Information **nicht nur über Farbe** (Ablaufstatus zusätzlich Text/Symbol)
- [ ] **200 % Schrift** bzw. große Systemschrift ohne abgeschnittene Inhalte
- [ ] Zielgrößen ≥ 24 × 24 px (WCAG 2.5.8)
- [ ] Live-Änderungen (SignalR, Sync) dezent angesagt (`aria-live="polite"`), nicht störend
- [ ] **Barcode-Scan** hat eine Alternative (manuelle Eingabe/Suche)
- [ ] Getestet mit: NVDA (Web) · TalkBack (Android) · VoiceOver (iOS, sobald verfügbar)
- [ ] axe-Prüfung in den UI-Tests ohne Befund (soweit automatisierbar)

## Bekannte Lücken
Hier MudBlazor- oder Plattform-Eigenheiten und deren Umgehung eintragen.

- **MudBlazor 9, `Error`/`ErrorText` von außen** (z. B. Feldfehler aus einer 400-Antwort): Das Feld
  wird rot und `aria-invalid`, der `ErrorText` erscheint aber nicht (beobachtet in bUnit). Umgehung:
  eigenes Fehlerelement unter dem Feld und `ErrorId` darauf setzen (→ `aria-describedby`), siehe
  `UI/ShelfLife/ShelfLifeDaysDialog.razor`.
- **MudBlazor 9, `MudSelect` mit `Error` + `ErrorText` + `ErrorId`** (M1 F): Hier erscheint der Text (beobachtet in
  bUnit) im eigenen Hilfetext-Container der Komponente, und **dieser** bekommt die `ErrorId`; Eingabe und
  Combobox verweisen per `aria-describedby` darauf. Ein zusätzliches eigenes Element mit derselben Id doppelt
  die Id (der leere Container kommt zuerst und gewinnt). Muster: `UI/Checkout/CheckoutLineRow.razor`.
