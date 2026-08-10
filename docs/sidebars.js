/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    'intro',
    {
      type: 'category',
      label: 'Getting started',
      collapsed: false,
      items: [
        'getting-started/requirements',
        'getting-started/installation',
        'getting-started/first-run',
        'getting-started/from-source',
      ],
    },
    {
      type: 'category',
      label: 'Control Center',
      items: [
        'control-center/index',
        'control-center/overview',
        'control-center/accounts',
        'control-center/inventory',
        'control-center/mail',
        'control-center/events',
        'control-center/raids',
        'control-center/gacha',
        'control-center/notices',
        'control-center/mods',
        'control-center/configuration',
        'control-center/updates',
      ],
    },
    {
      type: 'category',
      label: 'Server',
      items: [
        'server/architecture',
        'server/configuration',
        'server/protocol',
        'server/excel-data',
        'server/admin-api',
        'server/commands',
        'server/client-patching',
      ],
    },
    {
      type: 'category',
      label: 'Making a custom student',
      items: [
        'modding/index',
        'modding/donor',
        'modding/package',
        'modding/import',
        'modding/editing',
        'modding/art-overview',
        'modding/artwork',
        'modding/atlas',
        'modding/packing',
        'modding/own-assets',
        'modding/lobby',
        'modding/battle',
        'modding/beyond',
        'modding/skills',
        'modding/voice',
        'modding/lines',
        'modding/momotalk',
        'modding/troubleshooting',
      ],
    },
  ],
};

module.exports = sidebars;
