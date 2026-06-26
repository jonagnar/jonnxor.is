/**
 * Derive a collection id from a per-locale content filename, keeping the locale
 * suffix so a slug's locale files stay distinct entries. Astro's glob loader
 * otherwise derives the id from the frontmatter `slug` (blog) or the bare
 * filename (grimoire), which is identical across `<slug>.en` / `<slug>.is` —
 * they'd collide on one id and silently overwrite each other (the bug that hit
 * Phase 2: the en base vanished the instant a second locale file appeared).
 */
export function localeEntryId({ entry }: { entry: string }): string {
  return entry.replace(/\.(md|yaml)$/, '');
}
