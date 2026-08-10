---
id: donor
title: 'Step 1: pick a donor'
---

## Why cloning, not building

A student is not one database row. She is around eighteen tables' worth of rows keyed to her id, plus a separate row for her display name, plus a few hundred assets she refers to by name. Writing all of that by hand is a big job and most of the ways it goes wrong break the game in ways that are hard to trace.

So the importer does not write a student from nothing. It **copies** one.

You pick a donor - a student who already exists - and every row belonging to her gets copied to a free id, with the id numbers buried inside those rows rewritten to point at the new one. Anything you do not deliberately change still points at the donor's stuff. That is the whole trick: your new student borrows the donor's art, voice, skills and behaviour, so she works everywhere the donor works from the first minute.

Once she is in the database there is nothing to distinguish her from a student the developers shipped. The only record that her id was made up locally is a small file the Control Center keeps.

## What the donor decides

Everything you do not override:

| You get the donor's | So |
| --- | --- |
| Artwork and portraits | she looks exactly like the donor, until you replace the pictures |
| Skills, weapon, gear | she plays exactly like the donor |
| Battle model and animations | she fights as the donor |
| Voice lines and dialogue | she sounds like the donor |
| Memory lobby | the donor's lobby |
| Cafe behaviour | the donor's |

## What actually matters when picking

**Shape.** If you are going to replace the art later, pick a donor whose body shape and hair are close to what you have in mind. The rig only has geometry where the original had artwork, so a character much wider or with much longer hair than the donor gets cut off at the edges. This is the single most common disappointment, and it is decided here, before you have drawn anything. [How her art works](art-overview.md) explains why.

**Completeness.** Pick a fully featured student rather than an event-only or limited one, so she has all the rows worth copying.

Next: [build the mod zip](package.md).
