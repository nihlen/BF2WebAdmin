import { Actions as CounterActions } from './counter';
import { Actions as SocketActions } from './socket';

export type RootAction =
    | CounterActions[keyof CounterActions]
    | SocketActions[keyof SocketActions];