import { createMDX } from 'fumadocs-mdx/next';
import { type NextConfig } from 'next';

const config: NextConfig = {
  output: 'standalone',
  reactStrictMode: true,
  logging: { fetches: { fullUrl: true } }, // allows us to see cache behavior for fetches
  images: {
    formats: ['image/avif', 'image/webp'],
    unoptimized: true, // hoping this improves site performance
  },
  async headers() {
    return [
      // security headers
      {
        source: '/(.*)', // applies to all routes
        headers: [
          { key: 'Strict-Transport-Security', value: 'max-age=31536000; includeSubDomains; preload' }, // 1 year
          {
            key: 'Content-Security-Policy',
            value: `
              default-src 'self';
              img-src 'self' data: https:;
              script-src 'self' 'unsafe-inline' https://js.monitor.azure.com https://*.applicationinsights.azure.com https://*.vercel-scripts.com https://vercel.live;
              style-src 'self' 'unsafe-inline';
              font-src 'self';
              connect-src 'self' https://js.monitor.azure.com https://*.applicationinsights.azure.com https://*.vercel-scripts.com;
              frame-src https://vercel.live;
              frame-ancestors 'none';
              object-src 'none';
              `
              .replace(/\n/g, ' ')
              .trim(),
          },
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'Referrer-Policy', value: 'no-referrer' },
          { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
        ],
      },
    ];
  },
};

const withMDX = createMDX();
export default withMDX(config);
