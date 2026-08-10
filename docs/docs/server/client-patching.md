---
id: client-patching
title: Client patching
---

The retail client talks to Nexon. Getting it to talk to loopback instead means editing the installed files, which the server does at startup as a set of hosted services. Every one of them is individually switchable from the Configuration page.

## Finding the install

`ClientInstallDirectory` blank means find it: Steam's own library folders are parsed first, then the conventional locations. Every other client path is derived from whatever that resolves to. Set it explicitly when there are two installs or when the game is somewhere Steam does not know about.

## What gets patched

| Patcher | File | What it changes |
| --- | --- | --- |
| Metadata | `global-metadata.dat` | the server's RSA public key, in three 150-byte chunks, and the region label |
| Gamescale IAS | `gamescale.core.dll` | the login endpoint |
| Nexon platform IAS | the Nexon platform modules | the same |
| Inface IAS | inface | the same |
| Inface config | the inface config file | endpoint configuration |
| GameAssembly IAS | `GameAssembly.dll` | off by default |
| Steam offline | `GameAssembly.dll` | off by default; lets the client hand out an external ticket with Steam offline |
| grap64 | the plugin directory | plugin management |
| Banners | the client's `ExcelDB.db` | recruitment banner rows |
| Region label | `global-metadata.dat` | the title-screen region name |
| Store URL | the client's store lookup | points it at this server so the shop currency check answers with no route out |

The two `GameAssembly.dll` patchers are off by default because Steam restores that file on a verify, and a restored file means the patch is silently gone.

## The region label

The title-screen region name is not on the wire at all. It is the `ServerRegion` enum member **name** inside `global-metadata.dat`. Three things have to spell it identically - the enum member name, the standalone region literal, and the connection group name the server serves - or `Queuing_GetTicket` comes back with an empty gateway URL and the client hangs on the loading screen.

The same mechanism covers every other client-local string: rows in the client's localization table are keyed by a hash of the localization key, so patching a row is how you change any UI string that never crosses the wire.

## Signature matching and client updates

The native patches are anchored on byte signatures, not fixed offsets. A Steam auto-update replaces the DLL and silently un-applies every one of them. The symptom in `logs/log.txt` is:

```
IAS binary patch target was not found: <name>
```

When that happens the signature has to be re-anchored against the new binary. A soft "cannot be processed" popup at the payment step, specifically, is the stamp and payment precondition patch having come unstuck.

## Banners and event dates

Two of the patchers write into the client's own `ExcelDB.db` rather than into a binary:

- the **banner patcher** writes recruitment banner rows
- the **event schedule** rewrites event date ranges so a chosen event reads as permanently open

Both are re-applied on every server start, because a client update replaces `ExcelDB.db` and takes them with it.

The client reads that database when it launches, so both need a game relaunch, not just a server restart. And because the file is locked while the game is running, neither can be applied with Blue Archive open.

## Undoing everything

Verify the game files through Steam. That restores every patched binary and the shipped `ExcelDB.db`, which also removes any custom characters - they live in that database. Turning the auto-patch switches off before the next server start stops them being re-applied.
