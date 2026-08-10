---
id: events
title: Events
---

Every event and minigame in the client version you have, with a switch to force it open.

## How forcing an event open works

Nothing about an event's schedule is on the wire. The client works out for itself whether an event is running by comparing the clock against the dates in its own copy of the game data. Switching an event on here rewrites those dates in the installed game so they read as permanently open, and switching it off puts the shipped dates back.

Because the client reads its event table when it launches, you have to restart the game after applying - restarting the server is not enough.

Any number of events can run at once. What an event switched on gets you is its icon on the lobby rail and its own menus, not a slot in the rotating banner on the front page.

The schedule is re-applied every time the server starts, so a game update that replaces the table does not quietly close everything again.

## The list

One row per event, with reruns grouped under the run they repeat rather than sorted by id. A rerun and its original are the same event to a player and differ only in which dates they carry.

Columns: the event name, the featured students, what content it has, when it ran, whether it shows a lobby icon, and the open switch.

**Find** searches the id, the name, the internal key, the featured students, the event currency and the content types. **Show** filters to events with a minigame, events currently forced open, reruns, events with a lobby icon, or events with no name in the data. **Sort** reorders the list.

**Close everything** clears every switch at once. **Apply** writes the change.

## Minigames

Events whose content is a minigame rather than stages and a shop are labelled with what the minigame is: rhythm, shooting, board game, tower defense, Dream Maker, road puzzle, card battle, dice race, treasure hunt, conquest, field, location, card shop, box gacha or fortune gacha.

## Unlocks

Selecting an event shows what the account has unlocked inside it and lets you unlock content directly, which is useful for events whose entry conditions depend on progress you do not have.
