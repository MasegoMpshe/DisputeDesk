import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { DisputeTimelineEntry } from './disputes-api';

@Component({
  selector: 'app-dispute-timeline',
  imports: [DatePipe],
  templateUrl: './dispute-timeline.html',
  styleUrl: './dispute-timeline.css',
})
export class DisputeTimeline {
  readonly entries = input.required<DisputeTimelineEntry[]>();
}
