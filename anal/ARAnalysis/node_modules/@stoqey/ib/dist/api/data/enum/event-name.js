"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EventName = void 0;
/**
 * Emitted event names.
 */
var EventName;
(function (EventName) {
    /** Notifies when an event has been received (called for the any type for event). */
    EventName["all"] = "all";
    /** Notifies when the connection to TWS/IB Gateway has been established successfully. */
    EventName["connected"] = "connected";
    /** Notifies that the TCP socket connection to the TWS/IB Gateway has been disconnected. */
    EventName["disconnected"] = "disconnected";
    /** Notifies about the API server version. */
    EventName["server"] = "server";
    /** A Connection, API, or TWS Error event. */
    EventName["error"] = "error";
    /** An Connection, API, or TWS notification message. */
    EventName["info"] = "info";
    /** Notifies when data has been received from the server. */
    EventName["received"] = "received";
    /** Notifies when data is sent to the server. */
    EventName["sent"] = "sent";
    /** Notifies about the the result to request. */
    EventName["result"] = "result";
    /** Notifies when all the account's information has finished. */
    EventName["accountDownloadEnd"] = "accountDownloadEnd";
    /** Receives the account information. */
    EventName["accountSummary"] = "accountSummary";
    /** Notifies when all the accounts' information has ben received. */
    EventName["accountSummaryEnd"] = "accountSummaryEnd";
    /** Provides the account updates. */
    EventName["accountUpdateMulti"] = "accountUpdateMulti";
    /** Indicates all the account updates have been transmitted. */
    EventName["accountUpdateMultiEnd"] = "accountUpdateMultiEnd";
    /** Delivers the Bond contract data after this has been requested via reqContractDetails. */
    EventName["bondContractDetails"] = "bondContractDetails";
    /** Provides the [[CommissionReport]] of an [[Execution]] */
    EventName["commissionReport"] = "commissionReport";
    /** Feeds in completed orders. */
    EventName["completedOrder"] = "completedOrder";
    /** Notifies the end of the completed orders' reception. */
    EventName["completedOrdersEnd"] = "completedOrdersEnd";
    /** Callback to indicate the API connection has closed. */
    EventName["connectionClosed"] = "connectionClosed";
    /** Receives the full contract's definitions. */
    EventName["contractDetails"] = "contractDetails";
    /** After all contracts matching the request were returned, this method will mark the end of their reception. */
    EventName["contractDetailsEnd"] = "contractDetailsEnd";
    /** TWS's current time. */
    EventName["currentTime"] = "currentTime";
    /** A one-time response to querying the display groups.  */
    EventName["deltaNeutralValidation"] = "deltaNeutralValidation";
    /**
     * When requesting market data snapshots, this market will indicate the snapshot reception is finished.
     * Expected to occur 11 seconds after beginning of request.
     */
    EventName["tickSnapshotEnd"] = "tickSnapshotEnd";
    /** Returns the market data type. */
    EventName["marketDataType"] = "marketDataType";
    /** A one-time response to querying the display groups.  */
    EventName["displayGroupList"] = "displayGroupList";
    /**
     * Call triggered once after receiving the subscription request, and will be sent again
     * if the selected contract in the subscribed display group has changed.
     */
    EventName["displayGroupUpdated"] = "displayGroupUpdated";
    /** Provides the executions which happened in the last 24 hours. */
    EventName["execDetails"] = "execDetails";
    /** Indicates the end of the [[Execution]] reception. */
    EventName["execDetailsEnd"] = "execDetailsEnd";
    /** Returns array of family codes. */
    EventName["familyCodes"] = "familyCodes";
    /** Returns array of sample contract descriptions. */
    EventName["contractDescriptions"] = "contractDescriptions";
    /** Returns fundamental data. */
    EventName["fundamentalData"] = "fundamentalData";
    /** Returns beginning of data for contract for specified data type. */
    EventName["headTimestamp"] = "headTimestamp";
    /** Returns data histogram. */
    EventName["histogramData"] = "histogramData";
    /** Receives bars in real time if keepUpToDate is `true` in reqHistoricalData. */
    EventName["historicalDataUpdate"] = "historicalDataUpdate";
    /** Returns news headline */
    EventName["historicalNews"] = "historicalNews";
    /** Returns news headline. */
    EventName["historicalNewsEnd"] = "historicalNewsEnd";
    /** Returns historical price tick data. */
    EventName["historicalTicks"] = "historicalTicks";
    /** Returns historical bid/ask tick data. */
    EventName["historicalTicksBidAsk"] = "historicalTicksBidAsk";
    /**  Returns historical last price tick data. */
    EventName["historicalTicksLast"] = "historicalTicksLast";
    /** Receives a comma-separated string with the managed account ids. */
    EventName["managedAccounts"] = "managedAccounts";
    /** Returns minimum price increment structure for a particular market rule ID. */
    EventName["marketRule"] = "marketRule";
    /**  Called when receives Depth Market Data Descriptions. */
    EventName["mktDepthExchanges"] = "mktDepthExchanges";
    /** Called when receives News Article. */
    EventName["newsArticle"] = "newsArticle";
    /** Returns array of subscribed API news providers for this user */
    EventName["newsProviders"] = "newsProviders";
    /** Receives next valid order id. */
    EventName["nextValidId"] = "nextValidId";
    /**  Feeds in currently open orders. */
    EventName["openOrder"] = "openOrder";
    /** Notifies the end of the open orders' reception. */
    EventName["openOrderEnd"] = "openOrderEnd";
    /** Response to API bind order control message. */
    EventName["orderBound"] = "orderBound";
    /** Gives the up-to-date information of an order every time it changes. */
    EventName["orderStatus"] = "orderStatus";
    /** Receives PnL updates in real time for the daily PnL and the total unrealized PnL for an account. */
    EventName["pnl"] = "pnl";
    /** Receives real time updates for single position daily PnL values. */
    EventName["pnlSingle"] = "pnlSingle";
    /** Provides the portfolio's open positions. */
    EventName["position"] = "position";
    /** Indicates all the positions have been transmitted. */
    EventName["positionEnd"] = "positionEnd";
    /** Provides the portfolio's open positions. */
    EventName["positionMulti"] = "positionMulti";
    /** Indicates all the positions have been transmitted. */
    EventName["positionMultiEnd"] = "positionMultiEnd";
    /** Updates the real time 5 seconds bars. */
    EventName["realtimeBar"] = "realtimeBar";
    /** Receives the Financial Advisor's configuration available in the TWS. */
    EventName["receiveFA"] = "receiveFA";
    /** Notifies the end of the FA replace. */
    EventName["replaceFAEnd"] = "replaceFAEnd";
    /** Returns conId and exchange for CFD market data request re-route. */
    EventName["rerouteMktDataReq"] = "rerouteMktDataReq";
    /**
     * Returns the conId and exchange for an underlying contract when a request is made for level 2 data for an
     * instrument which does not have data in IB's database. For example stock CFDs and index CFDs.
     */
    EventName["rerouteMktDepthReq"] = "rerouteMktDepthReq";
    /**  Provides the data resulting from the market scanner request. */
    EventName["scannerData"] = "scannerData";
    /** Indicates the scanner data reception has terminated. */
    EventName["scannerDataEnd"] = "scannerDataEnd";
    /** Provides the xml-formatted parameters available from TWS market scanners (not all available in API). */
    EventName["scannerParameters"] = "scannerParameters";
    /** Provides the option chain for an underlying on an exchange specified in reqSecDefOptParams. */
    EventName["securityDefinitionOptionParameter"] = "securityDefinitionOptionParameter";
    /** Called when all callbacks to securityDefinitionOptionParameter are complete. */
    EventName["securityDefinitionOptionParameterEnd"] = "securityDefinitionOptionParameterEnd";
    /** Bit number to exchange + exchange abbreviation dictionary. */
    EventName["smartComponents"] = "smartComponents";
    /** Called when receives Soft Dollar Tier configuration information */
    EventName["softDollarTiers"] = "softDollarTiers";
    /** Provides an array of sample contract descriptions. */
    EventName["symbolSamples"] = "symbolSamples";
    /** Provides "Last" or "AllLast" tick-by-tick real-time tick. */
    EventName["tickByTickAllLast"] = "tickByTickAllLast";
    /** Provides "BidAsk" tick-by-tick real-time tick. */
    EventName["tickByTickBidAsk"] = "tickByTickBidAsk";
    /** Provides "MidPoint" tick-by-tick real-time tick. */
    EventName["tickByTickMidPoint"] = "tickByTickMidPoint";
    /**  Exchange for Physicals. */
    EventName["tickEFP"] = "tickEFP";
    /** Provides a market data generic tick. */
    EventName["tickGeneric"] = "tickGeneric";
    /** Provides a news headline tick. */
    EventName["tickNews"] = "tickNews";
    /** Provides option specific market data. */
    EventName["tickOptionComputation"] = "tickOptionComputation";
    /** Market data tick price callback. Handles all price related ticks. */
    EventName["tickPrice"] = "tickPrice";
    /** A tick with BOO exchange and snapshot permissions. */
    EventName["tickReqParams"] = "tickReqParams";
    /** Market data tick size callback. Handles all size-related ticks. */
    EventName["tickSize"] = "tickSize";
    /**  Market data callback. Every tickPrice is followed by a tickSize. */
    EventName["tickString"] = "tickString";
    /** Receives the last time on which the account was updated. */
    EventName["updateAccountTime"] = "updateAccountTime";
    /** Receives the subscribed account's information. */
    EventName["updateAccountValue"] = "updateAccountValue";
    /** Receives the subscribed account's portfolio. */
    EventName["updatePortfolio"] = "updatePortfolio";
    /** Returns the order book. */
    EventName["updateMktDepth"] = "updateMktDepth";
    /** Returns the order book (level 2). */
    EventName["updateMktDepthL2"] = "updateMktDepthL2";
    /** Provides IB's bulletins. */
    EventName["updateNewsBulletin"] = "updateNewsBulletin";
    /** Returns the requested historical data bars. */
    EventName["historicalData"] = "historicalData";
    /** Returns meta data from the WSH calendar. */
    EventName["wshMetaData"] = "wshMetaData";
    /** Returns calendar events from the WSH. */
    EventName["wshEventData"] = "wshEventData";
    /** Returns historical schedule. */
    EventName["historicalSchedule"] = "historicalSchedule";
    /** Returns user info. */
    EventName["userInfo"] = "userInfo";
})(EventName || (exports.EventName = EventName = {}));
//# sourceMappingURL=event-name.js.map