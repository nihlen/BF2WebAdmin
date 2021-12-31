var __extends = (this && this.__extends) || (function () {
    var extendStatics = function (d, b) {
        extendStatics = Object.setPrototypeOf ||
            ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
            function (d, b) { for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p]; };
        return extendStatics(d, b);
    };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
import * as React from 'react';
import { connect } from 'react-redux';
import { actionCreators } from './store/socket';
var Startup = /** @class */ (function (_super) {
    __extends(Startup, _super);
    function Startup() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Startup.prototype.componentDidMount = function () {
        this.props.websocketStart();
    };
    Startup.prototype.render = function () {
        return this.props.isConnected
            ? this.props.children
            : (React.createElement("p", null, "Connecting..."));
    };
    return Startup;
}(React.Component));
var mapStateToProps = function (state) { return ({
    isConnected: state.socket.isConnected,
}); };
var mapDispatchToProps = {
    websocketStart: actionCreators.websocketStart
};
export default connect(mapStateToProps, mapDispatchToProps)(Startup);
//# sourceMappingURL=Startup.js.map