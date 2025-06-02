import { environment, getSiteUrl } from '@paklo/core/environment';

const { development, main } = environment;
const siteUrl = getSiteUrl({ defaultValue: 'https://paklo.software' });

const siteConfig = {
  siteUrl: siteUrl,
  description: 'Dependabot-like automation on Azure DevOps',

  // either in development or not on main branch
  showDrafts: development || !main,
};

export default siteConfig;
