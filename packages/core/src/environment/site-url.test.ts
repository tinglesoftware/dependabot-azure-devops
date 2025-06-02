import { expect, test } from 'vitest';

import { getSiteUrlCombined, getSiteUrlForAca, getSiteUrlForAppService, getSiteUrlForSwa } from './site-url';

const WEBSITE_AUTH_V2_CONFIG_JSON_1 =
  '{"platform":{"enabled":true},"globalValidation":{"excludedPaths":["/.swa/health.html"]},"identityProviders":{"azureStaticWebApps":{"registration":{"clientId":"black-bush-020715303.5.azurestaticapps.net"}}},"legacyProperties":{"configVersion":"v2","legacyVersion":"V2"}}';
const WEBSITE_AUTH_V2_CONFIG_JSON_2 =
  '{"platform":{"enabled":true},"globalValidation":{"excludedPaths":["/.swa/health.html"]},"identityProviders":{"azureStaticWebApps":{"registration":{"clientId":"black-bush-020715303-331.westeurope.5.azurestaticapps.net"}}},"legacyProperties":{"configVersion":"v2","legacyVersion":"V2"}}';

test('should work for Azure ContainerApps', () => {
  // should return undefined if CONTAINER_APP_ENV_DNS_SUFFIX is not set
  process.env.CONTAINER_APP_NAME = 'paklo-website';
  expect(getSiteUrlForAca()).toBe(undefined);
  delete process.env.CONTAINER_APP_NAME;

  // should return undefined if CONTAINER_APP_NAME is not set
  process.env.CONTAINER_APP_ENV_DNS_SUFFIX = 'jollyplant-9349db20.westeurope.azurecontainerapps.io';
  expect(getSiteUrlForAca()).toBe(undefined);
  delete process.env.CONTAINER_APP_ENV_DNS_SUFFIX;

  // should return the correct site URL
  process.env.CONTAINER_APP_ENV_DNS_SUFFIX = 'jollyplant-9349db20.westeurope.azurecontainerapps.io';
  process.env.CONTAINER_APP_NAME = 'paklo-website';
  expect(getSiteUrlForAca()).toBe('https://paklo-website.jollyplant-9349db20.westeurope.azurecontainerapps.io');
  delete process.env.CONTAINER_APP_ENV_DNS_SUFFIX;
  delete process.env.CONTAINER_APP_NAME;
});

test('should work for Azure WebApps', () => {
  // should return undefined if WEBSITE_HOSTNAME is not set
  expect(getSiteUrlForAppService()).toBe(undefined);

  // should return the correct site URL
  process.env.WEBSITE_HOSTNAME = 'paklo-website.azurewebsites.net';
  expect(getSiteUrlForAppService()).toBe('https://paklo-website.azurewebsites.net');
  delete process.env.WEBSITE_HOSTNAME;
});

test('should work for Azure Static WebApps', () => {
  expect(getSiteUrlForSwa()).toBe(undefined);

  process.env.WEBSITE_AUTH_V2_CONFIG_JSON = WEBSITE_AUTH_V2_CONFIG_JSON_1;
  expect(getSiteUrlForSwa()).toBe('https://black-bush-020715303.5.azurestaticapps.net');
  delete process.env.WEBSITE_AUTH_V2_CONFIG_JSON;

  process.env.WEBSITE_AUTH_V2_CONFIG_JSON = WEBSITE_AUTH_V2_CONFIG_JSON_2;
  expect(getSiteUrlForSwa()).toBe('https://black-bush-020715303-331.westeurope.5.azurestaticapps.net');
  delete process.env.WEBSITE_AUTH_V2_CONFIG_JSON;

  expect(getSiteUrlForSwa()).toBe(undefined);

  // Test with the environment variable
  process.env.WEBSITE_AUTH_V2_CONFIG_JSON = WEBSITE_AUTH_V2_CONFIG_JSON_2;
  expect(getSiteUrlForSwa()).toBe('https://black-bush-020715303-331.westeurope.5.azurestaticapps.net');
  delete process.env.WEBSITE_AUTH_V2_CONFIG_JSON;
});

test('development uses localhost', () => {
  expect(getSiteUrlCombined({ development: true, main: true, defaultValue: 'https://contoso.com' })).toBe(
    'http://localhost:3000',
  );
  expect(getSiteUrlCombined({ development: false, main: true, defaultValue: 'https://contoso.com' })).toBe(
    'https://contoso.com',
  );
});

test('main uses default value', () => {
  expect(getSiteUrlCombined({ development: false, main: true, defaultValue: 'https://contoso.com' })).toBe(
    'https://contoso.com',
  );
});

test('non-main uses correct value', () => {
  // works for ACA
  process.env.CONTAINER_APP_ENV_DNS_SUFFIX = 'blackbush-020715303.westeurope.azurecontainerapps.io';
  process.env.CONTAINER_APP_NAME = 'paklo-website';
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'https://paklo-website.blackbush-020715303.westeurope.azurecontainerapps.io',
  );
  delete process.env.CONTAINER_APP_ENV_DNS_SUFFIX;
  delete process.env.CONTAINER_APP_NAME;

  // works for App Service
  process.env.WEBSITE_HOSTNAME = 'paklo-website.azurewebsites.net';
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'https://paklo-website.azurewebsites.net',
  );
  delete process.env.WEBSITE_HOSTNAME;

  // works for SWA
  process.env.WEBSITE_AUTH_V2_CONFIG_JSON = WEBSITE_AUTH_V2_CONFIG_JSON_2;
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'https://black-bush-020715303-331.westeurope.5.azurestaticapps.net',
  );
  delete process.env.WEBSITE_AUTH_V2_CONFIG_JSON;

  // works for Vercel
  process.env.VERCEL_BRANCH_URL = 'website-git-dependabot-npmandyarn-360aad-maxwell-werus-projects.vercel.app/';
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'https://website-git-dependabot-npmandyarn-360aad-maxwell-werus-projects.vercel.app/',
  );
  delete process.env.VERCEL_BRANCH_URL;

  // works for Cloudflare Pages
  process.env.CF_PAGES_URL = 'http://website-git-dependabot-npmandyarn-360aad-maxwell-werus-projects.pages.dev';
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'http://website-git-dependabot-npmandyarn-360aad-maxwell-werus-projects.pages.dev',
  );
  delete process.env.CF_PAGES_URL;

  // fallback
  expect(getSiteUrlCombined({ development: false, main: false, defaultValue: 'https://contoso.com' })).toBe(
    'https://contoso.com',
  );
});
