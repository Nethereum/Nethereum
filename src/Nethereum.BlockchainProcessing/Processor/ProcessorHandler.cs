using System;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processor
{
    public class ProcessorHandler<T> : ProcessorBaseHandler<T>
    {
        private Func<T, Task> _action;

        protected ProcessorHandler(){}

        public ProcessorHandler(Func<T, Task> action)
        {
            _action = action;
        }

        public ProcessorHandler(Func<T, Task> action, Func<T, Task<bool>> criteria):base(criteria)
        {
            _action = action;
        }

        public ProcessorHandler(Func<T, Task> action, Func<T, bool> criteria) : base(criteria)
        {
            _action = action;
        }

        public ProcessorHandler(Action<T> action, Func<T, Task<bool>> criteria) : base(criteria)
        {
            SetAction(action);
        }

        public ProcessorHandler(Action<T> action, Func<T, bool> criteria) : base(criteria)
        {
            SetAction(action);
        }

        private void SetAction(Action<T> action)
        {
            Func<T, Task> asyncAction = (t) =>
            {
                action(t);
                return Task.FromResult(0);
            };

            _action = asyncAction;
        }

        protected override Task ExecuteInternalAsync(T value)
        {
            return _action(value);
        }
    }
}