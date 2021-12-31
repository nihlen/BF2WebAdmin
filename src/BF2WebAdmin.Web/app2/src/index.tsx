import React from "react";
import ReactDOM from "react-dom";
import { BrowserRouter as Router, Route } from "react-router-dom";
import "./index.css";
import Header from "./components/Header";
import Home from "./components/Home";
import Counter from "./components/Counter";
import * as serviceWorker from "./serviceWorker";

ReactDOM.render(
  <Router>
    <Header />
    <Route path="/" component={Home} />
    <Route path="/counter" component={Counter} />
  </Router>,
  document.getElementById("root")
);

// If you want your app to work offline and load faster, you can change
// unregister() to register() below. Note this comes with some pitfalls.
// Learn more about service workers: https://bit.ly/CRA-PWA
serviceWorker.unregister();
