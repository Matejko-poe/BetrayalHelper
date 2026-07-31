# Contributing

Contributions and reproducible bug reports are welcome.

## Development setup

1. Clone the repository into `ExileCore/Plugins/Source/BetrayalHelper`.
2. Start ExileCore or run `dotnet build BetrayalHelper.csproj --no-restore` from that folder.
3. Keep changes focused and preserve configurable Farm/Filler and Run/Skip behavior.
4. Update `CHANGELOG.md` for user-visible changes.

## Parser changes

When adding or repairing an encounter parser, include:

- exact English `Action.Id`,
- exact English event text,
- expected board transition,
- expected button,
- at least one counterexample where the option should be rejected.

Do not generalize from the color of a relationship alone. Score relationship topology and destructive side effects separately.

## Pull requests

- Do not include `bin`, `obj`, local HUD configuration, tokens, recordings, or personal paths.
- Explain how the change was tested in ExileCore.
- Keep formatting changes separate from behavior changes when practical.
