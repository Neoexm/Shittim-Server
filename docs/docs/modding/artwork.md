---
id: artwork
title: 'Step 6: make the artwork'
---

Whether you are drawing it yourself or commissioning it, this is the shape of what you need.

## Two body pieces

**Upper** - head and torso, drawn complete down to about mid-thigh.

**Lower** - waist down to the soles of the feet, drawn complete up to about mid-chest.

Yes, they overlap by a lot. That is deliberate and it is what hides the seam.

Both on the same size canvas with the character in the same position, so that stacking them would give you the whole figure. Do not crop to content.

Reference sizes from a real rig: upper 855 x 821, lower 1299 x 1395. Work at twice that or more and let it be scaled down. Anything around 2000 pixels tall for the full figure is fine.

## Face plates

Features only, on transparent. Eyebrows, eyes, mouth, blush. No skin, no hair, no face outline. This is the bit [step 5](art-overview.md) warned about.

- One `default` plate that IS a complete face with the fringe, around 262 x 295
- One closed-eye plate, brows and lashes only, for the blink
- Fourteen expression plates, around 220 x 180 each

A reasonable set: normal, responding, smile, embarrassed, serious, depressed, responding 2, smile 2, smile 3, embarrassed 2, embarrassed 3, plus three of your choice. If fourteen is too many, do five or six good ones and repeat them - several talk states will look the same, which nobody notices much.

Every face file must be the **same canvas size with the character in the same position**. If the eyes sit at x=400, y=200 in one, they must sit there in all of them. Compositing is done blind against the boxes, so a misaligned canvas puts the eyes on her forehead.

## The halo

Around 833 x 400 on transparent, kept completely separate from the head because it turns on its own bone. A halo drawn onto the hair rotates with the head and looks wrong.

A halo is a ring, a few highlights and some transparency, so this is the piece to worry about least.

## The pose

Standing, facing the viewer, arms relaxed at the sides and **not crossing the body**, legs together, head level, neutral expression.

Every part of the character that something else overlaps has to be reconstructed - a forearm behind a coat still has to be a whole forearm, because when the bone moves it exposes the hole. A dynamic pose with arms across the torso multiplies the work enormously. It is usually faster to redraw in a neutral pose than to reconstruct one.

Next: [build the sheet](atlas.md).
