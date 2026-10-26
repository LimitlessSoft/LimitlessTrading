using LSCore.Mapper.Contracts;
using LT.Contracts.Constants;
using LT.Contracts.Dtos.Flex;
using LT.Contracts.Entities;

namespace LT.Contracts.DtoMappings.Flex;

/// <summary>
/// A Flex execution as a journal fill, shaped like a socket one: ExecId is IB's execution id
/// (else "flex:" + tradeID), Symbol the underlying and LocalSymbol the listing's own code (an
/// option's OCC symbol), side "BUY" / "SELL" with a positive quantity. OrderId stays 0: Flex's
/// ibOrderID is not the API order id. The caller sets Time when Flex sent none, and RecordedAt.
/// </summary>
public class FlexTradeFillEntityMapper : ILSCoreMapper<FlexTradeDto, FillEntity>
{
	public FillEntity ToMapped(FlexTradeDto source)
	{
		var symbol = source.UnderlyingSymbol.Length > 0 ? source.UnderlyingSymbol : source.Symbol;
		return new FillEntity
		{
			ExecId =
				source.ExecId.Length > 0 ? source.ExecId
				: source.TradeId.Length > 0 ? FlexConstants.TradeIdExecIdPrefix + source.TradeId
				: string.Empty,
			TradeId = source.TradeId,
			Time = source.Time ?? default,
			Account = source.Account,
			ConId = source.ConId,
			Symbol = symbol,
			LocalSymbol = source.Symbol != symbol ? source.Symbol : string.Empty,
			SecurityType = source.AssetCategory,
			Multiplier = source.Multiplier,
			Side = source.Side,
			Shares = source.Quantity,
			Price = source.Price,
			Exchange = source.Exchange,
			Commission = source.Commission,
			CommissionCurrency = source.CommissionCurrency,
			RealizedPnl = source.RealizedPnl,
		};
	}
}
