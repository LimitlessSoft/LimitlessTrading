"use strict";
/**
 * This file implements tests for the [[IBApiNext.getContractDetails]] function.
 */
Object.defineProperty(exports, "__esModule", { value: true });
const __1 = require("../../..");
describe("RxJS Wrapper: getFundamentalData()", () => {
    test("Error Event", (done) => {
        const apiNext = new __1.IBApiNext();
        const api = apiNext.api;
        // emit a error event and verify RxJS result
        const testValue = "We want this error";
        apiNext
            .getFundamentalData(new __1.Stock("AAPL"), "ReportSnapshot", [])
            .then(() => done("failed, then should not be called!"))
            .catch((error) => {
            expect(error.error.message).toEqual(testValue);
            done();
        });
        api.emit(__1.EventName.error, new Error(testValue), -1, 1);
    });
    test("Incremental collection", (done) => {
        const apiNext = new __1.IBApiNext();
        const api = apiNext.api;
        // emit contractDetails and contractDetailsEnd event and verify all subscribers receive it
        const testValue1 = "testValue1 --- is not an xml string";
        apiNext
            .getFundamentalData(new __1.Stock("AAPL"), "ReportSnapshot", [])
            .then((data) => {
            expect(data).toEqual(testValue1);
            done();
        })
            .catch((error) => {
            fail(error.error.message);
        });
        api.emit(__1.EventName.fundamentalData, 1, testValue1);
    });
});
//# sourceMappingURL=get-fundamental.test.js.map