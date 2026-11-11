/** Unified response envelope returned by every ASP.NET Core endpoint. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export type CommitSource = 'Live' | 'Scheduled';

/** Wire shape of a commit (dates are ISO strings). */
export interface CommitDto {
  sha: string;
  shortSha: string;
  message: string;
  messageHeadline: string;
  author: string;
  authorLogin: string | null;
  date: string;
  url: string | null;
  source: CommitSource;
}

export interface CommitListDto {
  source: CommitSource;
  count: number;
  lastSyncTime: string | null;
  lastAttemptTime: string | null;
  isStale: boolean;
  commits: CommitDto[];
}

/** Client-side commit model (dates parsed, so the Kendo Grid sorts them correctly). */
export interface Commit extends Omit<CommitDto, 'date'> {
  date: Date;
}

export interface CommitList {
  source: CommitSource;
  lastSyncTime: Date | null;
  lastAttemptTime: Date | null;
  isStale: boolean;
  commits: Commit[];
  /** Server-provided message, e.g. a stale-cache warning. */
  message: string;
}
