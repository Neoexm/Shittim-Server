---
id: index
title: Control Center
sidebar_label: Overview of the app
---

Shittim Control Center is the admin panel and the launcher. It is an Electron app with no framework, no bundler and no build step - the pages are plain ES modules.

## The shell

**Title bar.** A 32px custom bar with the window controls.

**Navigation rail.** Text-only, down the left. The pages are Overview, Accounts, Inventory, Mail, Events, Raids, Gacha, Notices, Mods, Configuration and Updates.

**Account selector.** In the top bar. Pages marked as needing a target - Inventory, Mail and Raids - act on whichever account is selected here. This is not the same setting as "which account does the game log into", which lives on the Accounts page.

**Console dock.** Collapsible, at the bottom. Everything the server and the proxy write to stdout ends up here, along with the Control Center's own notices about updates and rolled-back installs.

**Status bar.** Process state for the server and the proxy, the probe target, and the global power button that starts and stops both.

## Online, live and ready

The status indicator distinguishes two things, because a server that is listening is not necessarily a server that can serve:

- **live** means the web host answered `/health`, so the process is up and the port is bound.
- **ready** means `/api/admin/status` answered. That handler hits the database, so a 200 means the server is genuinely able to serve.

Pages that need the server wait for ready, not live.

## Where its settings live

The Control Center keeps its own settings (the project root, window state) separately from the server's. Everything it shows on the Configuration page is read from and written to the server's `Config/Config.json` directly, so editing that file by hand and editing it here are the same thing.

The one exception is gacha rates, which live in `gacha_config.json` next to the build directory and are hot-reloaded by the server within about five seconds.
