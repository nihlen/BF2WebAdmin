import * as React from 'react';
import * as ReactDOM from 'react-dom';
import { Provider } from 'react-redux';
// import { Router } from 'react-router';
import store from './store/store';
import Startup from './Startup';
import { AppRouter } from './Router';
ReactDOM.render(React.createElement(Provider, { store: store },
    React.createElement(Startup, null,
        React.createElement(AppRouter, null))), document.getElementById('app'));
// registerServiceWorker();
//# sourceMappingURL=index.js.map