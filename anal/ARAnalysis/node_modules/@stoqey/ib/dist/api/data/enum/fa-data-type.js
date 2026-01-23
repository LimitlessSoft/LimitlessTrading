"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.FADataType = void 0;
/**
 * Financial Advisor's configuration data types.
 */
var FADataType;
(function (FADataType) {
    FADataType[FADataType["NA"] = 0] = "NA";
    /** Offer traders a way to create a group of accounts and apply a single allocation method to all accounts in the group. */
    FADataType[FADataType["GROUPS"] = 1] = "GROUPS";
    /** @deprecated Let you allocate shares on an account-by-account basis using a predefined calculation value. */
    FADataType[FADataType["PROFILES"] = 2] = "PROFILES";
    /** Let you easily identify the accounts by meaningful names rather than account numbers. */
    FADataType[FADataType["ALIASES"] = 3] = "ALIASES";
})(FADataType || (exports.FADataType = FADataType = {}));
exports.default = FADataType;
//# sourceMappingURL=fa-data-type.js.map