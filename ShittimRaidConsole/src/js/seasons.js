// shipped season data lifted from the client's ExcelDB. Group order matches OpenRaidBossGroupId and hp is the JP launch pool - those numbers were sized for a whole region's playerbase, so scale them down to something your handful of installs can actually kill before publishing.
export const SEASONS = [
  {
    seasonId: 814,
    label: '814 - two bosses, world clear rewards (ticket A)',
    days: 13,
    bosses: [
      { groupId: 814000, hp: 9000000000000 },
      { groupId: 814100, hp: 9000000000000 },
    ],
  },
  {
    seasonId: 821,
    label: '821 - five fronts, round two, finale (ticket B)',
    days: 12,
    bosses: [
      { groupId: 821000, hp: 2940000000000 },
      { groupId: 821200, hp: 5145000000000 },
      { groupId: 821300, hp: 2940000000000 },
      { groupId: 821100, hp: 1666000000000 },
      { groupId: 821400, hp: 784000000000 },
      { groupId: 821001, hp: 3185000000000 },
      { groupId: 821201, hp: 9100000000000 },
      { groupId: 821301, hp: 10920000000000 },
      { groupId: 821101, hp: 2912000000000 },
      { groupId: 821401, hp: 1274000000000 },
      { groupId: 821501, hp: 10010000000000 },
      // the scripted finale has no pool of its own, hp 0 is what shipped
      { groupId: 821602, hp: 0 },
    ],
  },
  {
    seasonId: 823,
    label: '823 - single boss plus decisive (ticket C)',
    days: 6,
    bosses: [
      { groupId: 823000, hp: 26584000000000 },
      { groupId: 823100, hp: 0 },
    ],
  },
  {
    seasonId: 10814,
    label: '10814 - 814 rerun (ticket A)',
    days: 6,
    bosses: [
      { groupId: 10814000, hp: 4300000000000 },
      { groupId: 10814100, hp: 3500000000000 },
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
];
