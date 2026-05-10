using CommunityToolkit.Mvvm.Messaging.Messages;
using PackagesProps.Infrastructure;

namespace PackagesProps.Models.Messages;

public class ProgressDataMessage(double value) : ValueChangedMessage<double>(value);

public record AddPage<TPage>(TPage Page, bool Select) where TPage : ScreenPage;

public class AddPageMessage<TPage>(AddPage<TPage> message) : ValueChangedMessage<AddPage<TPage>>(message) where TPage : ScreenPage;