import { fileURLToPath } from 'url';
import { configDefaults, defineConfig } from 'vitest/config';

/** @type {import("vitest/config").ViteUserConfig} */
export default defineConfig({
  test: {
    globals: true,
    watch: false,
    exclude: [...configDefaults.exclude],
    alias: {
      '~/': fileURLToPath(new URL('./src/', import.meta.url)),
    },
  },
});
