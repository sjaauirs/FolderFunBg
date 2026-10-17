# Chapter 10 — Exercises and capstones

Use these exercises to turn reading into skill. Each one names what you will
learn, what to change, and how to prove it works. Do them on a VM or a machine
you can restore.

Difficulty labels:

- **B** — a few hours; one or two layers.
- **I** — a weekend; touches several layers.
- **A** — a week or more; design decisions and performance work.
- **Capstone** — portfolio-sized; includes a design note and tests.

Before starting any exercise, create a branch:

```powershell
git switch -c exercise/your-name
```

---

## Beginner (B)

### B1 — Change the defaults

Change the default position to `center`, alpha to 180, and readability to
`auto`. Test in a light and a dark Windows theme. Write down when the scrim
appears.

**Learn:** config precedence, alpha, luminance-based scrim.

### B2 — Add an image position end-to-end

Add `top-center`. Update:

- the C++ `Position` enum and parser,
- the C# `BackgroundPositions` list,
- the settings UI (the ComboBox is data-bound),
- tests and documentation.

**Learn:** a complete vertical slice through two languages and XAML.

### B3 — Fix the Defaults layout

The Defaults page has misaligned controls at wide window sizes. Fix the layout
with a form grid or explicit alignment. Verify at 1000 px and 1600 px widths.

**Learn:** WinUI layout, `HorizontalAlignment`, `Grid` sizing.

### B4 — Improve the error message for a bad image

When an import fails, show the file name and a short reason instead of a generic
message. Do not include the full exception text in the UI; log it instead.

**Learn:** boundary validation, user-facing error design.

### B5 — Add a config sample test

Add a unit test that loads `samples/config.example.json` and asserts it passes
`ConfigValidator`.

**Learn:** schema testing, sample-as-documentation.

### B6 — Add a build badge and screenshots to the README

Add the CI badge and two screenshots from `docs/images`. Keep the images small
and descriptive.

**Learn:** developer experience and release hygiene.

### B7 — Add a "Open log folder" button check

The button exists. Verify it works when the log folder does not exist yet.
Make it create the folder or show a helpful message.

**Learn:** defensive UI, per-user paths.

### B8 — Improve the hook log

Add one log line per navigation with the resolved rule (defaults vs. a specific
path/CLSID) and the image file name. Bound it to the first 50 navigations.

**Learn:** debug-friendly logging without noise.

### B9 — Test an empty folder

Add a manual-test checklist item and an automated visual check (screenshot) for
an empty folder. Confirm `ROLE_SYSTEM_LIST` triggers.

**Learn:** test design for version-sensitive UI.

### B10 — Add a tooltip to the position field

Explain what `stretch` and `zoom-fill` do in one sentence each.

**Learn:** UX writing and accessibility.

---

## Intermediate (I)

### I1 — Add an `enabled` switch

Implement the config `enabled` flag from Chapter 6 end-to-end: native parser,
C# model, settings toggle, tests, hot reload.

**Acceptance:** toggling the switch removes/restores the background in an open
Explorer window without restarting Explorer.

### I2 — Build a Diagnostics page

Add a fifth page with config path, DLL path, Explorer module status, and the last
20 log lines. Include **Copy diagnostics**.

**Acceptance:** the smoke test covers the page; no secrets are displayed.

### I3 — Add drag-and-drop image import

Allow dropping PNG/JPEG files onto the Image library page. Validate each file
with the existing `ImageLibrary` import path; show per-file results.

**Acceptance:** a corrupt file does not crash or silently disappear.

### I4 — Pre-scale large images

At import time, generate a cached copy capped at, say, 3840 px on the long edge.
Keep the original for future DPI changes. Measure the paint time before and
after.

**Acceptance:** a 6000×4000 PNG no longer dominates paint time.

### I5 — Add per-folder random seeds

`random: true` currently picks any image. Add an optional `randomSeed` so a
folder shows a stable image per session. Document the semantics.

**Acceptance:** the same folder shows the same random image until the config or
seed changes; unit tests cover the selection.

### I6 — Accessibility pass

Run Accessibility Insights. Fix names, focus order, and contrast. Add
`AutomationProperties.Name` where needed. Write a short report.

**Acceptance:** no critical accessibility errors; keyboard-only navigation works.

### I7 — Test the installer repair path

Add `--repair` to the setup EXE: re-copy files, re-register, recreate the
shortcut, keep the user config. Test with a deleted shortcut, a renamed hook
DLL, and an outdated app.

**Acceptance:** repair works from a non-working install without a reboot.

### I8 — Add a CI artifact

Make the GitHub Actions workflow run `build.ps1` and upload the native DLL and
the smoke-test result file. Do not publish the installer from CI until you have
a signing strategy.

**Acceptance:** a green CI run produces downloadable artifacts.

---

## Advanced (A)

### A1 — Research an `IShellView` wrapper

Write a design note comparing the current hook with an `IShellView` wrapper:

- what a shell view wrapper can and cannot draw,
- COM registration and lifetime,
- z-order guarantees,
- compatibility across Windows 10/11,
- expected effort.

Build a minimal prototype if you can. Do not ship it until it is safer than the
current approach.

### A2 — Composition-level renderer

Prototype drawing behind the icons using a composition visual or a layered
window. Investigate the interaction with DWM, click-through, and multi-monitor
scaling. Compare latency and correctness with the DirectUI hook.

### A3 — Per-monitor DPI image cache

Scale images per monitor DPI and cache the results. Handle `WM_DPICHANGED` and
mixed-DPI setups. Measure memory and paint time.

**Acceptance:** a 200% monitor and a 100% monitor show the same physical image
size and no visible blur beyond the source resolution.

### A4 — Structured logging with ETW

Replace or supplement the text log with ETW events. Provide a small manifest or
use `TraceLogging`. Add a script to capture a trace with `wpr`/`wpa`.

**Acceptance:** paint and hook-install events appear in a trace with low
overhead.

### A5 — Config schema versioning and migration

Design a versioned schema and a migration path for v1 → v2. Make both parsers
reject unsupported future versions safely and migrate old ones.

**Acceptance:** old configs still load; unknown future versions fail closed with
a clear message.

### A6 — Performance budget

Define a budget: hook overhead per paint, installer time, app startup time, and
memory. Add measurements and a report. Optimize the worst offender.

**Acceptance:** numbers before/after, with a reproducible measurement script.

---

## Capstones

### Capstone 1 — A production-quality settings app

Deliver:

- a Diagnostics page,
- an `enabled` switch,
- drag-and-drop import,
- accessibility fixes,
- a polished layout,
- tests and screenshots.

Write a short design note explaining the UI architecture and one trade-off you
made.

### Capstone 2 — A safer rendering path

Deliver a design note and prototype comparing DirectUI hooking, an `IShellView`
wrapper, and a composition visual. Include:

- a compatibility matrix,
- failure modes,
- a migration plan,
- evidence from at least two Windows builds.

### Capstone 3 — A portable release pipeline

Deliver:

- a CI workflow that builds, tests, and produces a signed installer,
- versioning and changelog automation,
- SHA-256 publication,
- a clean-machine install test script,
- an uninstall/repair test.

Document how to rotate the signing key and how to verify a release.

---

## Evaluation rubric

For a self-assessment or a course project:

| Criterion | Weight | What good looks like |
|---|---|---|
| Correctness | 30% | The feature works in the stated scenarios, including edge cases |
| Tests | 20% | Unit tests for core logic; smoke/manual checks for UI |
| Safety | 15% | No crashes, bounded logging, fail-closed config, clean uninstall |
| Code quality | 15% | Small functions, clear names, consistent style, no dead code |
| Documentation | 10% | README/guide updated, screenshots, design note |
| Reflection | 10% | Explains trade-offs and what you would do differently |

---

## Portfolio tips

- Write a short design note for every capstone: problem, constraints,
  alternatives, decision, evidence.
- Include before/after screenshots and a small measurement table.
- Link to the exact files and commits you changed.
- Explain the failure you hit and how you diagnosed it. Interviewers care more
  about the reasoning than about a perfect first attempt.
- Never claim a Windows internal is stable. Explain the fallback.

---

## Where to go next

- [Code tour](code-tour.md)
- [Glossary](glossary.md)
- [Further reading](further-reading.md)
- [Install guide](../install-guide.md)
