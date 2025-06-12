interface SiteUrlOptions {
  /** Whether the current environment is development. */
  development: boolean;

  /** Whether the current branch is the main branch. */
  main: boolean;

  /** The default URL to use if no other URL is found. */
  defaultValue: string;
}

/**
 * Get the site URL based on the environment variables.
 * @param options - The options to use.
 * @returns The site URL.
 */
export function getSiteUrlCombined({ development, main, defaultValue }: SiteUrlOptions) {
  // if we are in development, use localhost
  if (development) return `http://localhost:${process.env.PORT || 3000}`;

  // if we are on the main branch, use the known URL
  if (main) return defaultValue;

  // if we are on Azure ContainerApps, use the provided URL
  let value = getSiteUrlForAca();
  if (value && value.length > 0) return value;

  // if we are on Azure App Service, use the provided URL
  value = getSiteUrlForAppService();
  if (value && value.length > 0) return value;

  // if we are on Azure Static WebApps, use the provided URL
  value = getSiteUrlForSwa();
  if (value && value.length > 0) return value;

  // if we are on Vercel, use the provided URL
  value = process.env.VERCEL_BRANCH_URL;
  if (value && value.length > 0) return `https://${value}`;

  // if we are on Cloudflare Pages, use the provided URL
  value = process.env.CF_PAGES_URL;
  if (value && value.length > 0) return value;

  return defaultValue; // fallback (edge cases)
}

export function getSiteUrlForAca(): string | undefined {
  /*
   * Having looked at the available ENV variables when deployed, we can form the URL from
   * combinations of the following variables:
   * CONTAINER_APP_ENV_DNS_SUFFIX (e.g. "jollyplant-9349db20.westeurope.azurecontainerapps.io")
   * CONTAINER_APP_NAME (e.g. "paklo-website")
   */

  const suffix = process.env.CONTAINER_APP_ENV_DNS_SUFFIX;
  const name = process.env.CONTAINER_APP_NAME;
  if (!suffix || !name) return undefined;
  return `https://${name}.${suffix}`;
}

export function getSiteUrlForAppService(): string | undefined {
  /*
   * Environment variables for Azure App Service are documented at
   * https://learn.microsoft.com/en-us/azure/app-service/reference-app-settings?tabs=kudu%2Cdotnet#app-environment
   *
   * WEBSITE_HOSTNAME (e.g. "paklo-website.azurewebsites.net")
   */

  const value = process.env.WEBSITE_HOSTNAME;
  return value ? `https://${value}` : undefined;
}

export function getSiteUrlForSwa(): string | undefined {
  /*
   * Having looked at the available ENV variables when deployed to both production and preview environments,
   * only the WEBSITE_AUTH_V2_CONFIG_JSON has values we can use for this.
   *
   * Sample value for production:
   * {\"platform\":{\"enabled\":true},\"globalValidation\":{\"excludedPaths\":[\"/.swa/health.html\"]},\"identityProviders\":{\"azureStaticWebApps\":{\"registration\":{\"clientId\":\"black-bush-020715303.5.azurestaticapps.net\"}}},\"legacyProperties\":{\"configVersion\":\"v2\",\"legacyVersion\":\"V2\"}}
   *
   * Sample value for preview environment (named 331):
   * {\"platform\":{\"enabled\":true},\"globalValidation\":{\"excludedPaths\":[\"/.swa/health.html\"]},\"identityProviders\":{\"azureStaticWebApps\":{\"registration\":{\"clientId\":\"black-bush-020715303-331.westeurope.5.azurestaticapps.net\"}}},\"legacyProperties\":{\"configVersion\":\"v2\",\"legacyVersion\":\"V2\"}}
   *
   * The part we are interested in is the clientId value. We can extract this value and use it as the domain to form the siteUrl.
   */

  const config = process.env.WEBSITE_AUTH_V2_CONFIG_JSON;
  const clientIdMatch = config?.match(/"clientId":"([^"]+)"/);
  return clientIdMatch ? `https://${clientIdMatch[1]}` : undefined;
}
