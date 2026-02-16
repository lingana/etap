import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './skeleton-loader.component.html',
  styleUrls: ['./skeleton-loader.component.css']
})
export class SkeletonLoaderComponent {
  @Input() type: 'table' | 'card' | 'list' | 'paragraph' = 'table';
  @Input() rowCount: number = 5;
  @Input() columnCount: number = 5;

  getRows(): number[] {
    return Array(this.rowCount).fill(0).map((_, i) => i);
  }

  getColumns(): number[] {
    return Array(this.columnCount).fill(0).map((_, i) => i);
  }
}
