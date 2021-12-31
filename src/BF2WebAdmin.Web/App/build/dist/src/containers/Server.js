import { connect } from 'react-redux';
import Server from '../components/Server';
var mapStateToProps = function (state, ownProps) { return ({
    server: state.socket.servers.find(function (s) { return s.id === ownProps.match.params.id; })
}); };
var mapDispatchToProps = {};
export default connect(mapStateToProps, mapDispatchToProps)(Server);
//# sourceMappingURL=Server.js.map