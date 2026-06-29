# Security Notes

OneClickClose can close processes selected by configuration. Review `close-user-apps.config.json` before use.

Default behavior is conservative:

- Protected process names are never closed.
- Processes under the Windows system directory are protected.
- Processes are scored by visible window, parent process, path type, and memory before display.
- Password managers, sync tools, proxy tools, remote access tools, Tailscale, Syncthing, drivers, and Windows services are protected by default.
- Cleanup uses three stages: `WM_CLOSE`, `WM_QUERYENDSESSION`, then force close.
- Force close is still limited to `forceAllowedNames`.
- User preference files under `%LOCALAPPDATA%\OneClickClose` only store local history and suggestions.

If you distribute a modified configuration, clearly document what was changed.
