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

export const homeSections = z.object({
  hero: z.object({ tagline: z.string(), cta_primary: z.string(), cta_secondary: z.string() }).strict(),
  paths: z.array(z.object({ rune: z.string(), title: z.string(), copy: z.string(), link_label: z.string() }).strict()).length(3),
  now_kicker: z.string(),
  now: z.array(z.object({ label: z.string(), value: z.string(), sub: z.string() }).strict()).length(3),
  latest_heading: z.string(),
}).strict();
export type HomeSections = z.infer<typeof homeSections>;

/** slug -> sections schema; content.config.ts validates any pages entry whose slug appears here. */
export const sectionSchemas = { countdowns: countdownsSections, home: homeSections } as const;
