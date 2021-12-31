import * as React from 'react';
import { connect } from 'react-redux';
import { actionCreators } from './store/socket';
import { RootState } from './store';

interface StartupProps {
    isConnected: boolean;
    websocketStart: any;
}

class Startup extends React.Component<StartupProps, {}> {

    componentDidMount() {
        this.props.websocketStart();
    }

    render() {
        return this.props.isConnected
            ? this.props.children
            : (<p>Connecting...</p>);
    }
}

const mapStateToProps = (state: RootState) => ({
    isConnected: state.socket.isConnected,
});

const mapDispatchToProps = {
    websocketStart: actionCreators.websocketStart
};

export default connect(mapStateToProps, mapDispatchToProps)(Startup);