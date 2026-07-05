import matter from 'gray-matter';

/** Parse a Markdown file string into { data, body }. */
export function parsePost(raw) {
  const { data, content } = matter(raw);
  return { data, body: content };
}

/** Serialize a post record into a per-locale Markdown file string. */
export function serializePost(p) {
  const fm = matter.stringify(p.body ?? '', {
    title: p.title,
    date: p.date,
    category: p.category,
    excerpt: p.excerpt,
    readTime: p.readTime,
    draft: p.draft ?? false,
    locale: p.locale,
    slug: p.slug,
  });
  return fm;
}
