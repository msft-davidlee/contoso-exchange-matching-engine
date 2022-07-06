using OrderMatcher;

namespace Demo.MatchingEngine
{
    public class MyFeeProvider : IFeeProvider
    {
        public Fee GetFee(short feeId)
        {
            return new Fee { MakerFee = 0, TakerFee = 0 };
        }
    }
}
