import { connect } from 'react-redux';
import Server, { ServerProps } from '../components/Server';
import { RootState } from '../store';

const mapStateToProps = (state: RootState, ownProps: ServerProps) => ({
    server: state.socket.servers.find(s => s.id === ownProps.match.params.id)
});

const mapDispatchToProps = {
};

export default connect(mapStateToProps, mapDispatchToProps)(Server);