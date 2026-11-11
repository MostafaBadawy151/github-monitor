import { TestBed } from '@angular/core/testing';
import { ErrorDialogService } from './error-dialog.service';

describe('ErrorDialogService', () => {
  let service: ErrorDialogService;

  beforeEach(() => {
    service = TestBed.inject(ErrorDialogService);
  });

  it('shows errors one at a time in order', () => {
    service.show({ title: 'A', message: '1' });
    service.show({ title: 'B', message: '2' });

    expect(service.current()?.title).toBe('A');
    expect(service.pendingCount()).toBe(1);

    service.dismiss();
    expect(service.current()?.title).toBe('B');

    service.dismiss();
    expect(service.current()).toBeNull();
  });

  it('de-duplicates identical pending errors', () => {
    service.show({ title: 'A', message: 'API down' });
    service.show({ title: 'A', message: 'API down' });
    expect(service.pendingCount()).toBe(0);
  });

  it('retry dismisses and invokes the callback', () => {
    const retry = jasmine.createSpy('retry');
    service.show({ title: 'A', message: '1', retry });

    service.retry();

    expect(retry).toHaveBeenCalledTimes(1);
    expect(service.current()).toBeNull();
  });
});
