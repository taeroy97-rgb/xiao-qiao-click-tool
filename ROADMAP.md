# Roadmap

XiaoQiao Click Tool aims to become a reliable, open-source Windows desktop automation utility for non-technical users.

## Near term

- Improve validation messages for invalid timing, range, and stop-condition inputs
- Add more structured release notes for each GitHub Release
- Add more manual test cases for multi-monitor and DPI scaling scenarios
- Improve README with an English quickstart section
- Add GitHub Issues for known improvements and user-requested features

## Reliability improvements

- Add automated tests for settings parsing and validation logic
- Add safer handling for interrupted click tasks and cancellation edge cases
- Improve diagnostics for failed click delivery
- Review long-running task behavior under sleep, lock screen, and remote desktop disconnect scenarios

## User experience improvements

- Improve first-run guidance for non-technical users
- Add clearer in-app explanations for random range clicking and random timing
- Improve accessibility for keyboard navigation and high-contrast environments
- Refine visual layout and spacing for smaller displays

## Installer and distribution

- Keep GitHub Releases as the primary public installer channel
- Improve installer release notes and versioning consistency
- Investigate code signing options to reduce Windows unknown-publisher warnings
- Explore an optional update-check flow that does not compromise offline use

## Long term

- Add a safer preset system for common automation scenarios
- Add import/export for user configurations
- Add richer logs and troubleshooting diagnostics
- Build a small contributor workflow around issue templates and release checklists
