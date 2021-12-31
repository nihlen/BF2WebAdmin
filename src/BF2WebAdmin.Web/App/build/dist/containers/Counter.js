import { connect } from 'react-redux';
import Counter from '../components/Counter';
import { actionCreators } from '../store/counter';
var mapStateToProps = function (state) { return ({
    count: state.counter.count,
}); };
var mapDispatchToProps = {
    onIncrement: actionCreators.incrementCount,
    onDecrement: actionCreators.decrementCount
};
export default connect(mapStateToProps, mapDispatchToProps)(Counter);
//# sourceMappingURL=Counter.js.map