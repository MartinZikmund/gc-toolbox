# Specification Quality Checklist: Attribute-Based Tool Auto-Discovery & Navigation Tree

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-30
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- All clarifications were resolved interactively before drafting (see the **Clarifications** section in `spec.md`); no `[NEEDS CLARIFICATION]` markers remain.
- **Grounded in the existing app shell**: the spec was written against the real feature-001 code (`ICatalogService`/`CatalogService`, `IToolContributor`/`ICategoryContributor`, `ToolDescriptor`, `Category`, `WindowShell`/`NavigationView`, `INavigationService`, `IRecentsService`, `IFavoriteToolsService`, view-model-first navigation, `IStringLocalizer`). The feature is framed as **replacing the manual contributor registration with an attribute + source generator, enriching the descriptor, and rendering the discovered hierarchy in the navigation pane** — not as greenfield work.
- **Navigation tree (added 2026-05-30)**: a second capability was folded into this spec — a source generator that runs **against `GcToolkit.Core`** and emits a UI-agnostic group→category→tool tree that `WindowShell` renders into `NavigationView`. Modeled on the Uno `SamplesApp.UITests.Generator` (`SamplesListGenerator` emits typed data; the app renders it). Tool ViewModels (and, per the owner, all page ViewModels) move into `GcToolkit.Core` so the generator can see them.
- **Grouping rename**: the optional second enum level changed from a child **subcategory** to a parent **group / "uber-category"** that renders as a pane section heading. The spec, entities, FRs, and success criteria use `ToolCategory` (required, holds tools) and `ToolGroup` (optional heading) consistently.
- **Category dual-affordance (added 2026-05-30)**: each category node is both **expandable** (chevron reveals its tools inline) and **clickable** (opens a Catalog view scoped to that category, reusing the existing Catalog page). Captured in US3 (scenarios 5–6), FR-021/FR-022, edge cases, and SC-003.
- **Icons required for tools AND categories (added 2026-05-30)**: every tool (FR-008) and every category (FR-008a) must declare an Icons8 bitmap icon; a missing icon is a build-breaking error. This promotes the existing optional `IconKey` on both `ToolDescriptor` and `Category` to required. Reflected in the Clarifications, US5 (build-error scenario), edge cases, entities, SC-002, and SC-005. Group/"uber-category" icons remain optional (text headings).
- **Deliberate, scoped technical references**: requirements, user stories, and success criteria are written to be mechanism-agnostic and testable. Necessary technical context (compile-time C# source generator running against Core, ViewModel attribute target, `.resw` `<Id>_Name`/`<Id>_Tooltip` convention, Icons8 bitmap assets, enum grouping, `NavigationView` rendering, existing services) is confined to the **Context**, **Assumptions**, and **Dependencies** sections because (a) this is developer-facing infrastructure whose stakeholders include tool developers, (b) the source-generator mechanism and the Core/NavigationView targets are mandated by the request/issue (#71), and (c) it must integrate with concrete existing types. This is intentional, not a leak into the requirements.
- **Decisions to confirm at planning** (do not block the spec): (1) moving categories from the free-string `Category`/`ICategoryContributor` model to closed `ToolCategory`/`ToolGroup` enums; (2) reconciling existing dotted tool ids (e.g., `coordinates.conversion`) with the `Id`-as-resource-key-base convention (`.resw` keys can't use dots); (3) redefining `IconKey` from an optional glyph string to a required Icons8 bitmap reference; (4) **moving the existing page ViewModels into `GcToolkit.Core`** and resolving their current app-head dependencies (`Localizer.Instance`, `ToolListItem`); (5) the exact `NavigationView` control mapping for group headings vs. expandable category nodes vs. flat tool items, including the expandable-yet-invokable category node (chevron expand + click-to-navigate); (6) adding a **category-filter entry point to the existing `CatalogViewModel`** (today it always renders all categories and takes no navigation parameter) so a clicked category opens its scoped catalog.
