---
id: atlas
title: 'Step 7: build the sheet'
---

You have loose pieces. You need one 2048 x 2048 PNG with each piece sitting in the right box.

There are two routes and they are very different amounts of work.

## Route A - reuse the skeleton

What almost everyone should do.

Do not open Spine at all. Composite your pieces into the donor's existing boxes and leave the skeleton and the layout file completely untouched. Every animation keeps working because nothing about the rig changed - only the pixels it samples.

What the compositing step has to do:

1. Read the layout file to get every region's name, position, size and rotation flag.
2. Cut your source into upper and lower at the waist, if it came as one image.
3. Scale each piece to fit its box. Use the same scale for the two body pieces or the proportions break at the waist.
4. Apply the 180 degree rotation to the head region.
5. Crop the eyes-to-mouth band out of each expression head into the face plate boxes.
6. Draw the halo into its box.
7. **Stencil every region against the original sheet's transparency**, so nothing bleeds into an overlapping box and the face plates come out features-only.
8. Write a 2048 x 2048 PNG.

That is a scripting job, not something the Control Center does. Nothing in this repo ships it - the working version was a Python script using Pillow. It is maybe 150 lines and the layout file gives you every number you need.

Expect to iterate on scale. If your character is narrower than the donor, the lower body under-fills its box and you get a visible size jump at the waist. That is normally a one-line change to how the halves are scaled.

## Route B - build your own rig

Worth it only if you want motion the donor does not have: hair that sways, an arm that moves, a longer idle.

The good news is that these rigs are tiny. A measured one has:

- **Four bones**: root, the body layer, and two for the halo
- **Four slots**: the two body pieces, the face, the halo
- **Two animations**: an idle and a blink
- **Attachments**: the fourteen expression plates plus the default, swapped into the face slot

That is it. No hair physics, no talk clips, no cloth simulation. Two flat body meshes, a face plate and a halo that turns.

You need **Spine 4.2 specifically** - the version is written into the file header. The Essential tier covers bones, slots, plain image attachments and keyframed animation, which is all this uses. Meshes and weights need Professional, but you only need those if you want the body to bend rather than move as a rigid piece, and for an idle-only character a rigid cut-out looks completely fine standing still.

The work: make the root bone, parent a body bone under it, add two for the halo. Make four slots and attach your pieces. Build the idle as a slow vertical bob on the body bone with the halo turning, about two seconds, looping. Build the blink as a two-frame attachment swap on the face slot. Export as binary skeleton plus texture atlas - **the sheet gets generated here, automatically, from your pieces folder.**

Realistically an afternoon for somebody who has never opened Spine, most of it learning the interface.

**Free alternative:** the Unity Spine runtime reads JSON skeletons as well as binary ones. A four-bone rigid rig with two animations is genuinely hand-writable JSON, maybe 150 lines. No licence and no editor. That is only viable because these rigs are so small, but they are.

One catch on Route B: your bone and slot names have to match the original exactly, because the animation clips reference them by name. Rig against the donor's exported skeleton as a template.

Next: [put it in the game](packing.md).
