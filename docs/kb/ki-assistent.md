# KI-Assistent

Regeln: AGENTS.md §2.3. Grundsatz: ADR-0001 (Microsoft.Extensions.AI), ADR-0007 (Budget).

## Anbieter
| Anbieter | Paket (nur Infrastructure) | Hinweis |
|---|---|---|
| Ollama (lokal) | OllamaSharp (`IChatClient`) | kostenlos, privat, braucht passende Hardware; Budget standardmäßig unbegrenzt |
| Anthropic Claude | Anthropic-SDK mit `IChatClient`-Adapter | kostenpflichtig, beste Qualität |
| OpenAI-kompatibel | `Microsoft.Extensions.AI.OpenAI` | auch für LM Studio, vLLM u. ä. |

- Auswahl, Modell und API-Key im Admin-Bereich (ADR-0007); ohne Konfiguration ist KI **aus**,
  und alle KI-Funktionen sind in der UI ausgeblendet bzw. deaktiviert.

## Prompts
- Liegen versioniert als Dateien (Ablage wird mit dem ersten KI-Feature festgelegt und hier
  eingetragen), je Prompt: Zweck, Eingaben, erwartetes Ausgabeschema.
- Strukturierte Antworten werden gegen ein JSON-Schema validiert; ungültig → Fehler
  `ai.invalid_response`, keine Teilübernahme.

## Grenzen
- KI macht **Vorschläge**; Änderungen an Vorrat oder Einkaufsliste erst nach Bestätigung.
- Haltbarkeit, Allergene, Verzehr nach Ablauf: nur aus geprüften Regeln, KI-Texte mit Hinweis.
- An die KI gehen nur nötige Daten (z. B. Vorratsnamen und Mengen), keine Namen der Personen,
  keine Konten-, Kontakt- oder Standortdaten (Details: [datenschutz.md](datenschutz.md)).

## Budget und Messung
- Jeder Aufruf zählt Anfragen und Tokens pro Haushalt und Monat.
- Telemetrie je Aufruf: Dauer, Anbieter, Modell, Eingabe-/Ausgabe-Tokens, Fehlerart – nie
  Prompt- oder Antworttext.
