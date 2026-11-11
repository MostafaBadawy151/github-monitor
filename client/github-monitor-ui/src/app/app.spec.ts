import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    }).compileComponents();
  });

  it('renders the "GitHub Repository Monitor" page with both sections', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    // Both sections load on startup; answer with empty lists.
    const http = TestBed.inject(HttpTestingController);
    const empty = (source: string) => ({
      success: true,
      message: 'ok',
      data: { source, count: 0, lastSyncTime: null, lastAttemptTime: null, isStale: false, commits: [] },
    });
    http.expectOne('/api/github/live').flush(empty('Live'));
    http.expectOne('/api/github/scheduled').flush(empty('Scheduled'));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent).toContain('GitHub Repository Monitor');
    expect(el.textContent).toContain('Live Commits');
    expect(el.textContent).toContain('Scheduled Commits');
    expect(el.textContent).toContain('Refresh Live Data');
    expect(el.textContent).toContain('LastSyncTime');
    expect(el.querySelectorAll('kendo-grid').length).toBe(2);
  });
});
