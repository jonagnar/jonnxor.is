import YAML, { Scalar } from 'yaml';

/** Parse a per-locale grimoire YAML file string into a plain record object. */
export function parseDoc(raw) {
  return YAML.parse(raw);
}

/**
 * Serialize a grimoire record into a per-locale YAML file string. Keys are
 * written in a fixed order for stable diffs; `body` (rich HTML) is forced to a
 * literal block scalar and line-wrapping is disabled so long HTML lines aren't
 * folded. `game` is omitted when not set.
 */
export function serializeDoc(r) {
  const obj = {
    slug: r.slug,
    locale: r.locale,
    order: r.order,
    realm: r.realm,
    ...(r.game ? { game: r.game } : {}),
    cat: r.cat,
    title: r.title,
    tags: r.tags ?? [],
    updated: r.updated,
    body: r.body ?? '',
  };
  const doc = new YAML.Document(obj);
  const bodyNode = doc.get('body', true);
  if (bodyNode) bodyNode.type = Scalar.BLOCK_LITERAL;
  return doc.toString({ lineWidth: 0 });
}
