"use strict";
const React = require("react");
const react_router_1 = require("react-router");
const react_redux_1 = require("react-redux");
const WeatherForecastsState = require("../store/WeatherForecasts");
class FetchData extends React.Component {
    componentWillMount() {
        // This method runs when the component is first added to the page
        let startDateIndex = parseInt(this.props.params.startDateIndex) || 0;
        this.props.requestWeatherForecasts(startDateIndex);
    }
    componentWillReceiveProps(nextProps) {
        // This method runs when incoming props (e.g., route params) change
        let startDateIndex = parseInt(nextProps.params.startDateIndex) || 0;
        this.props.requestWeatherForecasts(startDateIndex);
    }
    render() {
        return React.createElement("div", null,
            React.createElement("h1", null, "Weather forecast"),
            React.createElement("p", null, "This component demonstrates fetching data from the server and working with URL parameters."),
            this.renderForecastsTable(),
            this.renderPagination());
    }
    renderForecastsTable() {
        return React.createElement("table", { className: 'table' },
            React.createElement("thead", null,
                React.createElement("tr", null,
                    React.createElement("th", null, "Date"),
                    React.createElement("th", null, "Temp. (C)"),
                    React.createElement("th", null, "Temp. (F)"),
                    React.createElement("th", null, "Summary"))),
            React.createElement("tbody", null, this.props.forecasts.map(forecast => React.createElement("tr", { key: forecast.dateFormatted },
                React.createElement("td", null, forecast.dateFormatted),
                React.createElement("td", null, forecast.temperatureC),
                React.createElement("td", null, forecast.temperatureF),
                React.createElement("td", null, forecast.summary)))));
    }
    renderPagination() {
        let prevStartDateIndex = this.props.startDateIndex - 5;
        let nextStartDateIndex = this.props.startDateIndex + 5;
        return React.createElement("p", { className: 'clearfix text-center' },
            React.createElement(react_router_1.Link, { className: 'btn btn-default pull-left', to: `/fetchdata/${prevStartDateIndex}` }, "Previous"),
            React.createElement(react_router_1.Link, { className: 'btn btn-default pull-right', to: `/fetchdata/${nextStartDateIndex}` }, "Next"),
            this.props.isLoading ? React.createElement("span", null, "Loading...") : []);
    }
}
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = react_redux_1.connect((state) => state.weatherForecasts, // Selects which state properties are merged into the component's props
WeatherForecastsState.actionCreators // Selects which action creators are merged into the component's props
)(FetchData);
//# sourceMappingURL=FetchData.js.map