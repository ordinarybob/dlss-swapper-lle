using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DLSS_Swapper.Messages;

internal class GridDensityChangedMessage : ValueChangedMessage<bool>
{
    public GridDensityChangedMessage() : base(true)
    {
    }
}
