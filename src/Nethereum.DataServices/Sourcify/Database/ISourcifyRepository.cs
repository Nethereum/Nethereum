using Nethereum.DataServices.Sourcify.Database.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify.Database
{
    public interface ISourcifyRepository
    {
        Task<Code> GetCodeAsync(byte[] codeHash);
        Task AddCodeAsync(Code code);

        Task<Contract> GetContractAsync(Guid id);
        Task AddContractAsync(Contract contract);

        Task<ContractDeployment> GetDeploymentAsync(long chainId, byte[] address);
        Task AddDeploymentAsync(ContractDeployment deployment);

        Task<CompiledContract> GetCompiledContractAsync(Guid id);
        Task AddCompiledContractAsync(CompiledContract compiledContract);

        Task<VerifiedContract> GetVerifiedContractAsync(long chainId, byte[] address);
        Task AddVerifiedContractAsync(VerifiedContract verifiedContract);

        Task<Signature> GetSignatureByHash4Async(byte[] hash4);
        Task<Signature> GetSignatureByHash32Async(byte[] hash32);
        Task<List<Signature>> SearchSignaturesAsync(string query);
        Task AddSignatureAsync(Signature signature);

        Task<Source> GetSourceAsync(byte[] sourceHash);
        Task AddSourceAsync(Source source);

        Task<List<CompiledContractSource>> GetSourcesForCompilationAsync(Guid compilationId);
        Task AddCompiledContractSourceAsync(CompiledContractSource source);

        Task<SourcifyMatch> GetSourcifyMatchAsync(long verifiedContractId);
        Task AddSourcifyMatchAsync(SourcifyMatch match);

        Task BulkInsertAsync<T>(IEnumerable<T> entities) where T : class;
    }
}
