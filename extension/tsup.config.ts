import { defineConfig } from 'tsup';

export default defineConfig({
  entry: [
    'src/task-v1.ts',
    'src/task-v2.ts',
    // add other independent scripts here
  ],
  outDir: 'dist',
  format: 'cjs',
  target: 'node20',
  platform: 'node',
  splitting: false,
  clean: true,
  dts: false,
  sourcemap: true,
  noExternal: [/./], // ⬅️ bundle everything
});
