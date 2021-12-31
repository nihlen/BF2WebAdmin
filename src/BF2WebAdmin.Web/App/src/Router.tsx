import * as React from 'react';
import { connect } from 'react-redux';
import { BrowserRouter as Router, Link, Route } from 'react-router-dom';
import Home from './components/Home';
import Server from './containers/Server';
import Counter from './containers/Counter';
import { RootState } from './store';
import { GameServer } from './store/socket/index';

interface AppProps {
    servers: GameServer[];
}

const AppRouter: React.SFC<AppProps> = (props) => {
    const servers = props.servers.map(s => (
        <li key={s.id}><Link to={`/server/${s.id}`}>{s.name}</Link></li>
    ));
    return (
        <Router>
            <div>
                <ul>
                    <li><Link to="/">Home</Link></li>
                    <li><Link to="/counter">Counter</Link></li>
                    {servers}
                </ul>
                <Route exact={true} path="/" component={Home} />
                <Route path="/counter" component={Counter} />
                <Route path="/server/:id" component={Server} />
            </div>
        </Router>
    );
};

const mapStateToProps = (state: RootState) => ({
    servers: state.socket.servers,
});

const mapDispatchToProps = {};

const AppContainer = connect(mapStateToProps, mapDispatchToProps)(AppRouter);
export default AppContainer;