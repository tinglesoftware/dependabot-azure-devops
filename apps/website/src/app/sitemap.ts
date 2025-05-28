import { docs, legal } from '@/lib/source';
import siteConfig from '@/site-config';
import type { MetadataRoute } from 'next';

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  type Route = MetadataRoute.Sitemap[number];

  const routesMap = [
    '', // root without trailing slash
    // '/about',
  ].map(
    (route): Route => ({
      url: `${siteConfig.siteUrl}${route}`,
      // lastModified: new Date().toISOString(),
      changeFrequency: 'daily',
      priority: 0.5,
    }),
  );

  // pages for legal docs
  const legalRoutes = await Promise.all(
    legal.getPages().map(async (post): Promise<Route> => {
      return {
        url: new URL(post.url, siteConfig.siteUrl).toString(),
        lastModified: post.data.updated,
        changeFrequency: 'daily',
        priority: 0.5,
      };
    }),
  );

  // page for docs
  const docsRoutes = await Promise.all(
    docs
      .getPages()
      .filter((doc) => siteConfig.showDrafts || !doc.data.draft) // filter out drafts
      .map(async (doc): Promise<Route> => {
        return {
          url: new URL(doc.url, siteConfig.siteUrl).toString(),
          lastModified: doc.data.git.date,
          changeFrequency: 'daily',
          priority: 0.5,
        };
      }),
  );

  const fetchedRoutes: Route[] = [];
  fetchedRoutes.push(...legalRoutes);
  fetchedRoutes.push(...docsRoutes);

  return [...routesMap, ...fetchedRoutes];
}
