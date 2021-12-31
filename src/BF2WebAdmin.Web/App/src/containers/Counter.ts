import { connect } from 'react-redux';
import Counter from '../components/Counter';
import { actionCreators } from '../store/counter';
import { RootState } from '../store';

const mapStateToProps = (state: RootState) => ({
    count: state.counter.count,
});

const mapDispatchToProps = {
    onIncrement: actionCreators.incrementCount,
    onDecrement: actionCreators.decrementCount
};

export default connect(mapStateToProps, mapDispatchToProps)(Counter);