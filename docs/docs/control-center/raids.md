---
id: raids
title: Raids
---

Sets the raid season for the account selected in the top bar. Four content types, each with its own list of seasons:

- **Total Assault**
- **Grand Assault**
- **Joint Firing Drill**
- **Final Restriction**

There are around 199 seasons across the four, so the search box takes a season number or a boss name.

Picking a season sets it as the current one for that content, which is the same thing the `setseason` console command does.

## World raid

World raid is scheduled separately, from the raid manifest rather than from this page. If a coordinator URL is configured, every install pointed at the same coordinator chips away at one shared HP pool; with no coordinator the raid still runs off whatever manifest is cached locally, just with a purely local pool.

The coordinator URL lives in `WorldRaidCoordinatorUrl` in the server config.
