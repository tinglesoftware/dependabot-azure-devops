import { defineConfig } from 'tsup';

export default defineConfig({
  format: ['esm', 'cjs'],
  target: 'node22',
  platform: 'node',
  splitting: false,
  clean: true,
  dts: true,
  sourcemap: true,
  noExternal: ['semver'], // tsup default for libraries: mark all node_modules as external
  entry: {
    azure: 'src/azure/index.ts',
    dependabot: 'src/dependabot/index.ts',
    environment: 'src/environment/index.ts',
    github: 'src/github/index.ts',
    logger: 'src/logger.ts',
    cli: 'src/cli/index.ts',
  },
  outDir: 'dist',
});
