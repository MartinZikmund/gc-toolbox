# The mark

A geocache at the centre of topographic contour rings — the thing you close in on. Rings are
deliberately irregular (offset centres, uneven radii) so they read as *contours*, not a target.
The amber dot is the find; it never appears anywhere else as decoration (see the amber discipline
rule in the design system).

## Which cut, at which size

| File                  | Rings | Use at          | Why                                                                 |
| ---------------------- | :---: | --------------- | -------------------------------------------------------------------- |
| `mark_foreground.svg` |   3   | ≥64px, app icon | Full composition. Foreground layer of the adaptive icon — pair with `mark_background.svg`. Transparent background; art stays inside the centre ~66% safe zone. |
| `mark_background.svg` |   —   | app icon plate  | Solid `#1B5E3F` background layer for the adaptive icon.              |
| `mark_40.svg`         |   2   | ~32–48px        | The full mark's outermost ring goes mushy below ~32px; dropping it and thickening the remaining two keeps the contour read. |
| `mark_24.svg`         |   1   | ~20–28px        | Two rings is one too many at this size — down to the innermost contour, stroke thickened again. |
| `mark_16.svg`         |   0   | ≤16px           | Any ring at all is noise here. Just the pine field and the amber find — this is also the "you have arrived" state on the compass. |

Each cut is a **bespoke composition**, not a scaled-down crop: ring count, radii and stroke weight
are re-tuned per size so the mark stays legible rather than shrinking into mush.

## `contour_band.svg`

A 1200×120 strip of 5 nested contour lines for page headers and empty states. Every curve is a sum
of sine harmonics with an **integer number of cycles across the 1200-wide viewBox**, so the value
(and slope) at `x=0` matches `x=1200` exactly — tile it or stretch it horizontally and there's no
seam. Stroke-only, `currentColor`, varying opacity (0.35 → 0.85 → 0.35) so the middle line reads as
a ridge. Set `color` on a wrapping element (or the `<img>`/`<svg>` context) to recolor.

## Regenerating ring geometry

The ring paths are Catmull-Rom splines through points sampled from a base radius perturbed by a few
sine harmonics, with each ring's centre drifting slightly toward a shared "peak" direction so
smaller rings sit closer to the find — like real contours around a summit that isn't dead centre.
There's no build step; the generator script that produced these coordinates was a scratch file, not
checked into the repo. Regenerate by hand-tuning coordinates directly if the mark ever needs to
change.
