export type Platform =
  | 'azure_app_service'
  | 'azure_container_apps'
  | 'azure_static_web_app'
  | 'cloudflare_pages'
  | 'vercel'
  | undefined;

export function getPlatform(): Platform {
  if (process.env.CONTAINER_APP_ENV_DNS_SUFFIX) return 'azure_container_apps';
  // SWA is a special case of Azure App Service so we need to check it first
  else if (process.env.WEBSITE_STATICWEBAPP_RESOURCE_ID) return 'azure_static_web_app';
  else if (process.env.WEBSITE_HOSTNAME) return 'azure_app_service';
  else if (process.env.CF_PAGES_URL) return 'cloudflare_pages';
  else if (process.env.VERCEL_BRANCH_URL) return 'vercel';

  return undefined;
}
