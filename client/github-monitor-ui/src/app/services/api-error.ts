import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';
import { ApiResponse } from '../models/commit.models';

/** Normalized error surfaced to the UI. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Turns any failure (HTTP error, timeout, envelope with success=false) into a readable ApiError. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (error instanceof TimeoutError) {
    return new ApiError('The request timed out. The server took too long to respond.');
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return new ApiError(
        'Cannot reach the API server. Make sure the ASP.NET Core API is running on http://localhost:5200.',
        0,
      );
    }

    // The API always returns { success, message, data }, even for errors.
    const body = error.error as Partial<ApiResponse<unknown>> | null;
    if (body && typeof body === 'object' && typeof body.message === 'string' && body.message) {
      return new ApiError(body.message, error.status);
    }

    const fallback: Record<number, string> = {
      401: 'Authentication failed (401).',
      404: 'The requested API endpoint was not found (404).',
      500: 'The server encountered an unexpected error (500).',
      502: 'An upstream service returned an error (502).',
      503: 'A required service is unavailable (503).',
      504: 'An upstream service timed out (504).',
    };
    return new ApiError(fallback[error.status] ?? `Request failed with status ${error.status}.`, error.status);
  }

  return new ApiError(error instanceof Error ? error.message : 'An unknown error occurred.');
}
