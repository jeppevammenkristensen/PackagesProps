using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PackagesProps.Infrastructure.LongRunning;

public class ProgressDataMessage(double value) : ValueChangedMessage<double>(value);