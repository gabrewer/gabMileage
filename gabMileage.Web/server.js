import { spawn } from 'node:child_process'

const mode = (process.argv[2] ?? 'dev').toLowerCase()
const port = process.env.PORT ?? '3000'

/**
 * Breadcrumbs (backend-builder)
 * What: Replace placeholder static server with TanStack Start (Vite) launcher
 * Why: PRD/AC require TanStack Start; also keeps PORT honored for Aspire orchestration
 * Alternatives considered: keep a bespoke Node HTTP server (rejected; blocks Start)
 * Confidence: high
 */

function run(command, args) {
  const child = spawn(command, args, {
    stdio: 'inherit',
    shell: true,
    env: process.env,
  })

  child.on('exit', (code) => {
    process.exit(code ?? 0)
  })
}

switch (mode) {
  case 'dev':
    run('vite', ['dev', '--host', '0.0.0.0', '--port', port])
    break

  case 'build':
    run('vite', ['build'])
    break

  case 'start':
  case 'preview':
    run('vite', ['preview', '--host', '0.0.0.0', '--port', port])
    break

  default:
    console.error(`Unknown mode '${mode}'. Use dev|build|start.`)
    process.exit(1)
    break
}
