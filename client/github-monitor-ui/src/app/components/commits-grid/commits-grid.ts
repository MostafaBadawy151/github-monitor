import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ChipModule } from '@progress/kendo-angular-buttons';
import { GridModule } from '@progress/kendo-angular-grid';
import { SortDescriptor } from '@progress/kendo-data-query';
import { Commit } from '../../models/commit.models';

/**
 * Kendo Grid with the required columns: Commit Message, Author, Date, Commit SHA, Source.
 * Responsive: under 768px the five columns collapse into one stacked "card" column
 * through Kendo's column `media` queries.
 */
@Component({
  selector: 'app-commits-grid',
  imports: [GridModule, ChipModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './commits-grid.html',
  styleUrl: './commits-grid.scss',
})
export class CommitsGridComponent {
  readonly commits = input.required<Commit[]>();
  readonly loading = input(false);
  readonly emptyText = input('No commits to display.');

  protected readonly sort: SortDescriptor[] = [{ field: 'date', dir: 'desc' }];
  protected readonly wide = '(min-width: 768px)';
  protected readonly narrow = '(max-width: 767.98px)';
}
