---
id: updates
title: Updates
---

Two separate things update: the server source, and the Control Center itself.

## Server source

The updater is git-free. **Check for updates** compares the locally recorded commit - a download marker, or `HEAD` if the project root is a real git checkout - against `origin/main` through the GitHub API, and lists the incoming changelog.

Applying an update downloads the changed files and rebuilds the server. A running server is stopped for the build and started again afterwards, and the build output streams to the console dock.

If an install is interrupted partway through, the next launch of the Control Center rolls the changed files back to the version you were on and says so in the console. Files it could not roll back are named.

## Rebuild server

Rebuilds without updating. Installing an update already rebuilds, so this button is for rebuilding after editing the source yourself, or after a build failed.

## The Control Center itself

It checks GitHub Releases on launch and prompts when there is a newer build. Installed builds go through electron-updater and update in place. Portable builds get a notice with a download link instead, because there is nothing to update in place.

**Check for app update** runs that check by hand. Dev runs skip the automatic check.
