import { HttpErrorResponse } from '@angular/common/http';

/** A field has an entry only when the server reported a problem with it, so lookups can be undefined. */
export type FieldErrors = Record<string, string[] | undefined>;

/** Turns any failed HTTP call into a sentence a person can act on. */
export function messageOf(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    if (e.status === 0) return "Can't reach the server. Check that the API is running and try again.";
    if (e.status === 429) return 'Too many attempts. Please wait a minute and try again.';
    const title = (e.error as { title?: string } | null)?.title;
    if (title) return title;
    if (e.status >= 500) return 'Something went wrong on our side. Please try again.';
  }
  return 'Something went wrong. Please try again.';
}

/** Per-field messages from the API's validation response, keyed by lower-camel field name. */
export function fieldErrorsOf(e: unknown): FieldErrors {
  return e instanceof HttpErrorResponse ? ((e.error as { errors?: FieldErrors } | null)?.errors ?? {}) : {};
}
