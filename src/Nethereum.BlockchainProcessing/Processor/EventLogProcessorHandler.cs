using System;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Processor
{
    public class EventLogProcessorHandler<TEvent> : ProcessorHandler<FilterLog> where TEvent : new()
    {
        Func<EventLog<TEvent>, Task> _eventAction;
        Func<EventLog<TEvent>, Task<bool>> _eventCriteria;

        public EventLogProcessorHandler(
            Func<EventLog<TEvent>, Task> action) : this(action, null)
        {
        }

        public EventLogProcessorHandler(
            Func<EventLog<TEvent>, Task> action,
            Func<EventLog<TEvent>, Task<bool>> eventCriteria) 
        {
            _eventAction = action;
            _eventCriteria = eventCriteria;
            SetMatchCriteriaForEvent();
        }

        public EventLogProcessorHandler(
            Action<EventLog<TEvent>> action) : this(action, null)
        {
        }

        public EventLogProcessorHandler(
            Action<EventLog<TEvent>> action,
            Func<EventLog<TEvent>, bool> eventCriteria)
        {
            _eventAction = (l) => { action(l); return Task.FromResult(0); };
            if (eventCriteria != null)
            {
                _eventCriteria = async (l) => { return await Task.FromResult(eventCriteria(l)).ConfigureAwait(false); };
            }
            SetMatchCriteriaForEvent();
        }

        private void SetMatchCriteriaForEvent()
        {
            base.SetMatchCriteria(async log =>
            {
                if (await Task.FromResult(log.IsLogForEvent<TEvent>()).ConfigureAwait(false) == false) return false;

                if (_eventCriteria == null) return true;

                var eventLog = log.DecodeEvent<TEvent>();
                return await _eventCriteria(eventLog).ConfigureAwait(false);
            });
        }

        protected override Task ExecuteInternalAsync(FilterLog value)
        {
            var eventLog = value.DecodeEvent<TEvent>();
            return _eventAction(eventLog);
        }
    }
}