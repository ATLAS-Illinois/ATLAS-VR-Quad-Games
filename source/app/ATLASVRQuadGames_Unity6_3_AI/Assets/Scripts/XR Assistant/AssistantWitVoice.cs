using Meta.WitAi;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Interfaces;

/// <summary>Managed Wit recognition with configuration available before SDK Awake.</summary>
public sealed class AssistantWitVoice : Wit, IWitConfigurationProvider
{
    public WitConfiguration Configuration => RuntimeConfiguration?.witConfiguration;
    public bool IsInitialized { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        IsInitialized = true;
    }
}
