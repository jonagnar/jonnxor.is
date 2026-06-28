# Changelog

All notable changes to jonnxor.is are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
deploys flow `preview` → `main` → Vercel.

## [Unreleased]

### Added
- `docs/` scaffold (this changelog + `RELEASE.md`).

### In progress
- i18n (is / en / ja) — Directus-backed blog & grimoire slices (planning in `~/dev/resources/jonnxor.is/`).
- Astro-native chrome (nav/footer as components, self-hosted fonts).

## [0.1.0] — 2026-06-21
### Added
- Astro 6 rebuild: all 11 pages ported onto a shared `Base.astro` layout.
- Live deploy pipeline: `git push` → Forgejo → GitHub mirror → Vercel
  (`jonnxor.is` production, `preview.jonnxor.is` staging).
