"use strict";
const React = require("react");
const react_router_1 = require("react-router");
class NavMenu extends React.Component {
    render() {
        return React.createElement("div", { className: 'main-nav' },
            React.createElement("div", { className: 'navbar navbar-inverse' },
                React.createElement("div", { className: 'navbar-header' },
                    React.createElement("button", { type: 'button', className: 'navbar-toggle', "data-toggle": 'collapse', "data-target": '.navbar-collapse' },
                        React.createElement("span", { className: 'sr-only' }, "Toggle navigation"),
                        React.createElement("span", { className: 'icon-bar' }),
                        React.createElement("span", { className: 'icon-bar' }),
                        React.createElement("span", { className: 'icon-bar' })),
                    React.createElement(react_router_1.Link, { className: 'navbar-brand', to: '/' }, "WebApplicationBasic")),
                React.createElement("div", { className: 'clearfix' }),
                React.createElement("div", { className: 'navbar-collapse collapse' },
                    React.createElement("ul", { className: 'nav navbar-nav' },
                        React.createElement("li", null,
                            React.createElement(react_router_1.Link, { to: '/', activeClassName: 'active' },
                                React.createElement("span", { className: 'glyphicon glyphicon-home' }),
                                " Home")),
                        React.createElement("li", null,
                            React.createElement(react_router_1.Link, { to: '/counter', activeClassName: 'active' },
                                React.createElement("span", { className: 'glyphicon glyphicon-education' }),
                                " Counter")),
                        React.createElement("li", null,
                            React.createElement(react_router_1.Link, { to: '/fetchdata', activeClassName: 'active' },
                                React.createElement("span", { className: 'glyphicon glyphicon-th-list' }),
                                " Fetch data"))))));
    }
}
exports.NavMenu = NavMenu;
//# sourceMappingURL=NavMenu.js.map