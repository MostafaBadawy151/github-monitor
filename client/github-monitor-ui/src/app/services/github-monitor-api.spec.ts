import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError } from './api-error';
import { GithubMonitorApi } from './github-monitor-api';

describe('GithubMonitorApi', () => {
  let api: GithubMonitorApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(GithubMonitorApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps a successful envelope and parses dates', () => {
    let result: any;
    api.getScheduledCommits().subscribe((r) => (result = r));

    http.expectOne('/api/github/scheduled').flush({
      success: true,
      message: 'Returned 1 cached commits.',
      data: {
        source: 'Scheduled',
        count: 1,
        lastSyncTime: '2025-06-01T12:00:00+00:00',
        lastAttemptTime: '2025-06-01T12:00:00+00:00',
        isStale: false,
        commits: [
          {
            sha: 'abcdef1234',
            shortSha: 'abcdef1',
            message: 'Hello',
            messageHeadline: 'Hello',
            author: 'Jane',
            authorLogin: 'jane',
            date: '2025-01-01T00:00:00+00:00',
            url: null,
            source: 'Scheduled',
          },
        ],
      },
    });

    expect(result.lastSyncTime).toEqual(new Date('2025-06-01T12:00:00Z'));
    expect(result.commits[0].date instanceof Date).toBeTrue();
    expect(result.commits[0].source).toBe('Scheduled');
  });

  it('surfaces the server message from an error envelope (e.g. GitHub 401)', () => {
    let error: ApiError | undefined;
    api.getLiveCommits().subscribe({ error: (e) => (error = e) });

    http.expectOne('/api/github/live').flush(
      { success: false, message: 'GitHub rejected the credentials (401).', data: null },
      { status: 502, statusText: 'Bad Gateway' },
    );

    expect(error).toEqual(jasmine.any(ApiError));
    expect(error!.message).toBe('GitHub rejected the credentials (401).');
    expect(error!.status).toBe(502);
  });

  it('reports a friendly message when the API is unreachable', () => {
    let error: ApiError | undefined;
    api.getLiveCommits().subscribe({ error: (e) => (error = e) });

    http.expectOne('/api/github/live').error(new ProgressEvent('error'), { status: 0 });

    expect(error!.message).toContain('Cannot reach the API server');
  });

  it('treats success=false with HTTP 200 as an error', () => {
    let error: ApiError | undefined;
    api.getLiveCommits().subscribe({ error: (e) => (error = e) });

    http.expectOne('/api/github/live').flush({ success: false, message: 'Nope', data: null });

    expect(error!.message).toBe('Nope');
  });
});
