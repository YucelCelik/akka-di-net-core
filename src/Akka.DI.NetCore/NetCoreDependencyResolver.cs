using System;
using System.Collections.Concurrent;
using System.Linq;
using Akka.Actor;
using Akka.DI.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Akka.DI.NetCore
{
    public class NetCoreDependencyResolver : IDependencyResolver, INoSerializationVerificationNeeded
    {
        private readonly ActorSystem _system;
        private readonly IServiceCollection _serviceCollection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<ActorBase, IServiceScope> _activeScopes = new ConcurrentDictionary<ActorBase, IServiceScope>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NetCoreDependencyResolver"/> class.
        /// </summary>
        /// <param name="serviceCollection">The service collection used to resolve references</param>
        /// <param name="system">The actor system to plug into</param>
        /// <exception cref="ArgumentNullException">
        /// Either the <paramref name="serviceCollection"/> or the <paramref name="system"/> was null.
        /// </exception>
        public NetCoreDependencyResolver(IServiceCollection serviceCollection, ActorSystem system)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            _serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));
            _serviceProvider = _serviceCollection.BuildServiceProvider();
            _system.AddDependencyResolver(this);
        }

        /// <summary>
        /// Retrieves an actor's type with the specified name
        /// </summary>
        /// <param name="actorName">The name of the actor to retrieve</param>
        /// <returns>The type with the specified actor name</returns>
        public Type GetType(string actorName)
        {
            _typeCache.
                TryAdd(actorName,
                        actorName.GetTypeValue() ??
                        _serviceCollection.Where(c => c.ServiceType.FullName.Equals(actorName, StringComparison.OrdinalIgnoreCase)).
                            Select(c => c.ImplementationType).
                        FirstOrDefault());

            return _typeCache[actorName];
        }

        /// <summary>
        /// Creates a delegate factory used to create actors based on their type
        /// </summary>
        /// <param name="actorType">The type of actor that the factory builds</param>
        /// <returns>A delegate factory used to create actors</returns>
        public Func<ActorBase> CreateActorFactory(Type actorType)
        {
            return () =>
            {
                var serviceScope = _serviceProvider.CreateScope();
                var actor = (ActorBase)serviceScope.ServiceProvider.GetService(actorType);
                _activeScopes[actor] = serviceScope;
                return actor;
            };
        }

        /// <summary>
        /// Used to register the configuration for an actor of the specified type <typeparamref name="TActor"/>
        /// </summary>
        /// <typeparam name="TActor">The type of actor the configuration is based</typeparam>
        /// <returns>The configuration object for the given actor type</returns>
        public Props Create<TActor>() where TActor : ActorBase
        {
            return Create(typeof(TActor));
        }

        /// <summary>
        /// Used to register the configuration for an actor of the specified type <paramref name="actorType"/> 
        /// </summary>
        /// <param name="actorType">The <see cref="Type"/> of actor the configuration is based</param>
        /// <returns>The configuration object for the given actor type</returns>
        public virtual Props Create(Type actorType)
        {
            var result = _system.GetExtension<DIExt>().Props(actorType);
            return result;
        }

        /// <summary>
        /// Signals the service collection to remove it's reference to the actor.
        /// </summary>
        /// <param name="actor">The actor to remove from the container</param>
        public void Release(ActorBase actor)
        {
            if (_activeScopes.TryRemove(actor, out var scope))
            {
                scope.Dispose();
            }
        }
    }
}
