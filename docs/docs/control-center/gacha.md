---
id: gacha
title: Gacha
---

Recruitment rates and the banner list.

## Drop rates

Three percentages - SSR, SR and R - with a bar showing the split and a pill that says whether they add up to 100. **Normalise to 100%** scales whatever you have typed so it does.

Leaving all three at zero and saving falls back to the game's built-in rates. **Reset to defaults** does that and clears the guaranteed pickup as well.

Rates live in `gacha_config.json` next to the server's build directory, not in `Config.json`, and the server re-reads that file within about five seconds. No restart is needed.

## Guaranteed pickup

Optional. Picking a student here forces every pull to produce her, which overrides the rates entirely.

Forcing a character who is not on the active banner can confuse the client, so this is a debugging tool rather than something to leave on.

## Banners

Read-only, listed for reference. Banners are defined in the game's Excel data, so the only way to change one is to edit that data - which is what the [banner patcher](../server/client-patching.md) does when it writes recruitment banners into the client's `ExcelDB.db`.

Each card shows the banner id, its display order, whether it is a newbie or a selector banner, up to eight featured students, and the sale window.

## Related console commands

`!gacha [type] [value]` sets rates and the guarantee from the console. Recruit point exchange goes through the shop rather than the gacha config - the client sends a shop purchase for it, not a pull.
