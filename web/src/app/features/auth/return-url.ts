/**
 * Where to send someone after they sign in.
 *
 * `returnUrl` comes from a query parameter, so it is untrusted, and it can also be missing: with
 * component input binding, Angular sets an input to `undefined` (not its `''` default) when the route
 * has no such parameter, which is what happens after signing out inside the app. Only in-app paths
 * are followed, so a crafted link cannot send someone to another site after they sign in.
 */
export const DEFAULT_AFTER_LOGIN = '/dashboard';

export function safeReturnUrl(url: unknown): string {
  if (typeof url !== 'string') return DEFAULT_AFTER_LOGIN;
  // One leading slash only: "//host" and "/\host" are treated as protocol-relative by browsers.
  if (!url.startsWith('/') || url.startsWith('//') || url.startsWith('/\\')) return DEFAULT_AFTER_LOGIN;
  // Control characters (tab, newline, ...) are stripped by browsers when they parse a URL, so
  // "/\t/evil.com" would become "//evil.com". Refuse anything containing them.
  if (/[\u0000-\u001f\u007f]/.test(url)) return DEFAULT_AFTER_LOGIN;
  return url;
}
