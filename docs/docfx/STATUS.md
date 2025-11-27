# DocFX Rollout Status

| Project | GenerateDocumentationFile | CS1591 Suppressed | Documentation Debt | Next Action |
|---------|---------------------------|-------------------|--------------------|-------------|
| DotnetAgents.AgentApi | ✅ | ✅ (temporary) | None – build with `-warnaserror:CS1591` succeeds | Integrate DocFX metadata (already configured) and schedule suppression removal once reviewed |
| DotnetAgents.Web | 🚧 | ❌ | Audit pending | Enable XML docs and add summaries for public components |
| IntelAgent | 🚧 | ❌ | Audit pending | Enable XML docs, document chat services and options |
| IntelAgents.Cli | 🚧 | ❌ | Audit pending | Add XML docs to CLI commands |

- ✅ Projects emit XML documentation when enabled; `DotnetAgents.AgentApi` is the first onboarded service.
- Temporary `CS1591` suppression remains until documentation debt is eliminated for each project.
- Remaining projects will be onboarded sequentially per sprint as outlined in the testing/docs/CI upgrade plan.
