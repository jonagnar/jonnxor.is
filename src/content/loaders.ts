/**
 * Derive a collection id from a per-locale content filename, keeping the locale
 * suffix so a slug's locale files stay distinct entries. Astro's glob loader
 * otherwise derives the id from the frontmatter `slug` (blog) or the bare
 * filename (grimoire), which is identical across `<slug>.en` / `<slug>.is` —
 * they'd collide on one id and silently overwrite each other (the bug that hit
 * Phase 2: the en base vanished the instant a second locale file appeared).
 *
 * Receives Astro's generateId options ({ entry, base, data }); only `entry`
 * (the path relative to the collection base, e.g. `posts/foo.en.md`) is needed.
 * `GlobOptions['generateId']` is not publicly exported from `astro/loaders`,
 * so the signature is documented inline rather than typed via the Astro type.
 */
export function localeEntryId({ entry }: { entry: string; base?: URL; data?: Record<string, unknown> }): string {
  return entry.replace(/\.(md|yaml)$/, '');
}
