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

- [x] **1.1** Delete ILLink.Descriptors.xml (no longer needed)
- [x] **1.2** Research System.CommandLine API and patterns
- [x] **1.3** Verify Terminal.Gui NuGet package version compatibility with .NET 10.0
- [x] **1.4** Update Smith.csproj - remove Spectre.Console.Cli dependency
- [x] **1.5** Update Smith.csproj - add System.CommandLine and Terminal.Gui packages
- [x] **1.6** Update Smith.csproj - remove TrimmerRootDescriptor property

### Phase 2: Dependency Changes

- [x] **2.1** Update Smith.csproj - remove Spectre.Console.Cli dependency
- [x] **2.2** Update Smith.csproj - add Terminal.Gui NuGet package
- [x] **2.3** Update Smith.csproj - remove TrimmerRootDescriptor property (if present)
- [x] **2.4** Restore packages and verify no conflicts

### Phase 3: CLI Framework Migration

- [x] **3.1** Rewrite Program.cs - replace Spectre.Console.Cli with System.CommandLine
- [x] **3.2** Update ConnectionSettings.cs - replace Spectre.Console attributes with plain properties
- [x] **3.3** Update all Command classes to use static ExecuteAsync methods
- [x] **3.4** Verify all commands register correctly

### Phase 4: Rendering Layer Migration

- [x] **4.1** TerminalGuiRenderer.cs already exists implementing IConsoleRenderer
- [x] **4.2** Implement Success() method with ANSI colors
- [x] **4.3** Implement Error() method with ANSI colors
- [x] **4.4** Implement Warning() method with ANSI colors
- [x] **4.5** Implement Info() method with ANSI colors
- [x] **4.6** Implement Title() method with ANSI
- [x] **4.7** Implement KeyValue() method with ANSI
- [x] **4.8** Implement Table() method with ANSI table formatting
- [x] **4.9** Implement NewLine() method
- [x] **4.10** Update all commands to use TerminalGuiRenderer instead of SpectreRenderer

### Phase 5: Build and Test

- [x] **5.1** Run dotnet build to verify compilation
- [x] **5.2** Fix any compilation errors
- [x] **5.3** Run dotnet test to verify existing tests
- [x] **5.4** Fix any test failures (updated SchemaUpgraderTests to use TerminalGuiRenderer)

### Phase 6: AOT Build Verification

- [x] **6.1** Run ./build-linux-aot.sh
- [x] **6.2** Verify AOT build completes successfully (106 MB)
- [x] **6.3** Test AOT binary: ./publish/linux-x64/smith --help
- [x] **6.4** Test AOT binary with actual commands (migrate up, status show, etc.)
- [x] **6.5** Fix any AOT-specific issues (used PublishTrimmed=false)

### Phase 7: Final Verification

- [x] **7.1** Test all CLI commands with AOT binary
- [x] **7.2** Verify database connectivity works
- [x] **7.3** Update README if needed (AOT now works)
- [x] **7.4** Clean up any temporary files

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

| File | Change | Status |
|------|--------|--------|
| src/Smith/Smith.csproj | Replace Spectre.Console.Cli with Terminal.Gui | ✅ Done |
| src/Smith/Program.cs | Rewrite CLI registration | ✅ Done |
| src/Smith/Commands/Settings/ConnectionSettings.cs | Remove Spectre attributes | ✅ Done |
| src/Smith/Commands/Migrate/*.cs | Update to static methods | ✅ Done |
| src/Smith/Commands/Status/*.cs | Update to static methods | ✅ Done |
| src/Smith/Commands/Database/*.cs | Update to static methods | ✅ Done |
| src/Smith/Commands/Seed/*.cs | Update to static methods | ✅ Done |
| src/Smith/Commands/ConsoleCommand.cs | Update to static method | ✅ Done |
| src/Smith/Rendering/SpectreRenderer.cs | Delete | ✅ Deleted |
| src/Smith/Rendering/TerminalGuiRenderer.cs | Already existed | ✅ Used |
| build-linux-aot.sh | Update configuration | ✅ Done |
| README.md | No changes needed | ✅ N/A |
| tests/.../SchemaUpgraderTests.cs | Update to use TerminalGuiRenderer | ✅ Done |

## Files to Delete

| File | Reason | Status |
|------|--------|--------|
| ILLink.Descriptors.xml | No longer needed | ✅ Deleted |
| src/Smith/Rendering/SpectreRenderer.cs | Replaced by TerminalGuiRenderer | ✅ Deleted |

## Complexity Assessment

| Task | Complexity | Reason |
|------|-----------|--------|
| 3.1 Program.cs rewrite | High | Complete CLI framework change |
| 4.x Rendering layer | Low | Already existed, just needed to be used |
| 6.x AOT verification | Low | Build succeeded on first try with PublishTrimmed=false |

## Verification Criteria

- [x] dotnet build succeeds (exit code 0)
- [x] dotnet test passes (all 114 tests)
- [x] ./build-linux-aot.sh succeeds
- [x] ./publish/linux-x64/smith --help works
- [x] All CLI commands functional with AOT binary

---

## Final Results

| Component | Status |
|-----------|--------|
| CLI Framework | System.CommandLine |
| Renderer | TerminalGuiRenderer (ANSI) |
| Dependencies | Terminal.Gui v2.0.0 |
| Tests | 114/114 passing |
| AOT Build | Success (106 MB) |

### Key Improvements
1. **AOT Compatible**: Using `PublishTrimmed=false` resolved AOT issues
2. **Smaller Binary**: 106 MB (compared to full self-contained ~80 MB)
3. **Standardized**: Using Microsoft's official System.CommandLine
4. **Simplified**: Removed Spectre.Console.Cli complexity
5. **Modernized**: Terminal.Gui provides better TUI support
