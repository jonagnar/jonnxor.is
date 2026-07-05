import YAML, { Scalar } from 'yaml';

/**
 * Build a {serialize, parse} codec for one snapshot collection. Mirrors the
 * discipline grimoire-yaml.mjs established: fixed key order for stable diffs,
 * block-literal scalars for long text, line-wrapping disabled, date-like
 * strings double-quoted (Astro's js-yaml loader is YAML 1.1 and would coerce
 * a bare date to a Date object). undefined/null values are always skipped;
 * `omitEmpty` keys are additionally dropped when `false` so optional flags
 * don't pollute every file.
 */
export function makeEntryCodec({ keyOrder, blockKeys = [], quoteKeys = [], omitEmpty = [] }) {
  const collision = blockKeys.find((k) => quoteKeys.includes(k));
  if (collision) throw new Error(`entry-yaml: key "${collision}" cannot be in both blockKeys and quoteKeys`);
  function serialize(r) {
    const obj = {};
    for (const k of keyOrder) {
      const v = r[k];
      if (v === undefined || v === null) continue;
      if (omitEmpty.includes(k) && v === false) continue;
      obj[k] = v;
    }
    const doc = new YAML.Document(obj);
    for (const k of blockKeys) {
      const n = doc.get(k, true);
      if (n) n.type = Scalar.BLOCK_LITERAL;
    }
    for (const k of quoteKeys) {
      const n = doc.get(k, true);
      if (n) n.type = Scalar.QUOTE_DOUBLE;
    }
    return doc.toString({ lineWidth: 0 });
  }
  // yaml's default schema is YAML 1.2 core: bare dates parse as strings, which
  // keeps serialize∘parse∘serialize the identity. Don't change the parse schema.
  return { serialize, parse: (raw) => YAML.parse(raw) };
}
