# Price Configurator Back Office — Design Spec

**Date:** 2026-05-21  
**Status:** Approved (brainstorming)  
**Full strategy (PDF/Markdown):** [`docs/price-configurator-back-office-strategy.md`](../price-configurator-back-office-strategy.md)

This file is the design-spec entry point. Detailed phases, architecture, and decisions live in the strategy document above.

## Summary

- New service **price-configurator-back-office** (ASP.NET 8 + CmsToolkit + Cosmos), modeled on kitchen-quiz-backoffice.
- **price-configurator** (`C:\Niteco-Project\Nobia\price-configurator`) loads published config via `GET /api/config/v1`; calculation stays in `utils.js`.
- **Draft + publish + scheduled publish**; preview in P4.
- Rollout: P0 foundation → P1 Magnet → P2 all brands → P3 CMS copy → P4 preview.

## Agent skill

`.cursor/skills/price-configurator-back-office/SKILL.md`

## Data handling

See `docs/DATA.md` for seed regeneration from the frontend repo.
