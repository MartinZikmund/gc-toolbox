# Caesar cipher (ROT13) tool — design

**Date**: 2026-06-01 · **Issue**: [#13](https://github.com/MartinZikmund/gc-toolbox/issues/13) · **Branch**: `feature/caesar`
**Parity reference**: [geocachingtoolbox.com Caesar cipher / ROT13](https://www.geocachingtoolbox.com/index.php?lang=en&page=caesarCipher)

## Summary

A **Caesar cipher** tool for the Ciphers category: encode and decode Caesar shift ciphers (including
ROT13), live as the user types, with a **"show all shifts"** mode that lists every rotation at once so
an unknown shift can be brute-forced by eye. It plugs into the feature-002 attribute-based discovery
with **zero edits to any shared/central file** — a `[Tool]`-decorated ViewModel in `GcToolkit.Core`, a
pure codec, localized strings, an icon, and a dedicated View are all that is added. The
`CaesarCipherViewModel` already existed as a discovered **stub** (routed to the shared `ToolHostView`);
this design promotes it to a real tool with its own codec and view.

The structure mirrors the validated **Morse (#53)** and **Roman numerals (#48)** tools: a pure,
fully-tested codec in `GcToolkit.Core`, a thin `ToolViewModelBase`-derived ViewModel with
`[ObservableProperty]`/`[RelayCommand]`, and a dedicated `ViewBase<TVm>` View in the app head that the
source generator auto-routes to (it falls back to `ToolHostView` only for view-less stubs).

### Going beyond parity

geocachingtoolbox offers three alphabets (letters/ROT13, digits/ROT5, ASCII/ROT47), a selectable
rotation, an encrypt/decrypt toggle, "show all rotations", and a "Key" display. This tool matches all
of that and adds:

1. **Case preservation & Unicode passthrough** — `Hello, World!` → `Khoor, Zruog!` keeps case,
   punctuation, spaces and any non-alphabet characters intact (parity sample upper-cases / strips).
2. **Signed one-call decoding** — decode is just the negated shift, so encode/decode is a single toggle
   over one codec path; no separate decode table.
3. **Per-candidate copy** in the all-shifts list — each rotation has its own copy button, plus a
   self-inverse hint (ROT13/ROT5/ROT47 use the same shift to encode and decode).
4. **Live substitution key** that updates with the alphabet and shift, shown aligned in monospace.

The **keyword/“custom alphabet key”** variation the parity site mixes in is intentionally **out of
scope** — that is a keyword-substitution cipher (closer to the separate Vigenère tool), not a Caesar
shift.

## Acceptance criteria (issue #13) → how this design satisfies them

| Criterion | Satisfied by |
|---|---|
| Tool page in the gallery shell, discoverable via search + category | `[Tool("CaesarCipher", ToolCategory.Ciphers, …)]` → generator emits catalog + nav-tree entries; `Keywords` feed search |
| Core transform, both directions | `CaesarCipher.Transform` (signed shift); VM `DirectionIndex` toggle; `AllShifts` for brute force |
| Input validation with clear error/empty states | Empty input → no output and an empty list; every other input is valid (a shift cipher never “fails”), so no error state is needed |
| Copy and share of results | `CopyOutputCommand`/`ShareOutputCommand` (single result or all shift lines) + per-row `CopyCommand` |
| Localized EN + CS (markup extension, short keys, no `x:Uid`) | `Caesar*` keys in `en`/`cs` `Resources.resw`, `{markup:Localize}` |
| Works offline | Pure in-process codec, no network/persistence |
| Accessible + responsive | `AutomationProperties` on slider/favorite/copy controls, font-scaling text styles, `ScrollViewer` + `MaxWidth` layout |
| Unit tests for core logic (MSTest) | `CaesarCipherTests` (26 cases: shifts, wrap, case, ROT13/5/47, negative decode, round-trips, all-shifts, key) |

## Architecture

```
GcToolkit.Core (no UI deps)                         GcToolkit (app head)
─────────────────────────────                       ─────────────────────────────
Ciphers/CaesarCipher.cs        ◀── unit tests        Views/CaesarCipherView.xaml(.cs)
ViewModels/Tools/                                      (ViewBase<CaesarCipherViewModel>,
  CaesarCipherViewModel.cs   ──[Tool]──┐               [NavigationInfo(Tool)])
  CaesarShiftItem.cs                   │              Strings/{en,cs}/Resources.resw  (+keys)
                                       │              Assets/Icons/Tools/CaesarCipher.png
                                       │              Assets/Icons/Categories/Ciphers.png
                                       ▼
                 GcToolkit.SourceGenerators (analyzer on the app head)
                 scans [Tool] in Core + ViewBase<> views → emits catalog,
                 nav-tree entry under Ciphers, and the VM→View registration.
```

### 1. Core codec — `GcToolkit.Core/Ciphers/CaesarCipher.cs`

Pure and stateless; the single source of truth for the transform. No UI, no platform, no I/O.

```csharp
public enum CaesarAlphabet { Letters, Digits, Ascii }            // 26 / 10 / 94 symbols
public readonly record struct CaesarShiftResult(int Shift, string Text);

public sealed class CaesarCipher
{
    public const int LetterAlphabetSize = 26, DigitAlphabetSize = 10, AsciiAlphabetSize = 94;
    public static int AlphabetSize(CaesarAlphabet a);            // 26/10/94
    public static int DefaultShift(CaesarAlphabet a);            // size/2 → ROT13/ROT5/ROT47

    // Shift in-alphabet chars by `shift` (normalized mod size; negative decodes); preserve case,
    // pass everything else through. Empty/null → "".
    public string Transform(string? text, int shift, CaesarAlphabet a = CaesarAlphabet.Letters);

    // Shifts 1..size-1, in order, for the "show all shifts" brute force.
    public IReadOnlyList<CaesarShiftResult> AllShifts(string? text, CaesarAlphabet a = Letters);

    public string PlainAlphabet(CaesarAlphabet a);               // ABC…Z / 0–9 / !..~
    public string CipherAlphabet(int shift, CaesarAlphabet a = Letters); // substitution row
}
```

- **Letters**: A–Z and a–z each rotate within 26; case preserved; non-letters pass through.
- **Digits**: 0–9 within 10; **ASCII**: 33–126 within 94 (ROT47 covers letters+digits+punctuation).
- **Normalize**: `((shift % size) + size) % size`, so any signed shift is valid and a full wrap is identity.
- Hot path uses `string.Create` (no intermediate allocations) — see thin, allocation-aware style.

### 2. ViewModel — `GcToolkit.Core/ViewModels/Tools/CaesarCipherViewModel.cs`

`[Tool("CaesarCipher", ToolCategory.Ciphers, Introduced/Updated = "2026-06-01", Keywords = […])]`,
deriving `ToolViewModelBase`. Constructor injects the base four services plus `IClipboardService` and
`IShareService`. Observable shape mirrors Roman/Morse:

- `ModeIndex` (0 Letters / 1 Digits / 2 ASCII) — on change, resets `Shift` to the alphabet’s ROT default
  and clamps `MaxShift = size − 1`.
- `DirectionIndex` (0 Encode / 1 Decode) — decode = negated shift.
- `Shift` (slider, 0…`MaxShift`), `MaxShift`.
- `InputText` → `Recompute()` on every change (`_suppressRecompute` guards the mode-reset carry).
- `ShowAllShifts` — toggles between a single `OutputText` and the `ShiftResults` list.
- `OutputText` / `HasOutput`; `IsSelfInverse` (single-result hint); `KeyPlain` / `KeyCipher` (live key).
- `ShiftResults : ObservableCollection<CaesarShiftItem>` — each item is `{ Shift, Text, CopyCommand }`
  (`CaesarShiftItem.cs`), so a row copies itself without reaching back into the VM.

**Commands**: `CopyOutput`/`ShareOutput` (`CanExecute = HasOutput`) emit the single result or, in
all-shifts mode, every `"<shift>: <text>"` line; `Clear`. Logic lives in the codec (thin-VM convention).

### 3. View — `GcToolkit/Views/CaesarCipherView.xaml(.cs)`

`CaesarCipherViewBase : ViewBase<CaesarCipherViewModel>` `[NavigationInfo(Tool)]` + sealed
`CaesarCipherView` — identical wiring to Roman/Morse. `ScrollViewer → StackPanel MaxWidth=760`, Fluent
theme resources, structural XAML comments:

- **Header** — `ToolName` + `Tooltip` + favorite `ToggleButton`.
- **Alphabet** — `RadioButtons` (Letters·ROT13 / Digits·ROT5 / ASCII·ROT47).
- **Direction** — `RadioButtons` (Encode / Decode).
- **Shift** — `Slider` 0…`MaxShift` + monospace value readout.
- **Input** — multiline `TextBox` (`UpdateSourceTrigger=PropertyChanged`) + Clear + “Show all shifts”
  `ToggleSwitch`.
- **Single result** (when not show-all) — read-only monospace `TextBox` + self-inverse `InfoBar`.
- **All shifts** (when show-all) — `ItemsControl` of rows (`shift · text · copy`), via
  `InvertedBoolToVisibilityConverter`/`BoolToVisibilityConverter`.
- **Actions** — Copy + Share.
- **Key** — card with aligned monospace plain/cipher rows (cipher row accent-coloured) + a one-line note.

### 4. Localization, icons, tests

**Resources** (`Strings/{en,cs}/Resources.resw`, short keys, both cultures required or the generator
errors): `CaesarCipher_Tooltip` (updated) + `CaesarMode`, `CaesarModeLetters/Digits/Ascii`,
`CaesarEncode/Decode`, `CaesarShiftLabel`, `CaesarInputLabel/Placeholder`, `CaesarClear`,
`CaesarShowAllShifts`, `CaesarOutputLabel`, `CaesarSelfInverseNote`, `CaesarAllShiftsLabel`,
`CaesarCopyShift`, `CaesarCopy`, `CaesarShare`, `CaesarKeyLabel`, `CaesarKeyNote`.

**Icons** (Icons8, colour/Fluency, convention paths): `Assets/Icons/Tools/CaesarCipher.png` (Roman
helmet — on-theme for Caesar) and `Assets/Icons/Categories/Ciphers.png` (padlock — first real Ciphers
tool, so the category icon lands with it). Missing icons fall back to nothing and never break the build.

**Tests** — `tests/GcToolkit.Core.Tests/Ciphers/CaesarCipherTests.cs` (MSTest): known shifts + wrap,
case preservation, non-letter passthrough, negative-shift decode and round-trips across all 26 shifts,
ROT13/ROT5/ROT47 self-inverse, empty/null, `AllShifts` counts (25/9/93) + “contains the known
plaintext”, and `PlainAlphabet`/`CipherAlphabet`/`AlphabetSize`/`DefaultShift` conventions.

## Out of scope (YAGNI)

- The keyword/“custom alphabet key” substitution variation (a different cipher family; see Vigenère).
- Atbash and other ciphers listed in the PRD — separate tools.
- Per-tool pages beyond this one; no changes to discovery/generator, catalog, recents, favorites, or
  search infrastructure.

## Risks / decisions

- **ASCII “show all shifts” is long** (93 rows) — acceptable inside the `ScrollViewer`; the feature is
  most useful for letters (25 rows), which is the default alphabet.
- **No error state** — a shift cipher is total over its alphabet, so every non-empty input produces a
  result; the only special state is empty input (cleared output), matching the parity sample’s behaviour.
