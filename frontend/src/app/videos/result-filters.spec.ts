import { FightVideo } from './fight-video.model';
import { NO_FILTERS, athletesOf, channelsOf, clipsOf, filterVideos, sortVideos } from './result-filters';

function video(id: string, title: string, channel: string, uploadDate: string | null, moments = 1): FightVideo {
  return {
    id, title, channel, uploadDate, url: '', thumbnailUrl: '',
    moments: Array.from({ length: moments }, (_, i) => ({ id: `${id}.${i}`, technique: 'armbar', startSeconds: 10 * i, snippet: '' }))
  };
}

const videos = [
  video('a', 'Gordon Ryan vs Pena', 'FloGrappling', '2024-03-01', 3),
  video('b', 'Meregali vs Buchecha', 'IBJJF', '2026-05-10', 1),
  video('c', 'Kaynan vs Tye', 'FloGrappling', null, 2)
];
const today = new Date(2026, 9, 19);

/** c.1 approved, b.0 with good votes, a.1 with bad votes. */
const curated = videos.map(v => ({
  ...v,
  moments: v.moments.map(m =>
    m.id === 'c.1' ? { ...m, status: 'bom' as const } :
    m.id === 'b.0' ? { ...m, votesUseful: 3, votesNotUseful: 1 } :
    m.id === 'a.1' ? { ...m, votesNotUseful: 2 } : m)
}));
const ids = (list: FightVideo[]) => list.map(v => v.id);

describe('filterVideos', () => {
  it('keeps every fight without filters', () => {
    expect(ids(filterVideos(videos, NO_FILTERS, today))).toEqual(['a', 'b', 'c']);
  });

  it('filters by athlete in the title, ignoring case and accents', () => {
    expect(ids(filterVideos(videos, { ...NO_FILTERS, athlete: 'MEREGALÍ' }, today))).toEqual(['b']);
  });

  it('with the catalog spellings of the athlete, finds the fights with any of them', () => {
    const filters = { ...NO_FILTERS, athlete: 'Buchecha', athleteNames: ['Marcus Almeida', 'Buchecha', 'Gordon'] };
    expect(ids(filterVideos(videos, filters, today))).toEqual(['a', 'b']);
  });

  it('filters by channel', () => {
    expect(ids(filterVideos(videos, { ...NO_FILTERS, channel: 'FloGrappling' }, today))).toEqual(['a', 'c']);
  });

  it('filters by period, leaving out fights without a date', () => {
    expect(ids(filterVideos(videos, { ...NO_FILTERS, period: '1a' }, today))).toEqual(['b']);
    expect(ids(filterVideos(videos, { ...NO_FILTERS, period: '2a' }, today))).toEqual(['b']);
    expect(ids(filterVideos(videos, { ...NO_FILTERS, period: '2a' }, new Date(2026, 1, 1)))).toEqual(['a', 'b']);
  });
});

describe('sortVideos', () => {
  it('keeps the API order for "mencoes" and puts the newest first for "recentes"', () => {
    expect(ids(sortVideos(videos, 'mencoes'))).toEqual(['a', 'b', 'c']);
    expect(ids(sortVideos(videos, 'recentes'))).toEqual(['b', 'a', 'c']);
  });

  it('puts first the fights with approved or well-voted moments for "melhores"', () => {
    expect(ids(sortVideos(curated, 'melhores'))).toEqual(['c', 'b', 'a']);
    expect(ids(sortVideos(videos, 'melhores'))).toEqual(['a', 'b', 'c']);
  });
});

describe('approved moments', () => {
  it('keeps only approved moments and their fights', () => {
    const result = filterVideos(curated, { ...NO_FILTERS, approvedOnly: true }, today);
    expect(ids(result)).toEqual(['c']);
    expect(result[0].moments.map(m => m.id)).toEqual(['c.1']);
  });
});

describe('clipsOf and channelsOf', () => {
  it('lists the moments fight by fight', () => {
    expect(clipsOf(videos).map(c => c.moment.id)).toEqual(['a.0', 'a.1', 'a.2', 'b.0', 'c.0', 'c.1']);
  });

  it('lists the best moments first for "melhores", taking turns between fights on ties', () => {
    expect(clipsOf(videos, 'melhores').map(c => c.moment.id)).toEqual(['a.0', 'b.0', 'c.0', 'a.1', 'c.1', 'a.2']);
    expect(clipsOf(curated, 'melhores').map(c => c.moment.id)).toEqual(['c.1', 'b.0', 'a.0', 'c.0', 'a.2', 'a.1']);
  });

  it('takes the athletes from "A vs B | Event" titles, those in more fights first', () => {
    const titled = [
      video('a', 'Gordon Ryan vs Felipe Pena | ADCC 2024', '', null),
      video('b', 'Meregali VS Gordon Ryan - Final', '', null),
      video('c', 'Highlights of the week', '', null),
      video('d', 'Kaynan Duarte vs. Tye Ruotolo (Superfight) | WNO', '', null),
      video('e', 'Full Match: Jean-Jacques Machado vs Rickson', '', null)
    ];
    expect(athletesOf(titled)).toEqual(['Gordon Ryan', 'Felipe Pena', 'Jean-Jacques Machado', 'Kaynan Duarte', 'Meregali', 'Rickson', 'Tye Ruotolo']);
  });

  it('lists each channel once', () => {
    expect(channelsOf(videos)).toEqual(['FloGrappling', 'IBJJF']);
  });
});
