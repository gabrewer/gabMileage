import { defineConfig } from 'vite'
import { tanstackStart } from '@tanstack/react-start/plugin/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import viteReact from '@vitejs/plugin-react'

/**
 * Breadcrumbs (backend-builder)
 * What: TanStack Start + TanStack Router file-based routing via Vite
 * Why: Matches PRD/AC; router plugin generates routeTree.gen.ts from src/routes
 * Alternatives considered: legacy Vinxi config (rejected; Start has moved to Vite-first config)
 * Confidence: medium
 */

export default defineConfig(() => {
  const isVitest = process.env.VITEST === 'true' || process.env.VITEST === '1'

  return {
    server: {
      port: Number.parseInt(process.env.PORT ?? '3000', 10),
    },
    plugins: [
      // TanStack Start + Router transforms are required for dev/build,
      // but are disabled for Vitest unit tests.
      ...(isVitest
        ? []
        : [
            tanstackStart(),
            tanstackRouter({
              target: 'react',
              autoCodeSplitting: true,
            }),
          ]),

      viteReact(),

      // Aspire health checks expect GET /health to return 200.
      {
        name: 'gabmileage-health',
        configureServer(server) {
          server.middlewares.use('/health', (_req, res) => {
            res.statusCode = 200
            res.setHeader('content-type', 'text/plain')
            res.end('OK')
          })
        },
        configurePreviewServer(server) {
          server.middlewares.use('/health', (_req, res) => {
            res.statusCode = 200
            res.setHeader('content-type', 'text/plain')
            res.end('OK')
          })
        },
      },
    ],
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
    },
  }
})
