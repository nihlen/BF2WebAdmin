// $> pbjs -t static-module -w commonjs -o compiled.js file1.proto file2.proto
// $> pbts -o compiled.d.ts compiled.js
var pbjs = require("protobufjs/cli/pbjs");
pbjs.main(["-t", "static-module", "-w", "commonjs", "-o", "./src/protos/protos.js", "messages.proto"], function (err, output) {
    if (err) {
        console.error('pbjs error', err);
        throw err;
    }
    // do something with output
    console.log('pbjs done', output.length);
});
var pbts = require("protobufjs/cli/pbts");
pbts.main(["-o", "./src/protos/protos.d.ts", "./src/protos/protos.js"], function (err, output) {
    if (err) {
        console.error('pbts error', err);
        throw err;
    }
    // do something with output
    console.log('pbts done', output.length);
});
//# sourceMappingURL=protobuild.js.map