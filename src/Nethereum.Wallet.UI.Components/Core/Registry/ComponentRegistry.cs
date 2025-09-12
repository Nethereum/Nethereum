using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Nethereum.Wallet.UI.Components.Core.Registry
{
    public class ComponentRegistry : IComponentRegistry
    {
        private readonly ConcurrentDictionary<Type, Type> _registrations = new();

        public void Register<TViewModel, TComponent>() 
            where TViewModel : class 
            where TComponent : class
        {
            Register(typeof(TViewModel), typeof(TComponent));
        }

        public void Register(Type viewModelType, Type componentType)
        {
            if (viewModelType == null) throw new ArgumentNullException(nameof(viewModelType));
            if (componentType == null) throw new ArgumentNullException(nameof(componentType));

            _registrations[viewModelType] = componentType;
        }

        public Type? GetComponentType<TViewModel>() where TViewModel : class
        {
            return GetComponentType(typeof(TViewModel));
        }

        public Type? GetComponentType(Type viewModelType)
        {
            return _registrations.TryGetValue(viewModelType, out var componentType) ? componentType : null;
        }

        public IEnumerable<Type> GetRegisteredViewModelTypes()
        {
            return _registrations.Keys;
        }
    }
}