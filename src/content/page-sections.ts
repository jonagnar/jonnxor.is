import { z } from 'astro/zod';

// Per-page `sections` shapes for the pages collection. content.config.ts
// validates against these at build time; pages import the inferred types so
// the cast and the validation can't drift.
export const countdownsSections = z.object({
  countdown_kicker: z.string(),
  countup_kicker: z.string(),
  methodology: z.string(),
}).strict();
export type CountdownsSections = z.infer<typeof countdownsSections>;

/** slug -> sections schema; content.config.ts validates any pages entry whose slug appears here. */
export const sectionSchemas = { countdowns: countdownsSections } as const;
