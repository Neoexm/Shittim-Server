// shipped season data lifted from the client's ExcelDB. Group order matches OpenRaidBossGroupId, and hp is the asia column because that is the one the client divides by to draw the world bar - publish less and the boss opens part-dead instead of just dying sooner, so a pool sized for a whole region is the price of a bar that starts full.
export const SEASONS = [
  {
    seasonId: 814,
    label: '814 - two bosses, world clear rewards (ticket A)',
    days: 13,
    bosses: [
      { groupId: 814000, hp: 3000000000000 },
      { groupId: 814100, hp: 3000000000000 },
    ],
  },
  {
    seasonId: 821,
    label: '821 - five fronts, round two, finale (ticket B)',
    days: 12,
    bosses: [
      { groupId: 821000, hp: 1620000000000 },
      { groupId: 821200, hp: 2835000000000 },
      { groupId: 821300, hp: 1620000000000 },
      { groupId: 821100, hp: 918000000000 },
      { groupId: 821400, hp: 432000000000 },
      { groupId: 821001, hp: 1750000000000 },
      { groupId: 821201, hp: 5000000000000 },
      { groupId: 821301, hp: 6000000000000 },
      { groupId: 821101, hp: 1600000000000 },
      { groupId: 821401, hp: 700000000000 },
      { groupId: 821501, hp: 5500000000000 },
      // the scripted finale has no pool of its own, hp 0 is what shipped
      { groupId: 821602, hp: 0 },
    ],
  },
  {
    seasonId: 823,
    label: '823 - single boss plus decisive (ticket C)',
    days: 6,
    bosses: [
      { groupId: 823000, hp: 12019530000000 },
      { groupId: 823100, hp: 0 },
    ],
  },
  {
    seasonId: 10814,
    label: '10814 - 814 rerun (ticket A)',
    days: 6,
    bosses: [
      { groupId: 10814000, hp: 2200000000000 },
      { groupId: 10814100, hp: 1700000000000 },
    ],
  },
  {
    seasonId: 900814,
    label: '900814 - permanent 814, no world pool',
    days: 365,
    bosses: [
      { groupId: 900814000, hp: 0 },
      { groupId: 900814100, hp: 0 },
    ],
  },
  {
    seasonId: 854,
    label: '854 - steel continent, three phases plus replay (interactive)',
    days: 35,
    // servers older than this cannot run an interactive season - they would 500 the raid lobby - so the sync gate has to keep them out
    minServerVersion: '2026.8.9',
    // spawnDay/elimDay stage the phases the way the event ran: the eight combined-operations bosses for two weeks, Malkuth for two, the showdown for the last. 8540900 shares Malkuth's bar through the hp link, and 8541100 is the scripted final with no pool of its own.
    bosses: [
      { groupId: 8540000, hp: 6880000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540100, hp: 6300000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540200, hp: 5400000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540300, hp: 23850000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540400, hp: 57500000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540500, hp: 41300000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540600, hp: 31250000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540700, hp: 21000000000000, spawnDay: 1, elimDay: 14 },
      { groupId: 8540800, hp: 234000000000000, spawnDay: 15, elimDay: 28 },
      { groupId: 8540900, hp: 234000000000000, spawnDay: 15, elimDay: 28 },
      { groupId: 8541000, hp: 200000000000000, spawnDay: 29, elimDay: 35 },
      { groupId: 8541100, hp: 0, spawnDay: 29, elimDay: 35 },
    ],
  },
];
