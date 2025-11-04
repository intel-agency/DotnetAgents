# DotnetAgents Documentation

Welcome to the DotnetAgents technical documentation set. This site is generated with [DocFX](https://dotnet.github.io/docfx/) and is organized into two major sections:

- **Guides & Articles** – architectural overviews, how-to guides, and operational runbooks for the DotnetAgents platform.
- **API Reference** – API surface generated from the .NET projects within this repository using XML documentation comments.

This DocFX configuration is being rolled out incrementally. The initial milestone focuses on the Agent API surface; subsequent iterations will bring the remaining services and clients online.

## Local build prerequisites

1. Ensure the .NET SDK defined in `global.json` is installed.
2. Install DocFX locally via `dotnet tool install --global docfx` or use the containerized workflow defined in project scripts (coming soon).
3. Run `docfx metadata` followed by `docfx build` inside the `docs/docfx` directory to generate the site.

## Structure

```
/docs
  └── docfx
      ├── docfx.json        # main configuration file
      ├── articles/         # conceptual documentation (you are here)
      └── api/              # generated API reference (output)
```

Happy documenting!
