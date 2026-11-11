import { HttpClient } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, catchError, map, throwError, timeout } from 'rxjs';
import { ApiResponse, CommitList, CommitListDto } from '../models/commit.models';
import { ApiError, toApiError } from './api-error';

/** Base URL of the GitHub endpoints. Relative by default; the dev server proxies /api to the ASP.NET Core API. */
export const GITHUB_API_BASE_URL = new InjectionToken<string>('GITHUB_API_BASE_URL', {
  providedIn: 'root',
  factory: () => '/api/github',
});

/** Client-side ceiling, slightly above the API's own WCF timeout (30s). */
const REQUEST_TIMEOUT_MS = 40_000;

@Injectable({ providedIn: 'root' })
export class GithubMonitorApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(GITHUB_API_BASE_URL);

  /** GET /api/github/live: real-time commits (ASP.NET Core, then WCF GetLiveCommits, then GitHub). */
  getLiveCommits(): Observable<CommitList> {
    return this.get('live');
  }

  /** GET /api/github/scheduled: commits cached by the Quartz job (WCF GetScheduledCommits). */
  getScheduledCommits(): Observable<CommitList> {
    return this.get('scheduled');
  }

  private get(path: 'live' | 'scheduled'): Observable<CommitList> {
    return this.http.get<ApiResponse<CommitListDto>>(`${this.baseUrl}/${path}`).pipe(
      timeout(REQUEST_TIMEOUT_MS),
      map((response) => {
        if (!response.success || !response.data) {
          throw new ApiError(response.message || 'The server reported a failure.');
        }
        return toCommitList(response.data, response.message);
      }),
      catchError((error) => throwError(() => toApiError(error))),
    );
  }
}

function toCommitList(dto: CommitListDto, message: string): CommitList {
  return {
    source: dto.source,
    isStale: dto.isStale,
    message,
    lastSyncTime: dto.lastSyncTime ? new Date(dto.lastSyncTime) : null,
    lastAttemptTime: dto.lastAttemptTime ? new Date(dto.lastAttemptTime) : null,
    commits: dto.commits.map((c) => ({ ...c, date: new Date(c.date) })),
  };
}
