using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.Wallet.Services.Transactions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public class TransactionMonitoringService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private IServiceScope? _scope;
        private IPendingTransactionService? _pendingTransactionService;
        
        public TransactionMonitoringService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _scope = _serviceProvider.CreateScope();
            _pendingTransactionService = _scope.ServiceProvider.GetRequiredService<IPendingTransactionService>();
            _pendingTransactionService.StartMonitoring();
            return Task.CompletedTask;
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _pendingTransactionService?.StopMonitoring();
            _scope?.Dispose();
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}