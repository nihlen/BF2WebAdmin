import * as React from 'react';
import { Router, Route, HistoryBase } from 'react-router';
import { Layout } from './components/layout';
import Home from './components/home';

export default <Route component={ Layout }>
    <Route path='/' components={{ body: Home }} />
</Route>;