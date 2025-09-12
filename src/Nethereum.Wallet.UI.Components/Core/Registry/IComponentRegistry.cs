using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Core.Registry
{
    public interface IComponentRegistry
    {
        void Register<TViewModel, TComponent>() 
            where TViewModel : class 
            where TComponent : class;
        void Register(Type viewModelType, Type componentType);
        Type? GetComponentType<TViewModel>() where TViewModel : class;
        Type? GetComponentType(Type viewModelType);
        IEnumerable<Type> GetRegisteredViewModelTypes();
    }
}