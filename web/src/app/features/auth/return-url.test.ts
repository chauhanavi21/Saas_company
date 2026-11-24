// Run with: npm test   (Node's built-in test runner; no extra dependencies)
import test from 'node:test';
import assert from 'node:assert/strict';
import { safeReturnUrl, DEFAULT_AFTER_LOGIN } from './return-url.ts';

test('a missing value falls back to the dashboard instead of throwing', () => {
  // Angular binds `undefined` when the route has no returnUrl, e.g. after signing out inside the app.
  assert.equal(safeReturnUrl(undefined), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl(null), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl(''), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl(42), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl({}), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl(['/projects']), DEFAULT_AFTER_LOGIN);
});

test('in-app paths are followed, including nested paths and query strings', () => {
  for (const ok of ['/dashboard', '/projects', '/projects/3f1c', '/projects/3f1c?tab=board', '/team#members', '/settings'])
    assert.equal(safeReturnUrl(ok), ok);
});

test('absolute and protocol-relative URLs are refused', () => {
  for (const bad of ['https://evil.example', 'http://evil.example/path', '//evil.example', '//evil.example/path',
                     'javascript:alert(1)', 'data:text/html,hi', 'evil.example', 'projects', ' /projects'])
    assert.equal(safeReturnUrl(bad), DEFAULT_AFTER_LOGIN, bad);
});

test('backslash and control-character tricks that browsers normalise to "//host" are refused', () => {
  for (const bad of ['/\\evil.example', '/\\/evil.example', '/\t/evil.example', '/\n/evil.example', '/\r/evil.example',
                     '/\u0000/evil.example', '/projects\n', '/pro\tjects'])
    assert.equal(safeReturnUrl(bad), DEFAULT_AFTER_LOGIN, JSON.stringify(bad));
});

test('the original check let the control-character and backslash forms through', () => {
  // The previous implementation, kept here to show what changed. Angular's router treats these as
  // in-app paths, so this is extra caution rather than a demonstrated exploit.
  const original = (url: string) => (url.startsWith('/') && !url.startsWith('//') ? url : '/dashboard');
  assert.equal(original('/\\evil.example'), '/\\evil.example');
  assert.equal(original('/\t/evil.example'), '/\t/evil.example');
  assert.equal(safeReturnUrl('/\\evil.example'), DEFAULT_AFTER_LOGIN);
  assert.equal(safeReturnUrl('/\t/evil.example'), DEFAULT_AFTER_LOGIN);
});

test('the original implementation threw on undefined, which is the sign-in bug', () => {
  const original = (url: any) => (url.startsWith('/') && !url.startsWith('//') ? url : '/dashboard');
  assert.throws(() => original(undefined), TypeError);
  assert.doesNotThrow(() => safeReturnUrl(undefined));
});
