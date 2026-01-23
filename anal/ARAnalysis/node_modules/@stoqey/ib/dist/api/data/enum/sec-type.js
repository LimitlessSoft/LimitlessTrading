"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.SecType = void 0;
/**
 * Security types.
 */
var SecType;
(function (SecType) {
    /** Stock (or ETF) */
    SecType["STK"] = "STK";
    /* Option. */
    SecType["OPT"] = "OPT";
    /* Future */
    SecType["FUT"] = "FUT";
    SecType["CONTFUT"] = "CONTFUT";
    /** Forex pair. */
    SecType["CASH"] = "CASH";
    /** Bond. */
    SecType["BOND"] = "BOND";
    /** Contract for Difference. */
    SecType["CFD"] = "CFD";
    /** Futures option. */
    SecType["FOP"] = "FOP";
    /** Warrant. */
    SecType["WAR"] = "WAR";
    SecType["IOPT"] = "IOPT";
    SecType["FWD"] = "FWD";
    /** Combo. */
    SecType["BAG"] = "BAG";
    /* Index. */
    SecType["IND"] = "IND";
    SecType["BILL"] = "BILL";
    /** Mutual fund. */
    SecType["FUND"] = "FUND";
    SecType["FIXED"] = "FIXED";
    SecType["SLB"] = "SLB";
    /** News. */
    SecType["NEWS"] = "NEWS";
    /** Commodity. */
    SecType["CMDTY"] = "CMDTY";
    SecType["BSK"] = "BSK";
    SecType["ICU"] = "ICU";
    SecType["ICS"] = "ICS";
    /** Cryptocurrency. */
    SecType["CRYPTO"] = "CRYPTO";
})(SecType || (exports.SecType = SecType = {}));
exports.default = SecType;
//# sourceMappingURL=sec-type.js.map