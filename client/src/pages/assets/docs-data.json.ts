import type { APIRoute } from "astro";
import { getCollection } from "astro:content";

// Static JSON of the grimoire for the site-wide search. site.js lazy-fetches this
// on any page that doesn't already inject window.JX_DOCS (i.e. everything but /docs).
// Single source of truth: the grimoire collection.
export const GET: APIRoute = async () => {
  const docs = (await getCollection("grimoire"))
    .sort((a, b) => a.data.order - b.data.order)
    .map((e) => ({ id: e.id, ...e.data }));
  return new Response(JSON.stringify(docs), {
    headers: { "Content-Type": "application/json" },
  });
};
