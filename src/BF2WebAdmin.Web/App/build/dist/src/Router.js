import * as React from 'react';
import { connect } from 'react-redux';
import { BrowserRouter as Router, Link, Route } from 'react-router-dom';
import Home from './components/Home';
import Server from './containers/Server';
import Counter from './containers/Counter';
var AppRouter = function (props) {
    var servers = props.servers.map(function (s) { return (React.createElement("li", { key: s.id },
        React.createElement(Link, { to: "/server/" + s.id }, s.name))); });
    return (React.createElement(Router, null,
        React.createElement("div", null,
            React.createElement("ul", null,
                React.createElement("li", null,
                    React.createElement(Link, { to: "/" }, "Home")),
                React.createElement("li", null,
                    React.createElement(Link, { to: "/counter" }, "Counter")),
                servers),
            React.createElement(Route, { exact: true, path: "/", component: Home }),
            React.createElement(Route, { path: "/counter", component: Counter }),
            React.createElement(Route, { path: "/server/:id", component: Server }))));
};
var mapStateToProps = function (state) { return ({
    servers: state.socket.servers,
}); };
var mapDispatchToProps = {};
var AppContainer = connect(mapStateToProps, mapDispatchToProps)(AppRouter);
export default AppContainer;
//# sourceMappingURL=Router.js.map