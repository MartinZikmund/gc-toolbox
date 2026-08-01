# Product Requirements Document — GC Toolkit (working title)

> **Status:** Draft v1 · **Date:** 2026-05-27 · **Owner:** Martin Zikmund
> **Type:** High-level PRD. Decomposed into per-feature specs via Spec Kit (`/speckit.specify`).
> Deferred ideas and post-MVP scope live in [`ideas.md`](../ideas.md).

---

## 1. Summary

A free, open-source, cross-platform **geocaching toolkit** — a native, offline-capable,
sensor-aware toolkit that goes beyond what existing web-based geocaching tool collections offer. The
MVP is a **gallery of standalone tools** that help geocachers solve puzzle/mystery caches at home and navigate
in the field, built with the [Uno Platform](https://platform.uno/) for Windows, Android, iOS, macOS,
Linux, and WebAssembly.

The toolkit is **provider-agnostic** (no dependency on any listing service's API in the MVP) and is
explicitly designed as the **beachhead for a later AI solving assistant** that will help geocachers
identify how to crack a cache and guide them toward the solution.

## 2. Vision & differentiation

Existing web-based geocaching tool collections are large sets of one-off calculators. This product
aims to be **more capable** by being a true native app:

- **Native + offline:** the solving tools work without a connection, with native performance and OS
  integration (share, clipboard, file system).
- **Sensor-aware:** access to GPS, magnetometer/compass, and other device sensors enables field tools
  (navigation, GPS averaging) that a website cannot offer.
- **Cohesive, discoverable UX:** a single gallery shell with search, favorites, and a shared design
  language — not a flat list of disconnected pages.
- **An AI-ready foundation:** tools expose a uniform, machine-invokable contract so a future AI
  assistant can reason over them and call them programmatically.

## 3. Target users

| Persona | Context | Needs |
| --- | --- | --- |
| **Mystery-cache solver** (primary) | At home/desk; any platform incl. web & desktop | Fast, reliable converters, ciphers, and coordinate math; copy/share results; discoverability |
| **On-the-go cacher** (secondary, first-class for field tools) | In the field, on a phone | Compass, navigate-to-coordinate, GPS averaging, saved coordinates; works offline |

Both span **newcomers** (need clarity and discoverability) and **power users** (need speed, favorites,
and accuracy).

## 4. Goals & success metrics

- **Primary success signal:** adoption & retention — real geocachers install the app and keep using
  the tools.
- **Measurement (MVP):** app-store dashboards + ratings/reviews. No in-app telemetry in the MVP
  (privacy-first; opt-in analytics is a deferred option — see `ideas.md`).
- **Implied product bar:** the native toolkit must feel genuinely handier than existing web-based
  tools, enough to keep on the home screen.

## 5. Scope — MVP (in)

### 5.1 Gallery shell
- Shared navigation across a **searchable, categorized tool catalog**.
- **Favorites** and **recents**.
- A shared **design language** and reusable components (coordinate input, copy/share buttons, result
  cards). Each tool is a page; tools may have bespoke pages but reuse common UX where practical.

### 5.2 Solving tools (initial curated subset)
Each tool is built as its own GitHub issue. This is the proposed MVP set; the catalog is extensible.

| Category | Tools |
| --- | --- |
| **Coordinate** | Format converter (DD ↔ DDM ↔ DMS, plus UTM/MGRS); waypoint projection (point + distance + bearing); distance & bearing between two points; midpoint / center of points |
| **Codes & ciphers** | Caesar/ROT (ROT13 + all-shifts), Atbash, Vigenère, Morse (text ↔ morse), A1Z26 (letter ↔ number) |
| **Numbers, bases & text** | Base converter (bin/oct/dec/hex); ASCII / character codes; Base64; Roman numerals; digit sum / cross-sum / checksum; letter & word value (A=1…Z=26) |
| **Reference tables (starter set)** | NATO phonetic alphabet; resistor color codes; Braille chart; phone keypad (multitap) |

### 5.3 Field tools (sensor-driven)
- **Compass** (magnetometer).
- **Navigate to coordinate** — Mapsui **online** map + bearing arrow + live distance to target.
- **GPS averaging** — capture a more accurate reading by averaging samples.
- **Saved coordinates / waypoints** — manually entered, stored locally.

### 5.4 Data & input
- **Local-first**, no accounts, no backend.
- **Manual entry + paste** of coordinates (no file import in the MVP).
- On-device persistence of favorites, recents, saved coordinates, and settings.

### 5.5 Platform, UX & localization
- All six Uno targets: Windows, Android, iOS, macOS, Linux, WebAssembly.
- **Fluent design**; light / dark / system theme.
- **Responsive** layouts across phone, tablet, desktop, and web.
- **Accessibility:** font scaling, screen-reader labels, sufficient contrast.
- **Localization:** English + Czech at launch; fully localizable via the markup localization extension
  with short, descriptive string keys (no `x:Uid`).

### 5.6 Distribution
- Google Play, Apple App Store, Microsoft Store, and a hosted WebAssembly build.
- macOS/Linux builds produced; distribution channel TBD.

## 6. Scope — explicitly NOT in the MVP

All deferred items are tracked in [`ideas.md`](../ideas.md):

- AI solving assistant (hint-first, with an opt-in full-solve mode) — the headline future vision.
- Per-cache **solver workspace** (variables, tool-result piping, candidate coordinates, solved state).
- **Accounts & cloud sync** across devices.
- **Listing-service (geocaching.com) API integration** — live cache data, descriptions, logs, finds.
- **GPX / file import** and rich export.
- **Offline map tiles** / downloadable regions.
- **In-app analytics / telemetry.**

## 7. Architecture posture

- **Uno Platform** single codebase targeting all six platforms.
- **MVVM** with CommunityToolkit.Mvvm; **thin ViewModels**, business logic in **DI-injected services**.
- **Testing:** MSTest with a TDD approach; logic extracted into services so it is unit-testable
  independently of the UI.
- **Tool contract:** simple tools implement a common, typed `input → transform → output` contract
  rendered by shared UI, making new tools cheap to add and **AI-invokable** in the future. Sensor/map
  tools (compass, navigation) use bespoke pages.
- **Local persistence** for app data (favorites, recents, saved coordinates, settings).
- **Sensor abstraction:** GPS, magnetometer, etc. accessed via services that **feature-detect** and
  **degrade gracefully** where a platform (especially WebAssembly) lacks the capability.
- **Maps:** Mapsui for the interactive map (online tiles in the MVP).

## 8. Constraints

- **Naming/trademark:** working name is **GC Toolkit** — a placeholder; revisit for a final
  trademark-safe brand decision before store submission. The "Geocaching" / "Geocaching.com" marks
  (Groundspeak) MUST NOT appear in the app title; "for geocaching" is acceptable only as a plain
  descriptor.
- **License & monetization:** GPLv3, free on all stores. A supporter/donation option (GitHub Sponsors
  and/or a non-functional supporter purchase) is acceptable; no feature gating in the MVP. Cost
  recovery for the future AI is deferred (subscription / credits / bring-your-own-key — see `ideas.md`).
- **Provider-agnostic:** no hard dependency on any single listing service in the MVP.

## 9. Risks

| Risk | Mitigation |
| --- | --- |
| **Mapsui** coverage/quality across all six targets, especially **WebAssembly** | Spike/verify Mapsui on each target early; have a compass-arrow-only fallback for navigation where maps are weak |
| **Sensor availability** varies by platform (Web especially) | Feature-detect; degrade gracefully; clearly disable/hide unavailable tools per platform |
| **All-six-platform QA breadth** on a solo/open-source project | Prioritize platforms for deep QA (mobile + Windows + Web first); rely on the shared codebase and CI |
| **App-store trademark review** | Distinct brand from day one; avoid trademarked terms in title/keywords |
| **Scope creep** from the broad tool catalog | One tool = one GitHub issue; ship a curated subset first; everything else to `ideas.md` |

## 10. Roadmap (high level)

1. **MVP** — gallery shell + curated solving tools + field tools (this PRD).
2. **Solver workspace** — per-cache projects, variables, tool-result piping.
3. **Accounts & cloud sync** + supporting backend.
4. **Listing-service integration** (geocaching.com Live API and/or Opencaching).
5. **AI solving assistant** — hint-first, opt-in full-solve, calling tools via the shared contract.

(Ordering of 2–5 is indicative; details and alternatives in `ideas.md`.)

## 11. Open questions / next steps

- Confirm the final **product name/brand** before store submission (working name **GC Toolkit** pending a trademark check).
- Fill the Spec Kit **constitution** (`/speckit.constitution`) to encode principles: TDD, localization
  conventions, Uno/MVVM structure, accessibility.
- Confirm the **local persistence** technology during the first relevant spec.
- Decompose this PRD into per-feature specs with `/speckit.specify`, generating one GitHub issue per
  tool / feature.
