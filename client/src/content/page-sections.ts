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

export const aboutSections = z.object({
  body: z.array(z.string()).min(1),
  sheet_title: z.string(),
  sheet: z.array(z.object({ k: z.string(), v: z.string() }).strict()).length(8),
  skills: z.array(z.object({ label: z.string(), gold: z.boolean() }).strict()).min(1),
  letter: z.object({
    kicker: z.string(),
    who: z.string(),
    when: z.string(),
    body: z.array(z.string()).min(1),
    sign_name: z.string(),
    sign_small: z.string(),
    cta_label: z.string(),
  }).strict(),
}).strict();
export type AboutSections = z.infer<typeof aboutSections>;

export const cvSections = z.object({
  role: z.string(),
  contact: z.object({ location: z.string(), web: z.string(), github: z.string() }).strict(),
  profile_heading: z.string(),
  profile: z.string(),
  experience_heading: z.string(),
  experience: z.array(z.object({
    when: z.string(),
    title: z.string(),
    org: z.string(),
    bullets: z.array(z.string()).min(1),
  }).strict()).min(1),
  skills_heading: z.string(),
  skills: z.array(z.object({ k: z.string(), v: z.string() }).strict()).min(1),
  education_heading: z.string(),
  education: z.array(z.object({ when: z.string(), degree: z.string(), school: z.string() }).strict()).min(1),
  languages: z.string(),
}).strict();
export type CvSections = z.infer<typeof cvSections>;

/** slug -> sections schema; content.config.ts validates any pages entry whose slug appears here. */
export const sectionSchemas = {
  countdowns: countdownsSections,
  home: homeSections,
  about: aboutSections,
  cv: cvSections,
} as const;
