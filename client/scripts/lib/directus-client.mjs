import { createDirectus, rest, authentication } from '@directus/sdk';

/**
 * A logged-in Directus SDK client for the local stack. Uses the object-payload
 * login required by @directus/sdk@22 (positional throws a TypeError).
 */
export async function connect() {
  const client = createDirectus(process.env.DIRECTUS_URL).with(rest()).with(authentication());
  await client.login({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD });
  return client;
}

/**
 * Exit the process explicitly. The authenticated client schedules a token-refresh
 * timer that keeps Node's event loop alive, which would otherwise hang the script
 * after its work is done (and break `&&` chaining).
 */
export function done(code = 0) {
  process.exit(code);
}
