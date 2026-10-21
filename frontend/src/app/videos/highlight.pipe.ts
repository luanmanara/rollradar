import { Pipe, PipeTransform } from '@angular/core';

export interface TextPart {
  text: string;
  match: boolean;
}

/** Splits text into parts, flagging the ones equal to the term (case-insensitive) so the template can bold them. */
@Pipe({ name: 'highlight' })
export class HighlightPipe implements PipeTransform {
  transform(text: string, term: string): TextPart[] {
    if (!term) return [{ text, match: false }];
    const escaped = term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return text
      .split(new RegExp(`(${escaped})`, 'gi'))
      .filter(part => part.length > 0)
      .map(part => ({ text: part, match: part.toLowerCase() === term.toLowerCase() }));
  }
}
