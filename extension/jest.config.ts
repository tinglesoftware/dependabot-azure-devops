import type { Config } from '@jest/types';

// Sync object
const config: Config.InitialOptions = {
  verbose: true,
  // transform: {
  //   "^.+\\.test.tsx?$": "ts-jest",
  // },
  testEnvironment: 'node',
  preset: 'ts-jest'
};

export default config;
