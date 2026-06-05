# Contributing to XiaoQiao Click Tool

Thanks for your interest in contributing. XiaoQiao Click Tool is an open-source Windows desktop automation utility focused on safe, configurable, offline click automation for non-technical users.

## Ways to contribute

- Report bugs with clear reproduction steps
- Suggest usability improvements for non-technical users
- Improve WPF UI layout, accessibility, and visual polish
- Add tests or reliability checks for click logic, validation, and settings persistence
- Improve installer, release, and documentation workflows
- Review edge cases around Windows permissions, DPI scaling, multi-monitor setups, and long-running tasks

## Development setup

Requirements:

- Windows
- .NET SDK that supports the project target framework
- Visual Studio or any editor that can work with C# / WPF projects

Build the solution from the repository root:

```powershell
dotnet build .\XiaoQiaoClickTool.slnx -c Release
```

Run the app during development:

```powershell
dotnet run --project .\wpf\XiaoQiaoClickTool\XiaoQiaoClickTool.csproj
```

## Pull request guidelines

- Keep changes focused and minimal
- Prefer reliability and user safety over broad rewrites
- Do not commit build outputs, installers, logs, local configuration, or tool caches
- Update README, ROADMAP, or CHANGELOG when behavior or release-facing details change
- Verify the project builds before opening a pull request

## Safety and product principles

- The app should remain local-first and offline-friendly
- User data, settings, logs, and history should stay on the local machine
- Long-running click tasks should have clear stop conditions and visible status
- Permission-related behavior should be explicit, especially for administrator-elevated target apps
- Non-technical users should be able to understand setup, operation, and failure messages
