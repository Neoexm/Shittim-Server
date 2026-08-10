// @ts-check
const { themes } = require('prism-react-renderer');

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Shittim Server',
  tagline: 'A private server for Blue Archive on Steam',
  favicon: 'img/favicon.ico',

  url: 'https://docs.shittem-server.com',
  baseUrl: '/',
  organizationName: 'Neoexm',
  projectName: 'Shittim-Server',
  trailingSlash: false,

  onBrokenLinks: 'throw',

  // detect keeps .md files on CommonMark, so angle brackets in command usage strings and config placeholders do not get parsed as JSX.
  markdown: { format: 'detect', hooks: { onBrokenMarkdownLinks: 'warn' } },

  i18n: { defaultLocale: 'en', locales: ['en'] },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          routeBasePath: '/',
          sidebarPath: require.resolve('./sidebars.js'),
          editUrl: 'https://github.com/Neoexm/Shittim-Server/tree/main/docs/',
        },
        blog: false,
        theme: { customCss: require.resolve('./src/css/custom.css') },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: { defaultMode: 'dark', respectPrefersColorScheme: true },
      navbar: {
        title: 'Shittim Server',
        items: [
          { type: 'docSidebar', sidebarId: 'docs', position: 'left', label: 'Documentation' },
          { to: '/modding/', label: 'Make a student', position: 'left' },
          { href: 'https://discord.gg/GANwPn9xX6', label: 'Discord', position: 'right' },
          { href: 'https://github.com/Neoexm/Shittim-Server', label: 'GitHub', position: 'right' },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              { label: 'Installation', to: '/getting-started/installation' },
              { label: 'Control Center', to: '/control-center/' },
              { label: 'Server', to: '/server/architecture' },
              { label: 'Custom students', to: '/modding/' },
            ],
          },
          {
            title: 'Elsewhere',
            items: [
              { label: 'Discord', href: 'https://discord.gg/GANwPn9xX6' },
              { label: 'Releases', href: 'https://github.com/Neoexm/Shittim-Server/releases' },
            ],
          },
        ],
        copyright: 'For educational and research purposes only. Not affiliated with Nexon.',
      },
      prism: {
        theme: themes.github,
        darkTheme: themes.dracula,
        additionalLanguages: ['csharp', 'json', 'bash', 'powershell'],
      },
    }),
};

module.exports = config;
