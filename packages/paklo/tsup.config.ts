import { defineConfig } from 'tsup';

export default defineConfig({
  format: ['esm', 'cjs'],
  target: 'node22',
  platform: 'node',
  splitting: false,
  clean: true,
  dts: true,
  sourcemap: true,
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
