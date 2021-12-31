import { combineEpics } from 'redux-observable';
import { epics as socket } from './socket';

export const rootEpic = combineEpics(
    socket,
);