import { FightVideo, TechniqueMoment } from '../videos/fight-video.model';

/** The visitor's own vote; null when they haven't voted. */
export interface MyVote {
  useful: boolean | null;
}

/** Votes on a moment, where each IP address counts once. */
export interface VoteTally {
  useful: number;
  notUseful: number;
  total: number;
}

/** The curator's verdict, same values as in the database plan. */
export type CurationStatus = 'pendente' | 'bom' | 'ruim';

export interface CurationItem {
  /** The video with only this moment. */
  video: FightVideo;
  moment: TechniqueMoment;
  votes: VoteTally;
  status: CurationStatus;
}
