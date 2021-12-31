// Patch RxJS to reduce bundle size
// see: https://github.com/ReactiveX/rxjs/blob/master/doc/lettable-operators.md
// see: https://blog.hackages.io/rxjs-5-5-piping-all-the-things-9d469d1b3f44

// import { Observable } from 'rxjs/Observable';

// Observable class extensions
export { from as ObservableFrom } from 'rxjs/observable/from';
export { of as ObservableOf } from 'rxjs/observable/of';
export { webSocket as ObservableWebSocket } from 'rxjs/observable/dom/webSocket';

// Observable operators
export { debounceTime } from 'rxjs/operators/debounceTime';
export { switchMap } from 'rxjs/operators/switchMap';
export { distinctUntilChanged } from 'rxjs/operators/distinctUntilChanged';
export { map } from 'rxjs/operators/map';
export { takeUntil } from 'rxjs/operators/takeUntil';

// To check import sizes:
// // Observable class extensions
// import { from } from 'rxjs/observable/from';
// import { of } from 'rxjs/observable/of';

// // Observable operators
// import { debounceTime } from 'rxjs/operators/debounceTime';
// import { switchMap } from 'rxjs/operators/switchMap';
// import { distinctUntilChanged } from 'rxjs/operators/distinctUntilChanged';
// import { map } from 'rxjs/operators/map';
