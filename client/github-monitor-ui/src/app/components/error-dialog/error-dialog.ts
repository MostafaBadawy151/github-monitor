import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ButtonModule } from '@progress/kendo-angular-buttons';
import { DialogModule } from '@progress/kendo-angular-dialog';
import { SVGIconModule } from '@progress/kendo-angular-icons';
import { exclamationCircleIcon } from '@progress/kendo-svg-icons';
import { ErrorDialogService } from '../../services/error-dialog.service';

/** Global error dialog (Kendo Dialog), driven by ErrorDialogService. Rendered once in the app shell. */
@Component({
  selector: 'app-error-dialog',
  imports: [DialogModule, ButtonModule, SVGIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dialog.current(); as error) {
      <kendo-dialog
        [title]="error.title"
        [width]="480"
        [maxWidth]="'92vw'"
        (close)="dialog.dismiss()"
      >
        <div class="error-body" role="alert">
          <kendo-svgicon [icon]="errorIcon" size="xlarge" themeColor="error" />
          <p>{{ error.message }}</p>
        </div>
        @if (dialog.pendingCount() > 0) {
          <p class="more">+{{ dialog.pendingCount() }} more error(s)</p>
        }
        <kendo-dialog-actions layout="end">
          @if (error.retry) {
            <button kendoButton (click)="dialog.retry()">Retry</button>
          }
          <button kendoButton themeColor="primary" (click)="dialog.dismiss()">OK</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .error-body {
      display: flex;
      gap: 12px;
      align-items: flex-start;
    }
    .error-body p {
      margin: 0;
      line-height: 1.5;
      overflow-wrap: anywhere;
    }
    .more {
      margin: 12px 0 0;
      font-size: 0.85rem;
      opacity: 0.7;
    }
  `,
})
export class ErrorDialogComponent {
  protected readonly dialog = inject(ErrorDialogService);
  protected readonly errorIcon = exclamationCircleIcon;
}
