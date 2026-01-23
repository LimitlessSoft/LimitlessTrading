/**
 * Security types.
 */
export declare enum SecType {
    /** Stock (or ETF) */
    STK = "STK",
    OPT = "OPT",
    FUT = "FUT",
    CONTFUT = "CONTFUT",
    /** Forex pair. */
    CASH = "CASH",
    /** Bond. */
    BOND = "BOND",
    /** Contract for Difference. */
    CFD = "CFD",
    /** Futures option. */
    FOP = "FOP",
    /** Warrant. */
    WAR = "WAR",
    IOPT = "IOPT",
    FWD = "FWD",
    /** Combo. */
    BAG = "BAG",
    IND = "IND",
    BILL = "BILL",
    /** Mutual fund. */
    FUND = "FUND",
    FIXED = "FIXED",
    SLB = "SLB",
    /** News. */
    NEWS = "NEWS",
    /** Commodity. */
    CMDTY = "CMDTY",
    BSK = "BSK",
    ICU = "ICU",
    ICS = "ICS",
    /** Cryptocurrency. */
    CRYPTO = "CRYPTO"
}
export default SecType;
