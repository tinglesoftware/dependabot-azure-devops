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
    dependabot: 'src/dependabot/index.ts',
    environment: 'src/environment/index.ts',
    logger: 'src/logger.ts',
  },
  outDir: 'dist',
});
