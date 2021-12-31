import { combineEpics } from 'redux-observable';
import { epics as socket } from './socket';
export var rootEpic = combineEpics(socket);
//# sourceMappingURL=root-epic.js.map