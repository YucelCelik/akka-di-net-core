```csharp

public static void RegisterComponents(IServiceCollection services)
{
    var config = ConfigurationFactory.ParseString(File.ReadAllText("akka.config"));
    var actorSystem = ActorSystem.Create("ProductCompareEngineActorSystem", config);
    services.AddSingleton(actorSystem);

    var propsResolver = new NetCoreDependencyResolver(services, actorSystem);

    var props = propsResolver.Create<SupervisorActor>();

    var newProps = props.WithRouter(FromConfig.Instance);

    actorSystem.ActorOf(newProps, "SupervisorActor");
}
