import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from '@progress/kendo-angular-buttons';
import { SVGIconModule } from '@progress/kendo-angular-icons';
import { arrowRotateCwIcon, clockIcon, cloudIcon, warningTriangleIcon } from '@progress/kendo-svg-icons';
import { Observable, finalize, interval } from 'rxjs';
import { CommitsGridComponent } from '../../components/commits-grid/commits-grid';
import { CommitList } from '../../models/commit.models';
import { ApiError } from '../../services/api-error';
import { ErrorDialogService } from '../../services/error-dialog.service';
import { GithubMonitorApi } from '../../services/github-monitor-api';

/** How often the page re-reads the Quartz cache. The job itself runs every 60s. */
const SCHEDULED_POLL_MS = 30_000;

@Component({
  selector: 'app-github-monitor-page',
  imports: [CommitsGridComponent, ButtonModule, SVGIconModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './github-monitor-page.html',
  styleUrl: './github-monitor-page.scss',
})
export class GithubMonitorPageComponent implements OnInit {
  private readonly api = inject(GithubMonitorApi);
  private readonly errors = inject(ErrorDialogService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly icons = { refresh: arrowRotateCwIcon, clock: clockIcon, cloud: cloudIcon, warning: warningTriangleIcon };

  // --- Live section state ---
  protected readonly live = signal<CommitList | null>(null);
  protected readonly liveLoading = signal(false);

  // --- Scheduled section state ---
  protected readonly scheduled = signal<CommitList | null>(null);
  protected readonly scheduledLoading = signal(false);
  /** Inline error for background polling failures (manual refreshes use the dialog). */
  protected readonly scheduledPollError = signal<string | null>(null);

  /** Ticks every second so "x seconds ago" labels stay current. */
  protected readonly now = signal(Date.now());

  protected readonly lastSyncAgo = computed(() => relative(this.scheduled()?.lastSyncTime ?? null, this.now()));
  protected readonly liveFetchedAgo = computed(() => relative(this.live()?.lastSyncTime ?? null, this.now()));

  ngOnInit(): void {
    this.refreshLive();
    this.refreshScheduled('manual');

    interval(SCHEDULED_POLL_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshScheduled('poll'));

    interval(1_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.now.set(Date.now()));
  }

  /** "Refresh Live Data": fetches in real time through ASP.NET Core, then WCF, then GitHub. */
  protected refreshLive(): void {
    if (this.liveLoading()) return;
    this.liveLoading.set(true);

    this.load(this.api.getLiveCommits(), this.liveLoading).subscribe({
      next: (result) => this.live.set(result),
      error: (error: ApiError) =>
        this.errors.show({
          title: 'Could not load live commits',
          message: error.message,
          retry: () => this.refreshLive(),
        }),
    });
  }

  protected refreshScheduled(mode: 'manual' | 'poll'): void {
    if (this.scheduledLoading()) return;
    this.scheduledLoading.set(true);

    this.load(this.api.getScheduledCommits(), this.scheduledLoading).subscribe({
      next: (result) => {
        this.scheduled.set(result);
        this.scheduledPollError.set(null);
      },
      error: (error: ApiError) => {
        if (mode === 'poll') {
          // Do not pop a dialog every 30 seconds; show the problem inline and keep the last data.
          this.scheduledPollError.set(error.message);
        } else {
          this.errors.show({
            title: 'Could not load scheduled commits',
            message: error.message,
            retry: () => this.refreshScheduled('manual'),
          });
        }
      },
    });
  }

  private load<T>(source: Observable<T>, loading: ReturnType<typeof signal<boolean>>): Observable<T> {
    return source.pipe(
      finalize(() => loading.set(false)),
      takeUntilDestroyed(this.destroyRef),
    );
  }
}

function relative(date: Date | null, now: number): string {
  if (!date) return '';
  const seconds = Math.max(0, Math.round((now - date.getTime()) / 1000));
  if (seconds < 5) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  return hours < 24 ? `${hours} h ago` : `${Math.floor(hours / 24)} d ago`;
}
