namespace TxTRPG.UI
{
    // Lets a child of FlexibleContentLayoutGroup decide its own main-axis request at calculation time.
    // The parent queries it before FlexibleLayoutItem, so serialized item values remain the fallback.
    public interface IFlexibleLayoutSizeSource
    {
        // Returns false to let the parent use the FlexibleLayoutItem (or default Weighted 1) rule.
        bool TryGetSizeRequest(FlexibleLayoutAxis axis, out FlexibleLayoutSizeRequest request);
    }
}
