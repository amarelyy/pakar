# PAKAR infrastructure

Web, API and AI run on **one Azure virtual machine** with Docker Compose; the database is on Neon.

| Folder or file | Use |
| --- | --- |
| [`vm/`](vm/README.md) | The deployment: compose file, web server settings, scripts and the step-by-step guide |
| `docker-compose.yml`, `.env.example` | PostgreSQL container for development |
| `bicep/` | Not used: templates of an earlier Azure design ([notes](../docs/archive/infra-bicep.md)) |
| `vercel/` | Earlier setup, kept while that site is online |
