import * as React from 'react';
import { Provider } from 'react-redux';
import { renderToString } from 'react-dom/server';
import { match, RouterContext, createMemoryHistory } from 'react-router';
import routes from './routes';
import configureStore from './configure-store';

export default function (params: any): Promise<{ html: string }> {
    return new Promise<{ html: string, globals: { [key: string]: any } }>((resolve, reject) => {
        // Match the incoming request against the list of client-side routes
        match({ routes, location: params.location }, (error : any, redirectLocation : any, renderProps: any) => {
            if (error) {
                throw error;
            }

            // If it didn't match any route, renderProps will be undefined
            if (!renderProps) {
                throw new Error(`The location '${ params.url }' doesn't match any route configured in react-router.`);
            }

            // Build an instance of the application
            const store = configureStore();
            const app = (
                <Provider store={ store }>
                    <RouterContext {...renderProps} />
                </Provider>
            );

            // Perform an initial render that will cause any async tasks (e.g., data access) to begin
            renderToString(app);

            // Once the tasks are done, we can perform the final render
            // We also send the redux store state, so the client can continue execution where the server left off
            params.domainTasks.then(() => {
                resolve({
                    html: renderToString(app),
                    globals: { initialReduxState: store.getState() }
                });
            }, reject); // Also propagate any errors back into the host application
        });
    });
}