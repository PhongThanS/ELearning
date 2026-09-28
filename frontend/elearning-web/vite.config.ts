/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Dev: proxy /api về backend để frontend và API cùng origin (cookie SameSite=Strict, không cần CORS — D-15).
const apiTarget = process.env.VITE_API_TARGET ?? "http://localhost:5136";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": { target: apiTarget, changeOrigin: false },
      "/health": { target: apiTarget, changeOrigin: false },
    },
  },
  build: {
    // Chunk chính chủ yếu là thư viện dùng chung (React, Bootstrap, TanStack, i18next); các trang đã tách theo route.
    chunkSizeWarningLimit: 800,
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
