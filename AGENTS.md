# Claxe Desktop — Agent engineering contract

This file governs autonomous coding work in this repository.

## Hard rules
- User's selections in `docs/01_VIZYON_KARARLAR.md` supersede generic UI conventions.
- Do NOT touch the separate Claxe Browser application or its files.
- Do NOT kill/replace explorer.exe, patch system DLLs, modify system shell registry values, install drivers, inject DLLs, bypass UIPI, or use undocumented WorkerW hacks as a baseline.
- Never claim an unsupported Windows integration exists. Prefer capability detection and documented fallback.
- Never present fake controls, simulated CPU or Wi-Fi values, or "implemented" settings that do nothing.
- Every Windows interaction must degrade gracefully and restore state after a crash when possible.
- Every feature defaults to off if it has compatibility, permissions, reliability, or performance risk.
- No opt-out telemetry and no hidden network requests. Network-capable widgets require origin allowlist, explicit permissions, and safe defaults.
- Do not refuse the entire app because optional anime art may be copyrighted. Support user-provided local assets; distinguish private display from public redistribution; choose free/licensed substitutes if needed. Do not misrepresent licensing.
- If SDK, tool, dependencies or licensing limitations are real, explain and propose viable alternatives. No invented workarounds.
- Keep work small and testable; don't advance beyond phase 1 before user approval.

## Code quality
- C# + .NET 10 WPF MVVM; abstractions around Win32, window tracking, theme/presets, wallpaper, widget host.
- Dependency injection, async cancellation, structured logs, versioned settings, schema validation, debounced disk writes, atomic persistence.
- Maintain unit tests for settings, profile merge, monitor remap, state recovery, layout engine and pause decision logic.
- Keep clear boundaries between core models, Windows platform adapters, UI, rendering, and untrusted web widgets.
- Per-monitor DPI aware, keyboard accessible, high contrast/reduced motion alternatives, error states visible.
- No extra dependencies without a concrete reason, security review and license check.
- Keep a `CHANGELOG.md` and `DECISIONS.md` for deviations.

## UX gate
- Check visual output against `assets/references` (references, not final assets).
- Default theme: Midnight Claxe (deep charcoal, plum/purple accents, subtle glass).
- Performance balance: animations beautiful, switchable, intelligently paused on fullscreen apps/games.
- Advanced settings discoverable through search; simple categories by default.

## Phase completion gate
1. Compiles where Windows build tooling is available.
2. Relevant tests run and results are stated honestly.
3. Windows shell stays usable when Claxe Desktop crashes/exits.
4. No empty buttons or nonfunctional user-facing toggles.
5. Explain precisely what is unfinished, and stop for user approval before next phase.

## Test çalışma tercihi (22 Ekim 2026 kullanıcı talebi)
- Günlük geliştirmede değişen bölüme odaklanan yaklaşık 10–12 kritik kontrol kullan; bütün regresyon takımını gereksiz yere tekrar çalıştırma.
- Widget kapsamı için `dotnet run --project tests/ClaxeDesktop.Core.Tests -c Release -- --widgets` 12 kontrol çalıştırır. Mevcut regresyon kontrollerini silme; tam koşu ancak kapsam gerektirirse.
