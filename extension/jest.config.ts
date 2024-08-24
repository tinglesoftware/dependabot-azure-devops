import type { Config } from '@jest/types';

// Sync object
const config: Config.InitialOptions = {
  verbose: true,
  // transform: {
  //   "^.+\\.test.tsx?$": "ts-jest",
  // },
  testEnvironment: 'node',
  preset: 'ts-jest',
  rootDir: './tests',
};

export default config;
