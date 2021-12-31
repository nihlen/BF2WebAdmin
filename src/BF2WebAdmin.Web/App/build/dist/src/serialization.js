import { IMessage } from './protos/protos';
var JsonSerializer = /** @class */ (function () {
    function JsonSerializer() {
    }
    JsonSerializer.prototype.serialize = function (data) {
        return JSON.stringify(data);
    };
    JsonSerializer.prototype.deserialize = function (data) {
        if (typeof data !== 'string') {
            return data;
        }
        return JSON.parse(data);
    };
    return JsonSerializer;
}());
export { JsonSerializer };
var ProtobufSerializer = /** @class */ (function () {
    function ProtobufSerializer() {
    }
    ProtobufSerializer.prototype.serialize = function (data) {
        return IMessage.encode(data).finish();
    };
    ProtobufSerializer.prototype.deserialize = function (data) {
        return IMessage.decode(data); // ?? Cannot convert to T
    };
    return ProtobufSerializer;
}());
export { ProtobufSerializer };
//# sourceMappingURL=serialization.js.map