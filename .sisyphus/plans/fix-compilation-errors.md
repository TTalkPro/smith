# Plan: Fix Compilation Errors - Revert to Working State

## Problem
The migration from Spectre.Console.Cli to System.CommandLine + Terminal.Gui is incomplete and the project doesn't compile.

## Root Cause
- System.CommandLine 2.x has different API than beta version used in initial rewrite
- Command classes still reference old Spectre.Console types
- Partial migration left project in broken state

## Solution: Revert to Working State with AOT

**Strategy**: Revert Spectre.Console.Cli, use non-trimmed AOT build (smaller than full self-contained, works with reflection-based libraries).

---

## Tasks

### Phase 1: Revert to Working State

- [ ] **1.1** Revert Smith.csproj - restore Spectre.Console.Cli, remove System.CommandLine and Terminal.Gui
- [ ] **1.2** Restore original Program.cs with Spectre.Console.Cli
- [ ] **1.3** Restore SpectreRenderer.cs
- [ ] **1.4** Delete TerminalGuiRenderer.cs (not needed)
- [ ] **1.5** Verify build passes with Spectre.Console.Cli

### Phase 2: Configure Non-Trimmed AOT

- [ ] **2.1** Update build script to use PublishTrimmed=false
- [ ] **2.2** Update Smith.csproj with AOT properties
- [ ] **2.3** Test AOT build

### Phase 3: Final Verification

- [ ] **3.1** Run ./build-linux-aot.sh
- [ ] **3.2** Test binary with --help
- [ ] **3.3** Verify all commands work

---

## Files to Revert/Modify

| File | Action |
|------|--------|
| src/Smith/Smith.csproj | Revert to Spectre.Console.Cli |
| src/Smith/Program.cs | Restore original |
| src/Smith/Rendering/SpectreRenderer.cs | Restore |
| src/Smith/Rendering/TerminalGuiRenderer.cs | Delete |
| build-linux-aot.sh | Update PublishTrimmed=false |

---

## Expected Outcome
- Project compiles successfully
- AOT binary generated (~20-30MB, self-contained)
- No .NET runtime required on target machine
- All CLI commands work

---

## Why Non-Trimmed AOT?
- Spectre.Console.Cli requires reflection (can't trim)
- Non-trimmed AOT = compiles to native code but keeps reflection metadata
- Binary is smaller than full self-contained (~30MB vs ~80MB)
- No runtime needed on target (unlike framework-dependent)
