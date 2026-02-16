import { Component, Input, Output, EventEmitter } from '@angular/core';

export interface FilterChip {
  id: string;
  label: string;
  value: string;
  type?: string;
}

@Component({
  selector: 'app-filter-chips',
  templateUrl: './filter-chips.component.html',
  styleUrls: ['./filter-chips.component.css']
})
export class FilterChipsComponent {
  @Input() filters: FilterChip[] = [];
  @Output() removeFilter = new EventEmitter<string>();
  @Output() clearAll = new EventEmitter<void>();

  onRemoveFilter(filterId: string): void {
    this.removeFilter.emit(filterId);
  }

  onClearAll(): void {
    this.clearAll.emit();
  }
}
