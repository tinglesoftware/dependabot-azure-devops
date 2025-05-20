import { fileURLToPath } from 'url';
import { configDefaults, defineConfig } from 'vitest/config';

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
