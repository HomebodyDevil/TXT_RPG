using NUnit.Framework;
using TxTRPG.Application.Items;
using TxTRPG.UI;

namespace TxTRPG.Application.Tests
{
    public sealed class InventoryGridPresentationTests
    {
        [Test]
        public void LegacySettingsDefaultToManualAndRequestOwnsPresetCopy()
        {
            var settings=new GridContentLayoutSettings(); settings.Normalize();
            Assert.That(settings.presentation.mode,Is.EqualTo(ActionGridDisplayMode.Manual));
            settings.presentation.mode=ActionGridDisplayMode.DistributedSpacing;
            settings.presentation.distributedSpacing.maximumHorizontalGap=27;
            var copy=GridContentLayoutSettings.CreateSafeCopy(settings,out var fallback);
            Assert.That(fallback,Is.False);
            copy.presentation.distributedSpacing.padding.left=90;
            Assert.That(settings.presentation.distributedSpacing.padding.left,Is.EqualTo(14));
            Assert.That(copy.presentation.mode,Is.EqualTo(ActionGridDisplayMode.DistributedSpacing));
            Assert.That(copy.presentation.distributedSpacing.maximumHorizontalGap,Is.EqualTo(27));
            Assert.That(copy.CalculateDisplayCapacity(5),Is.EqualTo(settings.CalculateDisplayCapacity(5)));
        }
    }
}
