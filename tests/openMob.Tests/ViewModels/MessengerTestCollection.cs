namespace openMob.Tests.ViewModels;

/// <summary>
/// Defines a test collection that serialises test classes which share the
/// <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default"/> singleton.
/// <para>
/// <see cref="FlyoutViewModel"/> registers for <see cref="openMob.Core.Messages.SessionDeletedMessage"/>
/// and <see cref="openMob.Core.Messages.CurrentSessionChangedMessage"/> in its constructor.
/// <see cref="openMob.Core.ViewModels.ContextSheetViewModel"/> publishes
/// <see cref="openMob.Core.Messages.SessionDeletedMessage"/> on successful delete.
/// Running these test classes in parallel causes cross-test messenger pollution.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class MessengerTestCollection
{
    /// <summary>The collection name used in <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "MessengerTests";
}
