import { applyMiddleware, compose, createStore } from 'redux';
import { createEpicMiddleware } from 'redux-observable';
import { rootEpic, rootReducer } from './';
var customWindow = window;
var composeEnhancers = (process.env.NODE_ENV === 'development' &&
    customWindow && customWindow.__REDUX_DEVTOOLS_EXTENSION_COMPOSE__
// && customWindow.__REDUX_DEVTOOLS_EXTENSION__ && customWindow.__REDUX_DEVTOOLS_EXTENSION__()
) || compose;
var configureStore = function (initialState) {
    var middlewares = [
        createEpicMiddleware(rootEpic),
    ];
    var enhancer = composeEnhancers(applyMiddleware.apply(void 0, middlewares));
    return createStore(rootReducer, initialState, enhancer);
};
// Pass an optional param to rehydrate state on app start
var store = configureStore();
// Export store singleton instance
export default store;
//# sourceMappingURL=store.js.map