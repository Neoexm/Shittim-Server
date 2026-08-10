---
id: accounts
title: Accounts
---

Everything about the accounts on this server. Nothing here is tied to Steam, and there is no limit on how many you make.

## Which account the game logs into

The dropdown at the top of the page sets `SelectedAccountId` in the server config. When it is set, every login is answered with that account regardless of which publisher identity connects, from the game's next launch. Clearing it puts the game back to following the Steam account.

That is a different setting from the account selector in the Control Center's top bar, which only decides what the Inventory, Mail and Raids pages act on. It is normal to have the game logged into one account while you edit another.

## The account list

Each row shows the nickname, the server id, the level and the primary currency balances - Gem, Gold, AP, arena tickets, raid tickets and master coins.

- **Create** makes a new account with a nickname.
- Selecting a row opens the detail view: nickname, level, and the full currency list.
- **Delete** removes the account and everything hanging off it.

## Currencies

Currency ids are stable and mirror the game's own enum. The ones you are most likely to want:

| Id | Name |
| --- | --- |
| 1 | Gold |
| 2 | GemPaid |
| 3 | GemBonus |
| 4 | Gem |
| 5 | ActionPoint |
| 7 | ArenaTicket |
| 8 | RaidTicket |
| 18 | MasterCoin |

Gem is derived from the paid and bonus stacks, so setting it directly does not behave the way setting the two components does. Action points cap at the overcharge limit in the game data (999), and anything above that is delivered as inventory-full mail instead - a stored value above the cap makes spending look like it does nothing, because the client clamps the counter it displays.
