# SEJ Location Guide

A .NET MAUI app for **Windows and Android**. It guides Indian Railways P. Way field engineers to put a **Switch Expansion Joint (SEJ)** in LWR/CWR at the **right location**, using the **right SEJ drawing**. Any wrong selection raises a **red alert**.

Rules are based on:
- **IRPWM 2024 (First Reprint), corrected up to ACS-16**: Paras 225, 226, 326–331, Annexures 2/4(B) and 3/16, Table-1, Fig. 3.6
- **RDSO Report No. CT-48 (Revised) Nov 2025**: Continuation of LWR through Turnouts

Prepared by **Dharam Pal Jangid**, Senior Section Engineer (P. Way), Bundi, West Central Railway.
For queries and suggestions: Mob. 7976273600

## What it does

Tap a location, enter the site details, and the app shows:
- the **correct SEJ location** (distance in metres from the fixed asset)
- the **permitted SEJ drawing(s)** for the rail section, temperature zone and curvature
- a colour-coded result, with the governing para for every point:
  - ✅ Green: correct
  - ⚠️ Amber: permitted only with approval (CTE/PCE) or with precautions
  - ⛔ Red: wrong selection, with a popup alert
- a **Copy result** button that copies the result as text for inspection notes

### Locations covered

| Group | Checks |
|---|---|
| Points & Crossings | LWR terminated near turnout (39 m / 40 m); LWR through turnout, one side only; LWR through turnout, both sides; crossover; turn-in curve with derailing switch; loop line |
| Level crossing | LC outside breathing length (Annex 3/16, or a site value) |
| Curves | SEJ curve limits; transition curve; 52 kg sharp-curve rule; LWR curve limits; buffer rails |
| Bridges | Girder bridge cases A–F (isolated, rail-free, partly box-anchored, far-end SEJ 15 m, SEJ on each pier, single span 30 m); ballasted deck / BLT bridges |
| Track structure | Change of rail section (T-6782); 39 m beyond SEJ; BLT junction (30 m); gradient; insulated joints / DAC |
| Drawing finder | RT-8822, RT-4160/4165, RT-6902/6914/6922/6930, RT-8924/8926, T-6782, buffer rails |

## Download

Every push to `main` runs the unit tests and builds both apps. The results appear on the repository's **Releases** page:
- `SEJ-Location-Guide-Windows.zip`: extract it and run `SejGuide.exe`
- `SEJ-Location-Guide.apk`: install it on an Android phone (allow "Install unknown apps")

## Project layout

```
src/SejGuide.Core    rules engine + IRPWM/CT-48 data (no UI; unit-tested)
src/SejGuide.App     .NET MAUI app (Android + Windows)
tests/SejGuide.Tests xUnit tests for each rule
.github/workflows    automatic build: tests → Windows exe + Android apk → Release
```

To add or change a rule, edit `src/SejGuide.Core/Rules.cs` (checks) or `Catalog.cs` (drawings and tables). Each screen in the app is generated from these definitions, so no UI code needs changing.

## Disclaimer

This app is a guidance aid. Before execution, verify against the latest IRPWM correction slips, RDSO drawings with their latest alterations, and the approvals of the competent authority.

© Dharam Pal Jangid
