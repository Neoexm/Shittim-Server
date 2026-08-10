---
id: intro
title: Shittim Server
sidebar_label: Introduction
slug: /
---

Shittim Server is a private server for Blue Archive's Steam release. It is an ASP.NET Core application on .NET 10 that answers the same protocols the official server does, keeps progress in a local SQLite database, and patches the installed client just enough for it to talk to loopback instead of Nexon.

It is far enough along to be playable: log in, pull on banners, clear campaign stages including the strategy map with a working enemy phase, run the cafe, claim missions and attendance, craft, shop, read the story, and replay any past event or minigame.

## The pieces

**Shittim Control Center** is the Electron app you actually use. It installs everything the server needs, starts and stops the server and the proxy, and is the admin panel: accounts, inventory, mail, gacha rates, event schedule, raid seasons, notices and mods all live there. It also keeps itself and the server up to date.

**Shittim-Server** is the server. It exposes a gateway endpoint the client sends its encrypted protocol traffic to, an `/api/admin` surface the Control Center drives, and a set of SDK endpoints that stand in for Nexon's own login stack.

**mitmproxy** sits between the client and the network and redirects the hostnames the game contacts at the local server. The Control Center installs it and trusts its certificate for you.

**The game client** is the retail Steam install, patched in place by the server at startup: the metadata blob gets the gateway's public key and the region label, a few native modules get their login checks pointed at loopback, and the client's own `ExcelDB.db` gets recruitment banners and forced event dates written into it.

## Where to start

If you just want to play, read [Requirements](getting-started/requirements.md) and then [Installation](getting-started/installation.md).

If you want to know what a particular Control Center page does, the [Control Center](control-center/index.md) section has one page per view.

If you want to add your own student to the game, go straight to [Making a custom student](modding/index.md).

Questions, bugs and support: [Discord](https://discord.gg/GANwPn9xX6).

For educational and research purposes only. Not affiliated with Nexon.
