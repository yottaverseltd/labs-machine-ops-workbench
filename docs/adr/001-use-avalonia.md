# ADR 001: Use Avalonia for the desktop

Status: accepted

## Context

The workbench needs one desktop codebase on Windows and Linux, compiled
bindings, native window integration, and a testable MVVM model.

## Decision

Use Avalonia 12 with compiled bindings and CommunityToolkit.Mvvm. Keep
platform-specific file selection behind `IGCodeFilePicker`.

## Consequences

The same view and view model run on both target platforms. Packaging still has
platform-specific work, and Linux requires its normal desktop libraries.
