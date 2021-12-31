// var path = require('path');
// var webpack = require('webpack');
// var merge = require('webpack-merge');
// var devConfig = require('./webpack.config.dev');
// var prodConfig = require('./webpack.config.prod');
// var ExtractTextPlugin = require('extract-text-webpack-plugin');
// var isDevelopment = process.env.ASPNETCORE_ENVIRONMENT === 'Development';

// module.exports = merge({
//     resolve: {
//         extensions: ['', '.js', '.jsx', '.ts', '.tsx']
//     },
//     module: {
//         loaders: [
//             { test: /\.ts(x?)$/, include: /App/, loader: 'ts-loader?silent=true' },
//             { test: /\.css$/, loader: ExtractTextPlugin.extract(['css-loader']) }
//         ]
//     },
//     // entry: {
//     //     main: ['./App/boot-client.tsx']
//     // },
//     entry: { 'main-server': './App/boot-server.tsx' },
//     output: {
//         path: path.join(__dirname, 'wwwroot', 'dist'),
//         // filename: '[name].js',
//         publicPath: '/dist/'
//     },
//     plugins: [
//         new ExtractTextPlugin('site.css')
//         // new webpack.DllReferencePlugin({
//         //     context: __dirname,
//         //     manifest: require('./wwwroot/dist/vendor-manifest.json')
//         // })
//     ]
// }, isDevelopment ? devConfig : prodConfig);
var isDevBuild = process.argv.indexOf('--env.prod') < 0;
var path = require('path');
var webpack = require('webpack');
var ExtractTextPlugin = require('extract-text-webpack-plugin');
var merge = require('webpack-merge');

// Configuration in common to both client-side and server-side bundles
var sharedConfig = () => ({
    resolve: { extensions: ['', '.js', '.jsx', '.ts', '.tsx'] },
    output: {
        filename: '[name].js',
        publicPath: '/dist/' // Webpack dev middleware, if enabled, handles requests for this URL prefix
    },
    module: {
        loaders: [
            // { test: /\.tsx?$/, include: /App/, loader: 'babel-loader' },
            // { test: /\.scss$/, include: /App/, loaders: ["style-loader", "css-loader", "sass-loader"] },
            { test: /\.json$/, loader: "json-loader" },
            // { test: /\.scss$/, loader: ExtractTextPlugin.extract('style-loader', 'css-loader!sass-loader') },
            // { test: /\.scss$/, loader: ExtractTextPlugin.extract('style', 'css!sass') },
            { test: /\.scss$/, loader: ExtractTextPlugin.extract("style", "css!sass") },
            { test: /\.tsx?$/, include: /App/, loader: 'ts-loader', query: { silent: true } }
        ]
    },
    plugins: [
        new ExtractTextPlugin('app.css')
    ],
    debug: true
});

// Configuration for client-side bundle suitable for running in browsers
var clientBundleOutputDir = './wwwroot/dist';
var clientBundleConfig = merge(sharedConfig(), {
    entry: { 'main-client': './App/boot-client.tsx' },
    module: {
        loaders: [
            // { test: /\.css$/, loader: ExtractTextPlugin.extract(['css-loader']) },
            // { test: /\.scss$/, loaders: ["style-loader", "css-loader?sourceMap", "sass-loader?sourceMap"] },
            // { test: /\.scss$/, loader: ExtractTextPlugin.extract(['css-loader', 'sass-loader']) },
            // { test: /\.scss$/, loader: ExtractTextPlugin.extract('style', 'css!sass') },
            // { test: /\.scss$/, loader: ExtractTextPlugin.extract('style-loader', 'css-loader!sass-loader') },
            { test: /\.scss$/, loader: ExtractTextPlugin.extract("style", "css!sass") },
            { test: /\.(png|jpg|jpeg|gif|svg)$/, loader: 'url-loader', query: { limit: 25000 } }
        ]
    },
    output: { path: path.join(__dirname, clientBundleOutputDir) },
    plugins: [
        new ExtractTextPlugin('main.css'),
        // new webpack.DllReferencePlugin({
        //     context: __dirname,
        //     manifest: require('./wwwroot/dist/vendor-manifest.json')
        // })
    ].concat(isDevBuild ? [
        // Plugins that apply in development builds only
        new webpack.SourceMapDevToolPlugin({
            filename: '[file].map', // Remove this line if you prefer inline source maps
            moduleFilenameTemplate: path.relative(clientBundleOutputDir, '[resourcePath]') // Point sourcemap entries to the original file locations on disk
        })
    ] : [
            // Plugins that apply in production builds only
            new webpack.optimize.OccurenceOrderPlugin(),
            new webpack.optimize.UglifyJsPlugin({ compress: { warnings: false } })
        ])
});

// Configuration for server-side (prerendering) bundle suitable for running in Node
var serverBundleConfig = merge(sharedConfig(), {
    resolve: { packageMains: ['main'] },
    entry: { 'main-server': './App/boot-server.tsx' },
    plugins: [
        { test: /\.scss$/, loader: ExtractTextPlugin.extract("style", "css!sass") }
        // new webpack.DllReferencePlugin({
        //     context: __dirname,
        //     manifest: require('./App/dist/vendor-manifest.json'),
        //     sourceType: 'commonjs2',
        //     name: './vendor'
        // })
    ],
    output: {
        libraryTarget: 'commonjs',
        path: path.join(__dirname, './App/dist')
    },
    target: 'node',
    devtool: 'inline-source-map'
});

module.exports = [clientBundleConfig, serverBundleConfig];