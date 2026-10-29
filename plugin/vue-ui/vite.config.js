import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig({
  base: './',
  plugins: [
    vue(), 
    tailwindcss(),
  ],
  server:{
    port: 5003,
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../ui/dist'),
    emptyOutDir: true,
    rollupOptions: {
      external: ['monaco-editor'],
    },
  },
  optimizeDeps: {
    exclude: ['monaco-editor'], 
  },
})
