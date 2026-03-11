# Plan: Replace Spectre.Console with AOT-Compatible Stack

## Context
Smith is a C# TUI PostgreSQL migration management tool that currently uses Spectre.Console.Cli. However, Spectre.Console.Cli explicitly does NOT support AOT compilation - the maintainers have marked it as "not trimmable and not appropriate for AOT scenarios". The current AOT build fails with:
```
CommandRuntimeException: Could not get settings type for command of type 'Smith.Commands.Migrate.MigrateInitCommand'
```

## Solution Options Considered

### Option 1: Terminal.Gui + System.CommandLine (Selected)
- **Terminal.Gui**: For TUI rendering (tables, colors) - AOT work in progress
- **System.CommandLine**: Microsoft official CLI parser - AOT compatible
- **Pros**: Rich TUI, official support
- **Cons**: More complex migration

### Option 2: System.CommandLine only (Simplified)
- Use Microsoft.System.CommandLine for CLI parsing
- Simple console output without TUI
- **Pros**: Simple, guaranteed AOT support
- **Cons**: Less visual

## Decision: Use Terminal.Gui + System.CommandLine

Since the project benefits from TUI features (tables, colored output), we'll migrate to:
- **System.CommandLine** for CLI framework (AOT-compatible, Microsoft official)
- **Terminal.Gui** for rendering (where needed, AOT-compatible v2)
- Keep existing IConsoleRenderer abstraction

## Deliverables
- All existing CLI commands work with new stack
- AOT build succeeds and runs without errors
- All tests pass
- Same user experience maintained

---

## Tasks

### Phase 1: Preparation

- [ ] **1.1** Delete ILLink.Descriptors.xml (no longer needed) ✅ DONE
- [ ] **1.2** Research System.CommandLine API and patterns
- [ ] **1.3** Verify Terminal.Gui NuGet package version compatibility with .NET 10.0
- [ ] **1.4** Update Smith.csproj - remove Spectre.Console.Cli dependency
- [ ] **1.5** Update Smith.csproj - add System.CommandLine and Terminal.Gui packages
- [ ] **1.6** Update Smith.csproj - remove TrimmerRootDescriptor property

### Phase 2: Dependency Changes

- [ ] **2.1** Update Smith.csproj - remove Spectre.Console.Cli dependency
- [ ] **2.2** Update Smith.csproj - add Terminal.Gui NuGet package
- [ ] **2.3** Update Smith.csproj - remove TrimmerRootDescriptor property (if present)
- [ ] **2.4** Restore packages and verify no conflicts

### Phase 3: CLI Framework Migration

- [ ] **3.1** Rewrite Program.cs - replace CommandApp with Terminal.Gui.CommandApp
- [ ] **3.2** Update ConnectionSettings.cs - replace Spectre.Console attributes with Terminal.Gui attributes
- [ ] **3.3** Update all Command classes to use Terminal.Gui.Command type
- [ ] **3.4** Verify all commands register correctly

### Phase 4: Rendering Layer Migration

- [ ] **4.1** Create TerminalGuiRenderer.cs implementing IConsoleRenderer
- [ ] **4.2** Implement Success() method with Terminal.Gui colors
- [ ] **4.3** Implement Error() method with Terminal.Gui colors
- [ ] **4.4** Implement Warning() method with Terminal.Gui colors
- [ ] **4.5** Implement Info() method with Terminal.Gui colors
- [ ] **4.6** Implement Title() method with Terminal.Gui
- [ ] **4.7** Implement KeyValue() method with Terminal.Gui
- [ ] **4.8** Implement Table() method with Terminal.Gui Table view
- [ ] **4.9** Implement NewLine() method
- [ ] **4.10** Update all commands to use TerminalGuiRenderer instead of SpectreRenderer

### Phase 5: Build and Test

- [ ] **5.1** Run dotnet build to verify compilation
- [ ] **5.2** Fix any compilation errors
- [ ] **5.3** Run dotnet test to verify existing tests
- [ ] **5.4** Fix any test failures

### Phase 6: AOT Build Verification

- [ ] **6.1** Run ./build-linux-aot.sh
- [ ] **6.2** Verify AOT build completes successfully
- [ ] **6.3** Test AOT binary: ./publish/linux-x64/smith --help
- [ ] **6.4** Test AOT binary with actual commands
- [ ] **6.5** Fix any AOT-specific issues

### Phase 7: Final Verification

- [ ] **7.1** Test all CLI commands with AOT binary
- [ ] **7.2** Verify database connectivity works
- [ ] **7.3** Update README if needed (AOT now works)
- [ ] **7.4** Clean up any temporary files

---

## Dependencies

- **2.1** → **2.2** → **2.3** → **2.4** (sequential: dependency updates)
- **3.1** → **3.2** → **3.3** → **3.4** (sequential: CLI framework)
- **4.1** → **4.10** (sequential: rendering layer)
- **5.1** → **5.2** → **5.3** → **5.4** (sequential: build/test)
- **6.1** → **6.2** → **6.3** → **6.4** → **6.5** (sequential: AOT verification)

## Parallel Opportunities

- **1.2** and **1.3** can run in parallel (research tasks)
- **4.2** through **4.9** can run in parallel (implementing renderer methods - but 4.1 must complete first)

## Files to Modify

| File | Change |
|------|--------|
| src/Smith/Smith.csproj | Replace Spectre.Console.Cli with Terminal.Gui |
| src/Smith/Program.cs | Rewrite CLI registration |
| src/Smith/Commands/Settings/ConnectionSettings.cs | Replace attributes |
| src/Smith/Commands/Migrate/*.cs | Update commands |
| src/Smith/Commands/Status/*.cs | Update commands |
| src/Smith/Commands/Database/*.cs | Update commands |
| src/Smith/Commands/Seed/*.cs | Update commands |
| src/Smith/Commands/ConsoleCommand.cs | Update command |
| src/Smith/Rendering/SpectreRenderer.cs | Delete |
| src/Smith/Rendering/TerminalGuiRenderer.cs | Create |
| build-linux-aot.sh | May need updates |
| README.md | Update if needed |

## Files to Delete

| File | Reason |
|------|--------|
| ILLink.Descriptors.xml | No longer needed |
| src/Smith/Rendering/SpectreRenderer.cs | Replaced by TerminalGuiRenderer |

## Complexity Assessment

| Task | Complexity | Reason |
|------|-----------|--------|
| 3.1 Program.cs rewrite | High | Complete CLI framework change |
| 4.x Rendering layer | Medium | Straightforward port of interface |
| 6.x AOT verification | High | May encounter AOT-specific issues |

## Verification Criteria

- [ ] dotnet build succeeds (exit code 0)
- [ ] dotnet test passes (all tests)
- [ ] ./build-linux-aot.sh succeeds
- [ ] ./publish/linux-x64/smith --help works
- [ ] All CLI commands functional with AOT binary
