"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.LogLevel = void 0;
/**
 * TWS / IB Gateway log levels.
 */
var LogLevel;
(function (LogLevel) {
    /** System log level. */
    LogLevel[LogLevel["SYSTEM"] = 1] = "SYSTEM";
    /** Error log level. */
    LogLevel[LogLevel["ERROR"] = 2] = "ERROR";
    /** Warning log level. */
    LogLevel[LogLevel["WARN"] = 3] = "WARN";
    /** Info log level. */
    LogLevel[LogLevel["INFO"] = 4] = "INFO";
    /** Detailed log level. */
    LogLevel[LogLevel["DETAIL"] = 5] = "DETAIL";
})(LogLevel || (exports.LogLevel = LogLevel = {}));
exports.default = LogLevel;
//# sourceMappingURL=log-level.js.map