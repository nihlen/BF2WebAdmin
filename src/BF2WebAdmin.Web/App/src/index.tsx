import * as React from 'react';
import * as ReactDOM from 'react-dom';
import { Provider } from 'react-redux';
// import { Router } from 'react-router';
import store from './store/store';
import Startup from './Startup';
import AppContainer from './Router';

ReactDOM.render(
    <Provider store={store}>
        <Startup>
            <AppContainer />
        </Startup>
    </Provider>,
    document.getElementById('app') as HTMLElement
);

// registerServiceWorker();
