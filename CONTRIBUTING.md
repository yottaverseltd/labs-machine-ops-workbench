# Contributing

MachineOps Workbench welcomes focused fixes, tests, documentation, and
simulator scenarios.

1. Open an issue describing the user problem.
2. Create a short branch from `main`.
3. Keep changes in the layer that owns the behaviour.
4. Add tests for new rules and failure paths.
5. Run formatting, Release build, and the complete test suite.
6. Open a pull request using the supplied checklist.

The project uses nullable reference types, latest recommended analyzers,
warnings as errors, compiled Avalonia bindings, central package versions, and
explicit cancellation. New dependencies need a concrete reason and a licence
compatible with MIT distribution.

Hardware controller support is outside the current safety boundary. Proposals
for it need a separate design and threat review before implementation.
