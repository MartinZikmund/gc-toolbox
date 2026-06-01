# Roman numerals tool — design

**Date**: 2026-06-01 · **Issue**: [#48](https://github.com/MartinZikmund/gc-toolbox/issues/48) · **Branch**: `feature/romannumerals`
**Parity reference**: [geocachingtoolbox.com Roman numbers](https://www.geocachingtoolbox.com/index.php?lang=en&page=romanNumbers)

## Summary

A **Roman numerals** tool for the Numbers category: convert between Roman numerals and integers in
both directions, live as the user types. It plugs into the feature-002 attribute-based discovery with
**zero edits to any shared/central file** — a `[Tool]`-decorated ViewModel in `GcToolkit.Core`, two+
localized strings, an icon, and a dedicated View are all that is added.

The structure deliberately mirrors the validated **Morse code tool (#53)**: a pure, fully-tested codec
in Core, a thin `ToolViewModelBase`-derived ViewModel with `[ObservableProperty]`/`[RelayCommand]`, and
a dedicated `ViewBase<TVm>` View in the app head that the source generator auto-routes to.

### Going beyond parity

geocachingtoolbox caps at 3999, converts both directions, accepts multiple space-separated values, and
shows the symbol reference table. This tool matches all of that and adds two deliberate extensions
(approved 2026-06-01):

1. **Extended range via vinculum** — values **1 – 3,999,999** using overline notation (×1000), e.g.
   `V̅` = 5000, `M̅` = 1,000,000.
2. **Lenient decoding with a warning** — accepts non-canonical forms common in geocaching puzzles
   (`IIII`, `VIIII`, `XXXXX`, clock-face `IIII`), computes the value, and flags it as a non-standard
   form. Encoding always emits canonical subtractive numerals.

Plus pattern-consistent niceties: live conversion, a direction toggle that carries the previous result
into the input for one-tap round-trips, copy/share/clear, and a step-by-step breakdown of the result.

## Acceptance criteria (issue #48) → how this design satisfies them

| Criterion | Satisfied by |
|---|---|
| Tool page in the gallery shell, discoverable via search + category | `[Tool("RomanNumerals", ToolCategory.Numbers, …)]` → generator emits catalog + nav-tree entries; `Keywords` feed search |
| Core transform, both directions | `RomanNumeralCodec.Encode` / `TryDecode`; VM `DirectionIndex` toggle |
| Input validation with clear error/empty states | Empty → no output; invalid chars / out-of-range → error `InfoBar`; non-canonical → warning `InfoBar` |
| Copy and share of results | `CopyOutputCommand` (`IClipboardService`), `ShareOutputCommand` (`IShareService`) |
| Localized EN + CS (markup extension, short keys, no `x:Uid`) | `RomanNumerals_*` + UI keys in `en`/`cs` `Resources.resw`, `{markup:Localize}` |
| Works offline | Pure in-process codec, no network/persistence |
| Accessible + responsive | `AutomationProperties` on controls, font-scaling-friendly text styles, `ScrollViewer` + `MaxWidth` layout (Morse pattern) |
| Unit tests for core logic (MSTest) | `RomanNumeralCodecTests` |

## Architecture

```
GcToolkit.Core (no UI deps)                         GcToolkit (app head)
─────────────────────────────                       ─────────────────────────────
Numbers/RomanNumeralCodec.cs   ◀── unit tests        Views/RomanNumeralsView.xaml(.cs)
ViewModels/Tools/                                      (ViewBase<RomanNumeralsViewModel>,
  RomanNumeralsViewModel.cs  ──[Tool]──┐               [NavigationInfo(Tool)])
                                       │              Strings/{en,cs}/Resources.resw  (+keys)
                                       │              Assets/Icons/Tools/RomanNumerals.png
                                       ▼
                 GcToolkit.SourceGenerators (analyzer on the app head)
                 scans [Tool] in Core + ViewBase<> views → emits catalog,
                 nav-tree entry under Numbers, and the VM→View registration.
```

### 1. Core codec — `GcToolkit.Core/Numbers/RomanNumeralCodec.cs`

Pure and stateless; the single source of truth for the transform. No UI, no platform, no I/O — exactly
like `MorseCodec`.

**Constants / range**: `MinValue = 1`, `MaxValue = 3_999_999`.

**Vinculum textual representation**: the canonical string form places the **combining overline
`U+0305`** immediately after each thousands-block symbol (e.g. `"V̅"` for 5000). This is the form
that is copied, shared, parsed, **and displayed** — the text engine (DirectWrite on Windows,
Skia/HarfBuzz on the Uno heads) shapes `U+0305` into the overline over the preceding glyph. (WinUI's
`TextDecorations` enum has no `Overline` member, only `Underline`/`Strikethrough`, so a combining mark
is the portable way to draw the bar and it keeps copy/paste fidelity.)

**Public surface**

```csharp
public sealed class RomanNumeralCodec
{
    public const long MinValue = 1;
    public const long MaxValue = 3_999_999;
    public const char Vinculum = '̅'; // combining overline

    /// Canonical subtractive numeral for value in [MinValue, MaxValue]; throws
    /// ArgumentOutOfRangeException otherwise (the VM guards range before calling).
    public string Encode(long value);

    /// Lenient parse. Returns false for empty/whitespace, unknown characters, or a
    /// malformed overline. On success, value is the computed integer and isCanonical
    /// is Encode(value) == NormalizedInput (false ⇒ non-standard form warning).
    public bool TryDecode(string? text, out long value, out bool isCanonical);

    /// Additive breakdown of a value for the explanation panel, in descending order,
    /// e.g. 1994 → [(M,1000),(CM,900),(XC,90),(IV,4)]. Symbols carry the overline where
    /// they belong to the thousands block.
    public IReadOnlyList<RomanNumeralPart> Explain(long value);
}

public readonly record struct RomanNumeralPart(string Symbol, long Value);
```

**Encode algorithm (Convention A — overline the whole thousands count)**
- If `value < 4000`: normal greedy encoding only, using the table that includes `M`=1000. This covers
  1–3999, so 1000–3999 render as `M`/`MM`/`MMM` (no vinculum below 4000).
- If `value >= 4000`: `thousands = value / 1000` (range 4–3999), `remainder = value % 1000`. Greedily
  encode `thousands` with the normal symbol table, overline every Latin letter (append `U+0305` after
  each), then append the normal greedy encoding of `remainder`. So vinculum begins at 4000 (`I̅V̅`),
  `5000 = V̅`, `3,999,999 = M̅M̅M̅C̅M̅X̅C̅I̅X̅CMXCIX`.

**Decode algorithm**
- Normalize: trim, upper-case, fold any precomposed/standalone overline into "letter + `U+0305`".
- Tokenize into symbols where a base letter optionally followed by `U+0305` is one symbol; map each to
  its value (`I̅`=1000 … `M̅`=1,000,000, plus `I`=1 … `M`=1000).
- Single left-to-right subtractive scan: `if current < next: subtract else add`. This naturally yields
  the value for canonical subtractives (`IV`), additive runs (`IIII`), and cross-vinculum subtractives
  (`MV̅` = 4000), so lenient decoding needs no special cases.
- `isCanonical = (Encode(value) == normalizedInput)`.
- Failure (return false): empty/whitespace, any character outside the symbol set + overline +
  whitespace, or an overline not attached to a base letter.

### 2. ViewModel — `GcToolkit.Core/ViewModels/Tools/RomanNumeralsViewModel.cs`

```csharp
[Tool("RomanNumerals", ToolCategory.Numbers,
      Introduced = "2026-06-01", Updated = "2026-06-01",
      Keywords = ["roman", "numeral", "číslice", "římské", "latin", "I", "V", "X", "vinculum"])]
public sealed partial class RomanNumeralsViewModel : ToolViewModelBase
```

Constructor injects `ICatalogService, IRecentsService, IFavoriteToolsService, IStringLocalizer`
(base) plus `IClipboardService, IShareService`. Mirrors `MorseCodeViewModel`'s observable shape:

- `DirectionIndex` — `0` = Number → Roman, `1` = Roman → Number. On change (ignoring the transient `-1`
  a `RadioButtons` can emit), carries `OutputText` into `InputText` so a round-trip is one tap.
- `InputText` → `Convert()` on every change (`_suppressConvert` guards the carry, as in Morse).
- `OutputText`, `HasOutput`.
- `HasWarning` + `WarningMessage` (localized) — set for non-canonical input, out-of-range, or a batch
  with some invalid tokens.
- `BreakdownLines` (`ObservableCollection<string>`, each `"Symbol = Value"` formatted from the codec's
  `Explain`) + `ShowBreakdown` — populated only when the input is a single token; hidden for batch/empty.

**Batch (parity)**: `Convert()` splits `InputText` on whitespace; each token is converted independently
and the results are re-joined by spaces. A token that fails conversion becomes `?` and raises an
aggregate warning. Direction-specific per token:
- Number→Roman: parse integer; out of `[1, 3,999,999]` or non-integer → `?` + warning.
- Roman→Number: `TryDecode`; failure → `?` + warning; `isCanonical == false` → warning.

**Commands**: `[RelayCommand(CanExecute = nameof(HasOutput))]` `CopyOutput`, `ShareOutput` (best-effort,
swallows platform share failures like Morse); `[RelayCommand]` `Clear`.

Logic lives in the codec; the VM only orchestrates (thin-VM convention).

### 3. View — `GcToolkit/Views/RomanNumeralsView.xaml(.cs)`

`RomanNumeralsViewBase : ViewBase<RomanNumeralsViewModel>` with `[NavigationInfo(NavigationSection.Tool)]`
+ `sealed partial RomanNumeralsView : RomanNumeralsViewBase` — identical wiring to `MorseCodeView`. The
generator routes the VM to this dedicated view (it falls back to the shared `ToolHostView` only for
stubs without a view).

Layout (`ScrollViewer` → `StackPanel MaxWidth=760`, Fluent theme resources, structural XAML comments):
- **Header** — `ToolName` + `Tooltip` + favorite `ToggleButton` (copied from Morse).
- **Direction** — `RadioButtons` (`Number → Roman`, `Roman → Number`).
- **Input** — `TextBox` (`UpdateSourceTrigger=PropertyChanged`), localized header + placeholder; `Clear`.
- **Result** — read-only `TextBox` (monospace, like Morse's output) bound to `OutputText`. The vinculum
  bar is drawn by the text engine shaping the codec's combining-overline (`U+0305`) string; the same
  string is what copy/share emits.
- **Warning** — `InfoBar` bound to `HasWarning`/`WarningMessage` (`Severity="Warning"`).
- **Actions** — Copy + Share buttons (Morse pattern).
- **Breakdown** — collapsible list of `BreakdownParts` (`Symbol` = `Value`), visible when `ShowBreakdown`.
- **Reference table** — static I/V/X/L/C/D/M → 1/5/10/50/100/500/1000 grid (parity), with a one-line
  note that an overline multiplies by 1000.

### 4. Localization, icon, tests

**Resources** (`Strings/en/Resources.resw` + `Strings/cs/Resources.resw`, short keys, both cultures
required or the generator errors `GCTOOL004`):

| Key | EN | CS |
|---|---|---|
| `RomanNumerals_Name` | Roman numerals | Římské číslice |
| `RomanNumerals_Tooltip` | Convert between Roman numerals and numbers, both ways. | Převod mezi římskými číslicemi a čísly v obou směrech. |
| `RomanNumberToRoman` | Number → Roman | Číslo → Římské |
| `RomanRomanToNumber` | Roman → Number | Římské → Číslo |
| `RomanInputLabel` / `RomanInputPlaceholder` | Input / Type a number or Roman numeral… | Vstup / Zadejte číslo nebo římskou číslici… |
| `RomanOutputLabel` | Result | Výsledek |
| `RomanClear` / `RomanCopy` / `RomanShare` | Clear / Copy / Share | Vymazat / Kopírovat / Sdílet |
| `RomanNonStandardNotice` | Non-standard form — interpreted by value. | Nestandardní zápis — interpretováno podle hodnoty. |
| `RomanInvalidNotice` | Some input couldn't be converted and is shown as ?. | Některý vstup nelze převést a je zobrazen jako ?. |
| `RomanBreakdownLabel` / `RomanReferenceLabel` | Breakdown / Symbols | Rozklad / Symboly |
| `RomanVinculumNote` | An overline multiplies a symbol by 1000. | Vodorovná čára nad symbolem jej násobí 1000. |

(Exact wording finalizable during implementation.)

**Icon**: `Assets/Icons/Tools/RomanNumerals.png` from Icons8 (via the Icons8 MCP). A missing file falls
back to a runtime placeholder (R7) and never breaks the build.

**Tests** — `tests/GcToolkit.Core.Tests/Numbers/RomanNumeralCodecTests.cs` (MSTest, `[DataTestMethod]`):
- Encode known values (1, 4, 9, 40, 90, 400, 900, 1994, 2023, 3999).
- Decode canonical numerals; round-trip across the range.
- **Vinculum boundaries**: 3999↔`MMMCMXCIX`, 4000↔`I̅V̅`, 5000↔`V̅`, 1,000,000↔`M̅`, max 3,999,999.
- **Lenient**: `IIII`→4, `VIIII`→9, `XXXXX`→50 decode with `isCanonical == false`.
- Invalid input (`ABC`, empty/whitespace, stray overline) → `TryDecode` false.
- Range guard: `Encode(0)` / `Encode(4_000_000)` throw.
- `Explain` decomposition for representative values.

## Out of scope (YAGNI)

- Per-tool pages beyond this one; no changes to discovery/generator, catalog, recents, favorites, or
  search infrastructure.
- ASCII bracket vinculum convention (`(V)`) — rejected: non-standard, not what puzzle sources use.
- Roman fractions (uncia/`S`) and the apostrophus notation — niche, not requested.

## Risks / decisions

- **Overline rendering across 6 heads** — WinUI has no `Overline` text decoration, so the vinculum is the
  combining mark `U+0305`, shaped by the platform text engine. This is the portable approach and keeps
  copy/paste fidelity; it only affects the ≥4000 "go further" range (1–3999 output is plain ASCII), and
  the breakdown panel + reference-table note make the ×1000 meaning explicit regardless.
- **Lenient decode ambiguity** — a shared-value canonicality check (`Encode(value) == input`) is the
  single rule that decides "standard vs. flagged," keeping behavior predictable and testable.
