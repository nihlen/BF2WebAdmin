"use strict";
const redux_1 = require("redux");
const redux_thunk_1 = require("redux-thunk");
const react_router_redux_1 = require("react-router-redux");
const Store = require("./store");
function configureStore(initialState) {
    // Build middleware. These are functions that can process the actions before they reach the store.
    const windowIfDefined = typeof window === 'undefined' ? null : window;
    // If devTools is installed, connect to it
    const devToolsExtension = windowIfDefined && windowIfDefined.devToolsExtension;
    const createStoreWithMiddleware = redux_1.compose(redux_1.applyMiddleware(redux_thunk_1.default), devToolsExtension ? devToolsExtension() : f => f)(redux_1.createStore);
    // Combine all reducers and instantiate the app-wide store instance
    const allReducers = buildRootReducer(Store.reducers);
    const store = createStoreWithMiddleware(allReducers, initialState);
    // Enable Webpack hot module replacement for reducers
    if (module.hot) {
        module.hot.accept('./store', () => {
            const nextRootReducer = require('./store');
            store.replaceReducer(buildRootReducer(nextRootReducer.reducers));
        });
    }
    return store;
}
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = configureStore;
function buildRootReducer(allReducers) {
    return redux_1.combineReducers(Object.assign({}, allReducers, { routing: react_router_redux_1.routerReducer }));
}
//# sourceMappingURL=configureStore.js.map