import * as React from 'react';

export interface CounterProps {
    count: number;
    onIncrement: () => any;
    onDecrement: () => any;
}

const Counter: React.SFC<CounterProps> = (props) => {
    return (
        <div className="counter">
            <div>
                Counter: {props.count}
            </div>
            <div>
                <button onClick={props.onDecrement}>-</button>
                <button onClick={props.onIncrement}>+</button>
            </div>
        </div>
    );
};

export default Counter;
