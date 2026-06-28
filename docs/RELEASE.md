# Release checklist — jonnxor.is

Deploys are git-driven: `origin` (Forgejo) push-mirrors to GitHub, which Vercel builds.
Two tracks: **`preview`** → `preview.jonnxor.is` (staging) and **`main`** → `jonnxor.is` (production).

## Cut a release (preview → production)
1. **Work on `preview`.** Tree clean, build green: `mise exec -- pnpm run build`.
2. **Push preview:** `git push origin preview` → verify `https://preview.jonnxor.is`.
3. **Update `docs/CHANGELOG.md`** — move `[Unreleased]` items under a new dated version.
4. **Fast-forward main:** `git switch main && git merge --ff-only preview`.
5. **Push production:** `git push origin main`. Forgejo mirrors → GitHub → Vercel builds prod.
   (Vercel needs `ENABLE_EXPERIMENTAL_COREPACK=1` so it uses the pinned pnpm 10.)
6. **Verify production:** `https://jonnxor.is` loads; `www` → 308 → apex.
7. **Tag (optional):** `git tag vX.Y.Z && git push origin vX.Y.Z`.

## Rollback
- Revert the merge commit on `main` and push, or redeploy a previous Vercel deployment from the dashboard.

> Never `git push github …` by hand — the Forgejo push-mirror is the only path to production.
