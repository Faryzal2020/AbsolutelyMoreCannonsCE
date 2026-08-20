import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/** The built bundle is served statically by Fastify from ./public. */
export default defineConfig({
  root: 'ui',
  plugins: [react()],
  build: {
    outDir: '../public',
    emptyOutDir: true,
    sourcemap: false,
  },
  server: {
    proxy: { '/api': 'http://localhost:3000' },
  },
});
