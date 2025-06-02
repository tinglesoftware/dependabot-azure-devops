import { getBranch, getSha } from './git';
import { type Platform, getPlatform } from './platform';
import { getSiteUrlCombined } from './site-url';

export type Environment = {
  /** The current environment. */
  name?: 'development' | 'production' | 'test';

  /** Whether the current environment is development. */
  development: boolean;

  /** Whether the current environment is production. */
  production: boolean;

  /** Whether the current environment is test. */
  test: boolean;

  /** The current platform. */
  platform: Platform;

  /** The current commit SHA. */
  sha?: string;

  /** The current branch name. */
  branch?: string;

  /** Whether the current branch is the main branch. */
  main: boolean;
};

function getEnvironment(): Environment {
  const env = process.env.NODE_ENV as Environment['name'];
  const branch = getBranch();
  const sha = getSha();
  const platform = getPlatform();

  return {
    name: env,
    development: env === 'development',
    production: env === 'production',
    test: env === 'test',
    platform,
    sha,
    branch,
    main: branch === 'main',
  };
}

export const environment = getEnvironment();

export interface SiteUrlOptions {
  /** The default URL to use if no other URL is found. */
  defaultValue: string;
}

export function getSiteUrl({ defaultValue }: SiteUrlOptions): string {
  const { development, main } = environment;
  return getSiteUrlCombined({ development, main, defaultValue: defaultValue });
}
