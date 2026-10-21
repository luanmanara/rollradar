import { Pipe, PipeTransform } from '@angular/core';

/** Formats seconds as m:ss or h:mm:ss, like the YouTube player. */
@Pipe({ name: 'formatTime' })
export class FormatTimePipe implements PipeTransform {
  transform(totalSeconds: number): string {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = String(totalSeconds % 60).padStart(2, '0');
    return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`;
  }
}
