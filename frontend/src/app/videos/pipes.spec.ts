import { FormatTimePipe } from './format-time.pipe';
import { HighlightPipe } from './highlight.pipe';

describe('FormatTimePipe', () => {
  const pipe = new FormatTimePipe();

  it('formats like the YouTube player', () => {
    expect(pipe.transform(5)).toBe('0:05');
    expect(pipe.transform(66)).toBe('1:06');
    expect(pipe.transform(7234)).toBe('2:00:34');
  });
});

describe('HighlightPipe', () => {
  const pipe = new HighlightPipe();

  it('flags every occurrence of the term ignoring case', () => {
    expect(pipe.transform('the Omoplata, an omoplata', 'omoplata')).toEqual([
      { text: 'the ', match: false },
      { text: 'Omoplata', match: true },
      { text: ', an ', match: false },
      { text: 'omoplata', match: true }
    ]);
  });
});
