using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PackagesProps.Models.Messages;

public enum StatusType { Info, Error }


public record StatusMessage(string Value, StatusType StatusType);

public class StatusValueDataMessage(StatusMessage value) : ValueChangedMessage<StatusMessage>(value);