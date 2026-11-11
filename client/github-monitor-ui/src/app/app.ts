import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ErrorDialogComponent } from './components/error-dialog/error-dialog';
import { GithubMonitorPageComponent } from './pages/github-monitor/github-monitor-page';

@Component({
  selector: 'app-root',
  imports: [GithubMonitorPageComponent, ErrorDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
