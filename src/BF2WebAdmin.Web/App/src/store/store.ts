import { applyMiddleware, compose, createStore } from 'redux';
import { createEpicMiddleware } from 'redux-observable';
import { rootEpic, rootReducer, RootState } from './';

const customWindow = window as any;

const composeEnhancers = (
    process.env.NODE_ENV === 'development' &&
    customWindow && customWindow.__REDUX_DEVTOOLS_EXTENSION_COMPOSE__
    // && customWindow.__REDUX_DEVTOOLS_EXTENSION__ && customWindow.__REDUX_DEVTOOLS_EXTENSION__()
) || compose;

const configureStore = (initialState?: RootState) => {

    const middlewares = [
        createEpicMiddleware(rootEpic),
    ];

    const enhancer = composeEnhancers(
        applyMiddleware(...middlewares),
    );

    return createStore(
        rootReducer,
        initialState!,
        enhancer,
    );

};

// Pass an optional param to rehydrate state on app start
const store = configureStore();

// Export store singleton instance
export default store;