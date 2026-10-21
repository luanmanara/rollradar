export interface TechniqueMoment {
  /** Stable id to vote on and curate the moment, e.g. "WxeOHXvLrpw.armbar.900". */
  id: string;
  /** Term highlighted in the English captions, e.g. "Omoplata". */
  technique: string;
  /** Second of the video where the caption segment mentioning the technique starts. */
  startSeconds: number;
  /** Caption text around the mention. */
  snippet: string;
  /** Catalog technique id ("rear-naked-choke") when known. */
  techniqueId?: string | null;
  /** The curator's verdict; moments marked "ruim" don't come in the search. */
  status?: MomentStatus;
  /** Visitors' votes, each IP address counted once. */
  votesUseful?: number;
  votesNotUseful?: number;
}

export type MomentStatus = 'pendente' | 'bom' | 'ruim';

export interface FightVideo {
  id: string;
  title: string;
  url: string;
  thumbnailUrl: string;
  channel: string;
  /** ISO date, e.g. "2019-12-02"; null when unknown. */
  uploadDate: string | null;
  moments: TechniqueMoment[];
  /** Caption language, e.g. "en". */
  language?: string | null;
  /** Length of the video in seconds, when known. */
  durationSeconds?: number | null;
}

export interface TechniqueSummary {
  /** e.g. "Armbar" or "Back Control / Back Mount" */
  english: string;
  /** Empty for terms found in the data that are not in the catalog. */
  portuguese: string;
  category: string;
  videoCount: number;
  /** Other names, e.g. "RNC", "armlock". */
  aliases?: string[];
  /** Catalog id ("rear-naked-choke"), used in the technique page address /tecnica/:id. */
  id?: string;
  /** Moments the curator marked as good. */
  approvedMoments?: number;
}

/** An athlete found in the fight titles ("A vs B | Evento"). */
export interface AthleteSummary {
  name: string;
  /** Fights with moments that have the athlete in the title. */
  fights: number;
  /** Other spellings from the athlete catalog ("Mica Galvao" for "Mica Galvão"). */
  aliases?: string[];
}

export interface SearchResult {
  /** Technique as typed. */
  query: string;
  /** Catalog techniques the query was recognized as; empty when it is not in the catalog. */
  techniques: TechniqueSummary[];
  /** English terms looked up in the captions. */
  searchTerms: string[];
  videos: FightVideo[];
}
