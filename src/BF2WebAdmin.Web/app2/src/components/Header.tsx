import React, { useReducer, useMemo, Reducer, useRef, useEffect } from "react";
import useWebSocket, { Options } from "../lib/useWebsocket";
import reducer, { State, Action } from "../store/reducer";
import { Link } from "react-router-dom";

const webSocketUrl = "ws://localhost:56920/";
const initialState: State = { isConnected: false, servers: [] };

const testOptions: Options = {
  onClose: () => console.log("onClose"),
  onError: () => console.log("onError"),
  onMessage: () => console.log("onMessage"),
  onOpen: () => console.log("onOpen"),
  share: false
};

function Header() {
  const [state, dispatch] = useReducer<Reducer<State, Action>>(reducer, initialState);
  console.log(state);
  const options = useMemo(
    () =>
      ({
        share: true,
        onOpen: () => dispatch({ type: "WEBSOCKET_START" }),
        onClose: () => dispatch({ type: "WEBSOCKET_STOP" }),
        onError: e => console.error("WebSocket error", e),
        onMessage: e => dispatch({ type: "WEBSOCKET_MESSAGE", payload: e.data })
      } as Options),
    [dispatch]
  );

  //   const [sendMessage, lastMessage, readyState] = useWebSocket(webSocketUrl);
  useWebSocket(webSocketUrl, options);

  //   const servers = state.servers.map(s => (
  //     <li key={s.id}>
  //       <Link to={"server/" + s.id}>{s.name}</Link>
  //     </li>
  //   ));

  return (
    <header className="App-header">
      <ul>
        <li>
          <Link to="/">Home</Link>
        </li>
        <li>
          <Link to="/counter">Counter</Link>
        </li>
        {/* {servers} */}
      </ul>
    </header>
  );
}

export default React.memo(Header);
