import * as React from 'react';
import { BrowserRouter as Router, Link, Route } from 'react-router-dom';
import Home from './components/Home';
import Server from './components/Server';
import Counter from './containers/Counter';
export var AppRouter = function () {
    return (React.createElement(Router, null,
        React.createElement("div", null,
            React.createElement("ul", null,
                React.createElement("li", null,
                    React.createElement(Link, { to: "/" }, "Home")),
                React.createElement("li", null,
                    React.createElement(Link, { to: "/counter" }, "Counter")),
                React.createElement("li", null,
                    React.createElement(Link, { to: "/server/netsky-de" }, "Server"))),
            React.createElement(Route, { exact: true, path: "/", component: Home }),
            React.createElement(Route, { path: "/counter", component: Counter }),
            React.createElement(Route, { path: "/server/:id", component: Server }))));
};
//# sourceMappingURL=Router.js.map