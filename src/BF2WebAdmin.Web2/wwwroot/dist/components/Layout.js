"use strict";
const React = require("react");
const NavMenu_1 = require("./NavMenu");
class Layout extends React.Component {
    render() {
        return React.createElement("div", { className: 'container-fluid' },
            React.createElement("div", { className: 'row' },
                React.createElement("div", { className: 'col-sm-3' },
                    React.createElement(NavMenu_1.NavMenu, null)),
                React.createElement("div", { className: 'col-sm-9' }, this.props.body)));
    }
}
exports.Layout = Layout;
//# sourceMappingURL=Layout.js.map