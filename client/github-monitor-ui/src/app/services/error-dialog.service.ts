import { Injectable, computed, signal } from '@angular/core';

export interface ErrorDialogRequest {
  title: string;
  message: string;
  /** If set, the dialog shows a "Retry" button that calls this. */
  retry?: () => void;
}

/**
 * Queue of errors shown one at a time in a Kendo Dialog (rendered by ErrorDialogComponent).
 * Identical pending messages are de-duplicated, e.g. both sections failing because the API is down.
 */
@Injectable({ providedIn: 'root' })
export class ErrorDialogService {
  private readonly queue = signal<ErrorDialogRequest[]>([]);

  readonly current = computed(() => this.queue()[0] ?? null);
  readonly pendingCount = computed(() => Math.max(0, this.queue().length - 1));

  show(request: ErrorDialogRequest): void {
    this.queue.update((items) =>
      items.some((i) => i.title === request.title && i.message === request.message) ? items : [...items, request],
    );
  }

  dismiss(): void {
    this.queue.update((items) => items.slice(1));
  }

  retry(): void {
    const request = this.current();
    this.dismiss();
    request?.retry?.();
  }
}
