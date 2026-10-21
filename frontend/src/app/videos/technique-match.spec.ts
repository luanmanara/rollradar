import { AthleteSummary, TechniqueSummary } from './fight-video.model';
import { matchAthletes, matchTechniques, normalize, similarTechniques, techniqueLabel } from './technique-match';

const t = (english: string, portuguese: string, videoCount: number, aliases: string[] = []): TechniqueSummary =>
  ({ english, portuguese, category: 'Cat', videoCount, aliases });

const catalog = [
  t('Rear Naked Choke', 'Mata-leão', 7, ['RNC']),
  t('Armbar', 'Chave de braço', 9, ['Armlock', 'juji-gatame']),
  t('Belly-Down Armbar', 'Chave de braço de bruços', 0),
  t('Closed Guard', 'Guarda fechada', 6),
  t('Spider Guard', 'Guarda aranha', 2),
  t('Back Control / Back Mount', 'Pegada nas costas', 3, ['Costas']),
  t('Half Guard', 'Meia-guarda', 8)
];

const names = (query: string) => matchTechniques(query, catalog).map(x => x.english);

describe('normalize', () => {
  it('removes case, accents and hyphens', () => {
    expect(normalize('  Mata-Leão ')).toBe('mata leao');
    expect(normalize('CHAVE DE BRAÇO')).toBe('chave de braco');
  });
});

describe('matchTechniques', () => {
  it('finds techniques by Portuguese name without accents', () => {
    expect(names('mata leao')).toEqual(['Rear Naked Choke']);
    expect(names('braco')[0]).toBe('Armbar');
  });

  it('finds techniques by alias and by any English alternative', () => {
    expect(names('rnc')).toEqual(['Rear Naked Choke']);
    expect(names('armlock')).toEqual(['Armbar']);
    expect(names('back mount')).toEqual(['Back Control / Back Mount']);
  });

  it('ranks exact names first, then prefixes, then word starts, breaking ties by video count', () => {
    expect(names('armbar')).toEqual(['Armbar', 'Belly-Down Armbar']);
    expect(names('guarda')).toEqual(['Closed Guard', 'Spider Guard', 'Half Guard']);
  });

  it('returns nothing for an empty or unknown query', () => {
    expect(names('  ')).toEqual([]);
    expect(names('xyz')).toEqual([]);
  });
});

describe('similarTechniques', () => {
  const similar = (query: string) => similarTechniques(query, catalog).map(x => x.english);

  it('suggests techniques a typo away from the query', () => {
    expect(similar('mata leon')).toEqual(['Rear Naked Choke']);
    expect(similar('armbr')[0]).toBe('Armbar');
    expect(similar('arana')).toEqual(['Spider Guard']);
  });

  it('suggests nothing for short or unrelated queries', () => {
    expect(similar('ab')).toEqual([]);
    expect(similar('xyzzy')).toEqual([]);
  });
});

describe('techniqueLabel', () => {
  it('shows both languages only when they differ', () => {
    expect(techniqueLabel(catalog[1])).toBe('Chave de braço (Armbar)');
    expect(techniqueLabel(t('Kimura', 'Kimura', 1))).toBe('Kimura');
  });
});

describe('matchAthletes', () => {
  const athletes: AthleteSummary[] = [
    { name: 'Marcus Almeida', fights: 3, aliases: ['Buchecha'] },
    { name: 'Mica Galvão', fights: 10, aliases: ['Mica Galvao'] },
    { name: 'Buchholz', fights: 1 }
  ];

  it('finds an athlete by an alias of the catalog', () => {
    expect(matchAthletes('buche', athletes).map(a => a.name)).toEqual(['Marcus Almeida']);
  });

  it('lists each athlete once even when several of their names match', () => {
    expect(matchAthletes('mica', athletes).map(a => a.name)).toEqual(['Mica Galvão']);
  });
});
