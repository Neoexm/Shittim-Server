---
id: art-overview
title: 'Step 5: how her art works'
---

This is the real work. Everything up to here was typing.

Read this page before you draw anything. Almost every wasted afternoon in this process comes from not knowing what is on it.

## What the game actually draws

For a student you only ever see in menus, there are four things:

| Where you see it | What it is | Size |
| --- | --- | --- |
| The card in the student grid | a small PNG | 252 x 204 |
| The little icon in lists | a small PNG | 182 x 144 |
| The profile and collection card | a bigger PNG | 404 x 456 |
| The full figure on the detail screen | a Spine skeleton, drawn live | 2048 x 2048 sheet |

The first three are crops and you can generate them from the illustration in a few minutes. The fourth is the entire visual impression of the character and is where all the effort goes.

There is **no full-body PNG anywhere in the game**. The figure that stands there breathing and blinking is assembled at runtime from pieces. That is why she moves.

The story screens and MomoTalk use the same skeleton, just playing different animations, so there is no separate portrait to make.

## Understanding the sheet

This is the concept everything else depends on, so it is worth getting straight.

The character is stored as one big square PNG, 2048 by 2048, holding all her pieces scattered around it like a page of stickers. Alongside it is a small text file listing where each piece is: a name, a position, a width and a height, and sometimes a flag saying the piece was turned 90 or 180 degrees to make it fit.

The game does not look at the picture and work out what is what. It reads the text file. "The head is the rectangle at x=1191, y=97, 855 wide, 821 tall, upside down." Then it samples exactly those pixels.

Two consequences, and both of them bite people:

**You cannot just drop a picture in.** If you replace the sheet with a nicely laid out image of your character, the head slot still reads the rectangle at 1191,97 - and now that rectangle contains a knee, or half a shoe, or nothing. You get scrambled garbage.

**The scattered layout is not a design.** It is the output of a packing program that rotates and tiles pieces to waste as little space as possible. Nobody chose it. Do not try to reproduce its aesthetic; just hit the boxes.

The sheet is a build artifact, the same way a compiled program is. The input is a folder of loose pieces.

## What the pieces actually are

Here is a real example. This is one shipped student's complete sheet - eighteen regions, and that is everything:

| Region | Box (x, y, width, height) | What it is |
| --- | --- | --- |
| `aru_00` | 2, 651, 1299 x 1395 | lower body, coat, and the rifle |
| `aru_01` | 1191, 97, 855 x 821, **rotated 180** | head and torso |
| `default` | 2, 1751, 262 x 295 | the default face |
| `eye close` | 266, 1691, 222 x 155 | the blink |
| `halo` | 2, 250, 833 x 400 | the halo |
| `01_normal` through `14` | around 220 x 180 each | fourteen expressions |

That is the whole rig. **No separate arms, no separate hair strands, no individually rigged fingers.** Two big body pieces that overlap at the waist, a face plate that gets swapped, and a halo on its own bone so it can turn independently.

Which is genuinely good news: it means a straight picture swap is the entire job and you never have to touch the skeleton.

Different outfits are different rigs with different layouts. Always read the actual text file for the rig you are targeting rather than assuming the numbers above.

## The three things people get wrong

**The face plates are not faces.** They are eyebrows, eyes and a mouth floating on nothing. The face itself - skin, outline, fringe - is already part of the body piece, and the plates lay on top of it. Measured on a real one, they are between 7% and 33% opaque, usually about 11%. A few strokes on an empty canvas.

Put a full opaque face crop in there and you get a rectangle of face hovering over her actual face. It looks exactly as bad as that sounds.

The one exception is the `default` plate, which really is a complete face with the fringe.

**The head is upside down.** The `rotate:180` flag in the text file is not decoration. Draw it the right way up and place it as-is and she is inverted.

**The boxes overlap.** On the sheet measured above, 23 of the declared rectangles overlap each other. The packer interleaved them because the actual artwork inside each one is sparse - the rectangles overlap but the visible pixels do not.

So if you fill every box solidly, neighbouring regions start sampling each other's pixels and you get pieces of one thing bleeding into another.

The fix for that and for the face plates is the same: **stencil against the original.** For each region, take the original sheet's transparency as a mask and clip your artwork to it. Then nothing writes outside where content legitimately belongs.

## The cost of stencilling

It clips your character to the donor's silhouette. Hair wider than the donor's gets cut. This is the same silhouette problem from [picking a donor](donor.md) and it is unavoidable when borrowing a rig - the geometry only exists where the original had artwork.

On one real attempt, stencilling took page coverage from a naive 100% down to 30%, against the original's 42%, and the difference was the character's hair being shredded at the edges.

Two ways to soften it. Grow the mask by 15 to 20 pixels, which reduces the shredding at the cost of a little bleed. Or go back and host on a different rig whose shape suits your character better. The second works much better, and it is why donor choice matters so much.

If the first attempt looks wrong at the edges, the answer is usually not to fiddle with the stencil. It is to host on a different rig.

Next: [make the artwork](artwork.md).
